using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 던전 생성 알고리즘 (Day 3) + 방 프리팹 배치 / 문 막기 / NavMesh 빌드 / 플레이어 배치 (Day 4).
///
/// 흐름:
///  1) 격자 가장자리 한 칸에 입구방 배치
///  2) 인접한 빈 칸으로 랜덤 확장 (부모 한 곳과만 문 연결 = 루프 없는 트리)
///  3) 입구에서 BFS 거리 계산 -> 가장 먼 방 = 보스방
///  4) 막다른 방 중 보스가 아닌 곳 = 보물방 (부족하면 일반방에서 지정)
///  5) 검증 (도달성/개수/문 대응) -> 실패하면 시드를 바꿔 재생성, 사용한 시드 기록
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("격자")]
    [Tooltip("격자 크기 (가로 x 세로). 방 개수보다 칸 수가 많아야 한다")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(5, 5);

    [Header("방 개수")]
    [SerializeField, Min(3)] private int minRooms = 8;
    [SerializeField, Min(3)] private int maxRooms = 10;
    [Tooltip("보물방 최소/최대 개수 (기획서 1~2). 막다른 방이 부족하면 일반방에서 지정")]
    [SerializeField, Min(1)] private int minTreasureRooms = 1;
    [SerializeField, Min(1)] private int maxTreasureRooms = 2;

    [Header("시드")]
    [Tooltip("켜면 실행할 때마다 새 시드를 뽑는다. 끄면 아래 Seed 값을 사용 (같은 배치 재현)")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed = 12345;
    [Tooltip("마지막으로 성공한 생성에 실제로 쓰인 시드 (읽기용). 버그 재현 시 Seed 에 넣고 Use Random Seed 를 끈다")]
    [SerializeField] private int lastSeed;
    [Tooltip("검증 실패 시 재생성 최대 횟수")]
    [SerializeField, Min(1)] private int maxAttempts = 50;

    [Header("실행")]
    [SerializeField] private bool generateOnStart = true;

    [Header("방 프리팹 (Day 4)")]
    [SerializeField] private GameObject entrancePrefab;
    [Tooltip("일반방 변형들 (A~D). 방마다 이 중 하나가 시드에 따라 선택된다")]
    [SerializeField] private GameObject[] normalPrefabs;
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject bossPrefab;
    [Tooltip("방 한 칸의 크기(m). 프리팹의 바닥 크기와 같아야 한다 (기본 20). Gizmo 칸 크기도 이 값을 쓴다")]
    [SerializeField, Min(1f)] private float roomSize = 20f;
    [Tooltip("생성된 방이 자식으로 들어갈 부모 (GeneratedRooms). 비우면 NavMesh 오브젝트, 그것도 없으면 이 오브젝트")]
    [SerializeField] private Transform roomParent;
    [Tooltip("GeneratedRooms 에 붙인 DungeonNavMesh. 방을 만든 뒤 NavMesh 를 빌드한다")]
    [SerializeField] private DungeonNavMesh navMesh;

    [Header("플레이어 배치 (Day 4)")]
    [Tooltip("비우면 씬에서 PlayerController 를 찾아 사용")]
    [SerializeField] private Transform player;

    [Header("검증용 몬스터 (Day 7 에 RoomSpawner 가 대체)")]
    [Tooltip("일반/보물/보스방의 MonsterSpawn 위치에 생성할 몬스터 (좀비 프리팹 등). 비우면 생성 안 함")]
    [SerializeField] private GameObject testMonsterPrefab;
    [SerializeField, Range(0f, 1f)] private float testMonsterChance = 1f;

    [Header("Gizmo")]
    [SerializeField] private bool showLabels = true;

    /// <summary>Gizmo 칸 크기 = 방 크기.</summary>
    private float cellSize => roomSize;

    private List<RoomData> rooms = new List<RoomData>();
    private Dictionary<Vector2Int, RoomData> roomMap = new Dictionary<Vector2Int, RoomData>();

    // Day 4: 실제로 생성된 방 오브젝트
    private readonly Dictionary<Vector2Int, Room> spawnedRooms = new Dictionary<Vector2Int, Room>();
    private Transform monsterParent;

    /// <summary>방 배치 + NavMesh 빌드 + 플레이어 배치까지 끝났을 때 (Day 7 RoomSpawner 등이 구독).</summary>
    public event Action DungeonBuilt;

    // ---------- 외부(Day 4) 에서 읽는 값 ----------
    public IReadOnlyList<RoomData> Rooms => rooms;
    public int LastSeed => lastSeed;
    public Vector2Int GridSize => gridSize;
    public RoomData Entrance => rooms.FirstOrDefault(r => r.Type == RoomType.Entrance);
    public RoomData Boss => rooms.FirstOrDefault(r => r.Type == RoomType.Boss);
    public IEnumerable<RoomData> Treasures => rooms.Where(r => r.Type == RoomType.Treasure);

    public RoomData GetRoom(Vector2Int pos)
    {
        roomMap.TryGetValue(pos, out var r);
        return r;
    }

    /// <summary>실제로 생성된 방 오브젝트 (Build Dungeon 이후). 없으면 null.</summary>
    public Room GetSpawnedRoom(Vector2Int pos)
    {
        spawnedRooms.TryGetValue(pos, out var r);
        return r;
    }

    /// <summary>격자 좌표 -> 월드 좌표 (방 바닥 정중앙, y = 생성기 높이). x -> X, y -> Z.</summary>
    public Vector3 RoomToWorld(Vector2Int gridPos)
    {
        return transform.position + new Vector3(gridPos.x * roomSize, 0f, gridPos.y * roomSize);
    }

    // ---------- 진입점 ----------

    private void Start()
    {
        if (generateOnStart) BuildDungeon();
    }

    private void OnValidate()
    {
        gridSize.x = Mathf.Max(1, gridSize.x);
        gridSize.y = Mathf.Max(1, gridSize.y);
        if (maxRooms < minRooms) maxRooms = minRooms;
        if (maxTreasureRooms < minTreasureRooms) maxTreasureRooms = minTreasureRooms;
    }

    /// <summary>던전을 새로 만든다. 성공하면 true.</summary>
    [ContextMenu("Generate")]
    public bool Generate()
    {
        if (!CheckConfig(out string configError))
        {
            Debug.LogError($"[Dungeon] 설정 오류: {configError}");
            return false;
        }

        int baseSeed = useRandomSeed ? new System.Random().Next() : seed;

        if (!BuildWithRetries(baseSeed, out var result, out int usedSeed, out int attempts, out string lastError))
        {
            Debug.LogError($"[Dungeon] {maxAttempts}회 재생성했지만 실패했습니다. 마지막 원인: {lastError}");
            return false;
        }

        rooms = result;
        roomMap = rooms.ToDictionary(r => r.Position);
        lastSeed = usedSeed;

        var boss = Boss;
        var treasureText = string.Join(", ", Treasures.Select(t => t.Position.ToString()));
        Debug.Log($"[Dungeon] 생성 완료 | 시드 {usedSeed} | 시도 {attempts}회 | 방 {rooms.Count}개 | " +
                  $"입구 {Entrance.Position} | 보스 {boss.Position} (거리 {boss.Distance}) | 보물 {treasureText}\n" +
                  BuildAscii(rooms));
        return true;
    }

    // ---------- 실제 던전 만들기 (Day 4) ----------

    /// <summary>
    /// 던전 데이터 생성 -> 이전 던전 제거 -> 방 프리팹 배치(문 Blocker 처리) -> NavMesh 빌드
    /// -> 플레이어 배치 -> 검증용 몬스터 생성. Play 모드 전용.
    /// 몬스터는 NavMesh 가 만들어진 뒤에 생성해야 NavMeshAgent 가 정상 동작한다.
    /// </summary>
    [ContextMenu("Build Dungeon (Play Mode)")]
    public bool BuildDungeon()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Dungeon] 실제 방 생성은 Play 모드에서만 실행합니다. (에디터의 Generate 는 Gizmo 확인용)");
            return false;
        }

        if (!CheckPrefabs(out string prefabError))
        {
            Debug.LogError($"[Dungeon] 방 프리팹 설정 오류: {prefabError}", this);
            return false;
        }

        if (!Generate()) return false;

        ClearDungeon();
        SpawnRooms();

        if (navMesh != null) navMesh.Build();
        else Debug.LogWarning("[Dungeon] Nav Mesh 가 연결되지 않아 NavMesh 를 빌드하지 못했습니다.", this);

        PlacePlayer();
        SpawnTestMonsters();
        ValidateNavigation();

        DungeonBuilt?.Invoke();
        return true;
    }

    private bool CheckPrefabs(out string error)
    {
        if (entrancePrefab == null) { error = "Entrance Prefab 이 비어 있습니다."; return false; }
        if (treasurePrefab == null) { error = "Treasure Prefab 이 비어 있습니다."; return false; }
        if (bossPrefab == null) { error = "Boss Prefab 이 비어 있습니다."; return false; }
        if (normalPrefabs == null || normalPrefabs.Length == 0) { error = "Normal Prefabs 가 비어 있습니다."; return false; }
        for (int i = 0; i < normalPrefabs.Length; i++)
        {
            if (normalPrefabs[i] == null) { error = $"Normal Prefabs[{i}] 가 비어 있습니다."; return false; }
        }
        error = null;
        return true;
    }

    /// <summary>이전에 만든 방/몬스터를 제거한다. 같은 프레임 NavMesh 빌드에 포함되지 않도록 먼저 비활성화한다.</summary>
    private void ClearDungeon()
    {
        foreach (var room in spawnedRooms.Values)
        {
            if (room == null) continue;
            room.gameObject.SetActive(false);
            Destroy(room.gameObject);
        }
        spawnedRooms.Clear();

        if (monsterParent != null)
        {
            monsterParent.gameObject.SetActive(false);
            Destroy(monsterParent.gameObject);
            monsterParent = null;
        }
    }

    private void SpawnRooms()
    {
        Transform parent = roomParent != null ? roomParent : (navMesh != null ? navMesh.transform : transform);

        // 방 변형 선택은 시드에서 파생 -> 같은 시드면 같은 방 구성
        var rng = new System.Random(unchecked(lastSeed * 31 + 7));

        foreach (var data in rooms)
        {
            var prefab = PickPrefab(data.Type, rng);
            var go = Instantiate(prefab, RoomToWorld(data.Position), Quaternion.identity, parent);
            go.name = $"{prefab.name}_{data.Position.x}_{data.Position.y}";

            if (!go.TryGetComponent(out Room room)) room = go.AddComponent<Room>();
            room.Setup(data);
            spawnedRooms[data.Position] = room;
        }
    }

    private GameObject PickPrefab(RoomType type, System.Random rng)
    {
        switch (type)
        {
            case RoomType.Entrance: return entrancePrefab;
            case RoomType.Treasure: return treasurePrefab;
            case RoomType.Boss: return bossPrefab;
            default: return normalPrefabs[rng.Next(normalPrefabs.Length)];
        }
    }

    private void PlacePlayer()
    {
        Transform target = player;
        if (target == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
        if (target == null)
        {
            Debug.LogWarning("[Dungeon] 플레이어를 찾지 못해 배치하지 못했습니다. Player 칸을 연결하세요.", this);
            return;
        }

        var entranceRoom = GetSpawnedRoom(Entrance.Position);
        Vector3 pos = entranceRoom.transform.position;
        Quaternion rot = Quaternion.identity;
        if (entranceRoom.PlayerSpawn != null)
        {
            pos = entranceRoom.PlayerSpawn.position;
            rot = entranceRoom.PlayerSpawn.rotation;
        }
        else
        {
            Debug.LogWarning("[Dungeon] 입구방에 PlayerSpawn 이 없어 방 중앙에 배치합니다.", this);
        }

        // CharacterController 는 켜진 채로 transform 을 옮기면 되돌려지므로 잠깐 끈다
        var cc = target.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        target.SetPositionAndRotation(pos + Vector3.up * 0.05f, rot);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
    }

    private void SpawnTestMonsters()
    {
        if (testMonsterPrefab == null) return;

        // 몬스터 콜라이더가 NavMesh 에 구멍을 내지 않도록 방(GeneratedRooms) 밖에 따로 모은다
        monsterParent = new GameObject("SpawnedMonsters").transform;

        var rng = new System.Random(unchecked(lastSeed * 17 + 3));
        int count = 0, failed = 0;

        foreach (var data in rooms)
        {
            if (data.Type == RoomType.Entrance) continue;
            if (!spawnedRooms.TryGetValue(data.Position, out var room)) continue;

            foreach (var sp in room.MonsterSpawns)
            {
                if (rng.NextDouble() > testMonsterChance) continue;

                if (NavMesh.SamplePosition(sp.position, out var hit, 2f, NavMesh.AllAreas))
                {
                    Instantiate(testMonsterPrefab, hit.position,
                                Quaternion.Euler(0f, sp.eulerAngles.y, 0f), monsterParent);
                    count++;
                }
                else
                {
                    failed++;
                    Debug.LogWarning($"[Dungeon] {room.name}/{sp.name} 근처에 NavMesh 가 없어 몬스터를 만들지 못했습니다.", sp);
                }
            }
        }

        Debug.Log($"[Dungeon] 검증용 몬스터 {count}마리 생성" + (failed > 0 ? $" (실패 {failed})" : ""));
    }

    /// <summary>입구방에서 모든 방 중앙까지 NavMesh 경로가 이어지는지 확인한다 (문 구멍/Blocker 점검용).</summary>
    private void ValidateNavigation()
    {
        var entranceRoom = GetSpawnedRoom(Entrance.Position);
        if (!NavMesh.SamplePosition(entranceRoom.transform.position, out var start, 3f, NavMesh.AllAreas))
        {
            Debug.LogError("[Dungeon] 입구방에서 NavMesh 를 찾지 못했습니다. NavMeshSurface 설정(Include Layers, Collect Objects)을 확인하세요.");
            return;
        }

        var path = new NavMeshPath();
        int bad = 0;
        foreach (var data in rooms)
        {
            var room = GetSpawnedRoom(data.Position);
            bool ok = false;

            foreach (var dir in DirectionUtil.All)
            {
                if (!data.HasDoor(dir)) continue;
                var door = room.transform.Find($"Doors/Door_{dir}");
                if (door == null) continue;

                if (NavMesh.SamplePosition(door.position, out var end, 2f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    ok = true;
                    break;
                }
            }

            if (!ok)
            {
                bad++;
                Debug.LogError($"[Dungeon] NavMesh 로 도달할 수 없는 방: {data} / {room.name}", room);
            }
        }
    }

    // ---------- 설정 검사 ----------

    private bool CheckConfig(out string error)
    {
        int area = gridSize.x * gridSize.y;
        if (minRooms < 2 + minTreasureRooms)
        {
            error = $"Min Rooms({minRooms}) 는 입구 + 보스 + 보물방 최소 개수({2 + minTreasureRooms}) 이상이어야 합니다.";
            return false;
        }
        if (area < minRooms)
        {
            error = $"격자 {gridSize.x}x{gridSize.y}({area}칸) 가 Min Rooms({minRooms}) 보다 작습니다. 격자를 키우거나 방 개수를 줄이세요.";
            return false;
        }
        error = null;
        return true;
    }

    // ---------- 재생성 루프 ----------

    private bool BuildWithRetries(int baseSeed, out List<RoomData> result, out int usedSeed,
                                  out int attempts, out string lastError)
    {
        result = null;
        usedSeed = baseSeed;
        attempts = 0;
        lastError = null;

        for (int i = 0; i < maxAttempts; i++)
        {
            attempts = i + 1;
            int trySeed = unchecked(baseSeed + i);
            if (TryBuild(trySeed, out var list, out string error))
            {
                result = list;
                usedSeed = trySeed;
                return true;
            }
            lastError = error;
        }
        return false;
    }

    // ---------- 한 번의 생성 시도 ----------

    private bool TryBuild(int trySeed, out List<RoomData> result, out string error)
    {
        result = null;
        error = null;

        var rng = new System.Random(trySeed);
        var list = new List<RoomData>();
        var map = new Dictionary<Vector2Int, RoomData>();

        int area = gridSize.x * gridSize.y;
        int hi = Mathf.Min(maxRooms, area);
        int lo = Mathf.Min(minRooms, hi);
        int target = rng.Next(lo, hi + 1);

        // 1) 가장자리 한 칸에 입구방
        var edges = new List<Vector2Int>();
        for (int x = 0; x < gridSize.x; x++)
            for (int y = 0; y < gridSize.y; y++)
                if (x == 0 || y == 0 || x == gridSize.x - 1 || y == gridSize.y - 1)
                    edges.Add(new Vector2Int(x, y));

        var startPos = edges[rng.Next(edges.Count)];
        var entrance = new RoomData(startPos) { Type = RoomType.Entrance };
        list.Add(entrance);
        map[startPos] = entrance;

        // 2) 인접 빈 칸으로 랜덤 확장
        var candidates = new List<(RoomData parent, Direction dir)>();
        while (list.Count < target)
        {
            candidates.Clear();
            foreach (var room in list)
            {
                foreach (var dir in DirectionUtil.All)
                {
                    var np = room.Position + dir.ToOffset();
                    if (InBounds(np) && !map.ContainsKey(np))
                        candidates.Add((room, dir));
                }
            }

            if (candidates.Count == 0)
            {
                error = $"확장할 빈 칸이 없습니다 (현재 {list.Count}개 / 목표 {target}개)";
                return false;
            }

            var pick = candidates[rng.Next(candidates.Count)];
            var childPos = pick.parent.Position + pick.dir.ToOffset();
            var child = new RoomData(childPos);

            // 부모와 자식 양쪽에 문 연결 (부모는 해당 방향, 자식은 반대 방향)
            pick.parent.Doors |= pick.dir;
            child.Doors |= pick.dir.Opposite();

            list.Add(child);
            map[childPos] = child;
        }

        // 3~4) 거리 계산 + 방 종류 지정
        ComputeDistances(entrance, map);
        AssignTypes(list, rng);

        // 5) 검증
        error = Validate(list, map, lo, hi);
        if (error != null) return false;

        result = list;
        return true;
    }

    private bool InBounds(Vector2Int p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < gridSize.x && p.y < gridSize.y;
    }

    // ---------- 거리 (BFS) ----------

    private static void ComputeDistances(RoomData entrance, Dictionary<Vector2Int, RoomData> map)
    {
        foreach (var r in map.Values) r.Distance = -1;

        var queue = new Queue<RoomData>();
        entrance.Distance = 0;
        queue.Enqueue(entrance);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var dir in DirectionUtil.All)
            {
                if (!cur.HasDoor(dir)) continue;
                if (!map.TryGetValue(cur.Position + dir.ToOffset(), out var next)) continue;
                if (next.Distance != -1) continue;
                next.Distance = cur.Distance + 1;
                queue.Enqueue(next);
            }
        }
    }

    // ---------- 방 종류 지정 ----------

    private void AssignTypes(List<RoomData> list, System.Random rng)
    {
        foreach (var r in list)
            if (r.Type != RoomType.Entrance) r.Type = RoomType.Normal;

        // 보스방: 입구에서 가장 먼 방 (동점이면 랜덤). 트리에서는 항상 막다른 방이다.
        int maxDist = list.Where(r => r.Type == RoomType.Normal).Max(r => r.Distance);
        var bossCandidates = list.Where(r => r.Type == RoomType.Normal && r.Distance == maxDist).ToList();
        bossCandidates[rng.Next(bossCandidates.Count)].Type = RoomType.Boss;

        // 보물방: 막다른 방(보스 제외) 우선, 입구에서 먼 쪽 우선.
        // 막다른 방이 부족하면 일반방 중에서 지정 (기획서 2.4 배치 규칙 4)
        int treasureCount = rng.Next(minTreasureRooms, maxTreasureRooms + 1);

        var deadEnds = Shuffle(list.Where(r => r.Type == RoomType.Normal && r.IsDeadEnd), rng)
            .OrderByDescending(r => r.Distance).ToList();
        var others = Shuffle(list.Where(r => r.Type == RoomType.Normal && !r.IsDeadEnd), rng)
            .OrderByDescending(r => r.Distance).ToList();

        foreach (var r in deadEnds.Concat(others).Take(treasureCount))
            r.Type = RoomType.Treasure;
    }

    private static List<T> Shuffle<T>(IEnumerable<T> source, System.Random rng)
    {
        var l = source.ToList();
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (l[i], l[j]) = (l[j], l[i]);
        }
        return l;
    }

    // ---------- 검증 ----------

    /// <summary>문제가 없으면 null, 있으면 원인 문자열.</summary>
    private string Validate(List<RoomData> list, Dictionary<Vector2Int, RoomData> map, int lo, int hi)
    {
        if (list.Count < lo || list.Count > hi)
            return $"방 개수 {list.Count} 가 범위({lo}~{hi}) 밖";

        int entranceCount = list.Count(r => r.Type == RoomType.Entrance);
        int bossCount = list.Count(r => r.Type == RoomType.Boss);
        int treasureCount = list.Count(r => r.Type == RoomType.Treasure);

        if (entranceCount != 1) return $"입구방 {entranceCount}개";
        if (bossCount != 1) return $"보스방 {bossCount}개";
        if (treasureCount < 1 || treasureCount > maxTreasureRooms) return $"보물방 {treasureCount}개";

        foreach (var r in list)
        {
            if (!InBounds(r.Position)) return $"격자 밖의 방 {r.Position}";
            if (r.Distance < 0) return $"입구에서 도달할 수 없는 방 {r.Position}";

            // 문 대응 검사: A 의 East 문이면 B 의 West 문
            foreach (var dir in DirectionUtil.All)
            {
                if (!r.HasDoor(dir)) continue;
                if (!map.TryGetValue(r.Position + dir.ToOffset(), out var neighbor))
                    return $"{r.Position} 의 {dir} 문 너머에 방이 없음";
                if (!neighbor.HasDoor(dir.Opposite()))
                    return $"{r.Position} 의 {dir} 문과 {neighbor.Position} 의 문이 대응되지 않음";
            }
        }
        return null;
    }

    // ---------- 텍스트 지도 (콘솔 확인용) ----------

    /// <summary>E=입구 n=일반 T=보물 B=보스. 위쪽이 North(+y).</summary>
    private string BuildAscii(List<RoomData> list)
    {
        int w = gridSize.x * 2 - 1;
        int h = gridSize.y * 2 - 1;
        var canvas = new char[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                canvas[x, y] = ' ';

        foreach (var r in list)
        {
            int cx = r.Position.x * 2;
            int cy = r.Position.y * 2;
            canvas[cx, cy] = TypeLetter(r.Type);
            if (r.HasDoor(Direction.East) && cx + 1 < w) canvas[cx + 1, cy] = '-';
            if (r.HasDoor(Direction.North) && cy + 1 < h) canvas[cx, cy + 1] = '|';
        }

        var sb = new StringBuilder();
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++) sb.Append(canvas[x, y]);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static char TypeLetter(RoomType t)
    {
        switch (t)
        {
            case RoomType.Entrance: return 'E';
            case RoomType.Treasure: return 'T';
            case RoomType.Boss: return 'B';
            default: return 'n';
        }
    }

    // ---------- 반복 테스트 ----------

    [ContextMenu("Run 100 Tests")]
    private void RunTests()
    {
        if (!CheckConfig(out string configError))
        {
            Debug.LogError($"[Dungeon] 설정 오류: {configError}");
            return;
        }

        const int runs = 100;
        int fail = 0, retrySum = 0, maxRetry = 0;
        int minCount = int.MaxValue, maxCount = 0;
        var treasureHistogram = new Dictionary<int, int>();
        int bossFarthestCount = 0;
        var seedSource = new System.Random();

        for (int i = 0; i < runs; i++)
        {
            int baseSeed = seedSource.Next();
            if (!BuildWithRetries(baseSeed, out var list, out _, out int attempts, out _))
            {
                fail++;
                continue;
            }

            retrySum += attempts - 1;
            maxRetry = Mathf.Max(maxRetry, attempts - 1);
            minCount = Mathf.Min(minCount, list.Count);
            maxCount = Mathf.Max(maxCount, list.Count);

            int tc = list.Count(r => r.Type == RoomType.Treasure);
            treasureHistogram[tc] = treasureHistogram.TryGetValue(tc, out var v) ? v + 1 : 1;

            int farthest = list.Max(r => r.Distance);
            if (list.First(r => r.Type == RoomType.Boss).Distance == farthest) bossFarthestCount++;
        }

        // 재현성 검사: 같은 시드로 두 번 만들면 같은 배치여야 한다
        int probeSeed = seedSource.Next();
        bool reproducible = false;
        if (BuildWithRetries(probeSeed, out var a, out _, out _, out _) &&
            BuildWithRetries(probeSeed, out var b, out _, out _, out _))
        {
            reproducible = BuildAscii(a) == BuildAscii(b);
        }

        int ok = runs - fail;
        var hist = string.Join(", ", treasureHistogram.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}개:{kv.Value}회"));
        string summary =
            $"[Dungeon] 테스트 {runs}회 | 성공 {ok} / 한도 초과 실패 {fail}\n" +
            $"  재시도 평균 {(ok > 0 ? (float)retrySum / ok : 0f):F2}회, 최대 {maxRetry}회\n" +
            $"  방 개수 범위 {(ok > 0 ? minCount : 0)}~{maxCount}\n" +
            $"  보물방 개수 분포 {hist}\n" +
            $"  보스가 가장 먼 방: {bossFarthestCount} / {ok}\n" +
            $"  같은 시드 재현: {(reproducible ? "OK" : "실패")}";

        if (fail > 0 || !reproducible) Debug.LogError(summary);
        else Debug.Log(summary);
    }

    // ---------- Gizmo ----------

    private void OnDrawGizmos()
    {
        if (rooms == null || rooms.Count == 0) return;

        Vector3 origin = transform.position;

        // 격자 칸 윤곽 (연한 회색)
        Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
        for (int x = 0; x < gridSize.x; x++)
            for (int y = 0; y < gridSize.y; y++)
                Gizmos.DrawWireCube(origin + CellCenter(x, y), new Vector3(cellSize, 0.01f, cellSize));

        foreach (var r in rooms)
        {
            Vector3 c = origin + CellCenter(r.Position.x, r.Position.y);

            Gizmos.color = TypeColor(r.Type);
            Gizmos.DrawCube(c, new Vector3(cellSize * 0.7f, 0.2f, cellSize * 0.7f));

            // 문 연결선 (East / North 만 그려서 중복 방지)
            Gizmos.color = Color.white;
            if (r.HasDoor(Direction.East))
                Gizmos.DrawLine(c, origin + CellCenter(r.Position.x + 1, r.Position.y));
            if (r.HasDoor(Direction.North))
                Gizmos.DrawLine(c, origin + CellCenter(r.Position.x, r.Position.y + 1));

#if UNITY_EDITOR
            if (showLabels)
                Handles.Label(c + Vector3.up * 0.6f, $"{TypeLetter(r.Type)}  d{r.Distance}");
#endif
        }
    }

    private Vector3 CellCenter(int x, int y)
    {
        return new Vector3(x * cellSize, 0f, y * cellSize);
    }

    private static Color TypeColor(RoomType t)
    {
        switch (t)
        {
            case RoomType.Entrance: return new Color(0.2f, 0.8f, 0.3f);
            case RoomType.Treasure: return new Color(1f, 0.85f, 0.1f);
            case RoomType.Boss: return new Color(0.9f, 0.15f, 0.15f);
            default: return new Color(0.55f, 0.55f, 0.55f);
        }
    }
}