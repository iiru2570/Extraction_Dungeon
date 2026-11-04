using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 프리팹 한 칸 (Day 4). DungeonGenerator 가 방을 만들 때 자동으로 붙인다 (프리팹에 직접 붙일 필요 없음).
/// 프리팹 계층의 이름으로 문(Blocker)과 스폰 포인트를 자동 수집한다.
///
/// 기대하는 계층:
///   Doors/Door_North|East|South|West/Blocker
///   Spawns/Monsters/MonsterSpawn_NN
///   Spawns/Chests/ChestSpawn_NN
///   Spawns/Special/PlayerSpawn | GuardianSpawn | BossSpawn
/// </summary>
public class Room : MonoBehaviour
{
    /// <summary>이 방의 생성 데이터 (격자 좌표, 종류, 문).</summary>
    public RoomData Data { get; private set; }

    public Transform PlayerSpawn { get; private set; }
    public Transform GuardianSpawn { get; private set; }
    public Transform BossSpawn { get; private set; }

    public IReadOnlyList<Transform> MonsterSpawns => monsterSpawns;
    public IReadOnlyList<Transform> ChestSpawns => chestSpawns;

    private readonly List<Transform> monsterSpawns = new List<Transform>();
    private readonly List<Transform> chestSpawns = new List<Transform>();
    private readonly Dictionary<Direction, GameObject> blockers = new Dictionary<Direction, GameObject>();
    private bool collected;

    /// <summary>데이터를 받아 문을 열고 닫는다. 문이 있는 방향은 Blocker 를 끄고, 없는 방향은 켠다.</summary>
    public void Setup(RoomData data)
    {
        Data = data;
        Collect();

        foreach (var dir in DirectionUtil.All)
        {
            if (blockers.TryGetValue(dir, out var blocker) && blocker != null)
                blocker.SetActive(!data.HasDoor(dir));
        }

        if (data.Type == RoomType.Entrance && PlayerSpawn == null)
            Debug.LogWarning($"[Room] {name}: 입구방인데 Spawns/Special/PlayerSpawn 이 없습니다.", this);
        if (data.Type == RoomType.Boss && BossSpawn == null)
            Debug.LogWarning($"[Room] {name}: 보스방인데 Spawns/Special/BossSpawn 이 없습니다.", this);
        if (data.Type == RoomType.Treasure && GuardianSpawn == null)
            Debug.LogWarning($"[Room] {name}: 보물방인데 Spawns/Special/GuardianSpawn 이 없습니다.", this);
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;

        // 문 Blocker (Direction 이름이 North/East/South/West 라서 Door_{dir} 로 찾는다)
        foreach (var dir in DirectionUtil.All)
        {
            var t = transform.Find($"Doors/Door_{dir}/Blocker");
            if (t != null) blockers[dir] = t.gameObject;
            else Debug.LogWarning($"[Room] {name}: Doors/Door_{dir}/Blocker 를 찾지 못했습니다. 이름을 확인하세요.", this);
        }

        CollectChildren("Spawns/Monsters", monsterSpawns);
        CollectChildren("Spawns/Chests", chestSpawns);

        var special = transform.Find("Spawns/Special");
        if (special != null)
        {
            foreach (Transform child in special)
            {
                switch (child.name)
                {
                    case "PlayerSpawn": PlayerSpawn = child; break;
                    case "GuardianSpawn": GuardianSpawn = child; break;
                    case "BossSpawn": BossSpawn = child; break;
                }
            }
        }
    }

    private void CollectChildren(string path, List<Transform> into)
    {
        var root = transform.Find(path);
        if (root == null) return;
        foreach (Transform child in root) into.Add(child);
    }
}
