using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 방 종류별 스폰 포인트에 몬스터/상자를 채웁니다 (Day 7). 검증용 Test Monster Prefab 을 대체합니다.
// DungeonGenerator.DungeonBuilt 이벤트(방 배치 + NavMesh 빌드 + 플레이어 배치 완료 후)에서 실행됩니다.
//
//  입구방  : 아무것도 생성하지 않음
//  일반방  : MonsterSpawn 마다 확률로 일반 몬스터 (풀에서 가중치 선택), ChestSpawn 마다 확률로 일반 상자
//  보물방  : ChestSpawn 마다 확률로 고급 상자, GuardianSpawn 에 정예 수호자
//  보스방  : 아무것도 생성하지 않음 (Day 8~9 에서 보스 연결)
//
// 생성물은 "SpawnedEntities" 오브젝트 아래에 모읍니다 (방 계층 밖: 몬스터/상자 콜라이더가 NavMesh 에 영향 없게).
// 같은 던전 시드면 같은 구성이 나옵니다.
public class RoomSpawner : MonoBehaviour
{
    [Serializable]
    public class MonsterEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField] private DungeonGenerator generator;   // 비우면 자동 검색

    [Header("일반방 - 몬스터")]
    [SerializeField] private List<MonsterEntry> normalMonsterPool = new List<MonsterEntry>();
    [Tooltip("MonsterSpawn 한 곳에 몬스터가 나올 확률")]
    [SerializeField, Range(0f, 1f)] private float monsterChance = 0.7f;

    [Header("일반방 - 상자")]
    [SerializeField] private GameObject normalChestPrefab;
    [SerializeField, Range(0f, 1f)] private float normalChestChance = 0.4f;

    [Header("보물방")]
    [SerializeField] private GameObject rareChestPrefab;
    [Tooltip("ChestSpawn 한 곳에 고급 상자가 나올 확률 (보물방 ChestSpawn 3개 → 2~3개)")]
    [SerializeField, Range(0f, 1f)] private float rareChestChance = 0.85f;
    [SerializeField] private GameObject eliteGuardianPrefab;

    [Header("공통")]
    [Tooltip("상자는 피벗이 중심이라 바닥에서 이만큼 띄워 배치 (Chest 큐브 높이 0.8 → 0.4)")]
    [SerializeField] private float chestYOffset = 0.4f;
    [Tooltip("몬스터는 스폰 포인트에서 이 거리 안의 NavMesh 위로 보정")]
    [SerializeField] private float navSampleDistance = 2f;
    [SerializeField] private bool logEvents = true;

    private Transform spawnedParent;

    private void Awake()
    {
        if (generator == null) generator = FindFirstObjectByType<DungeonGenerator>();
        if (generator == null)
            Debug.LogError("[RoomSpawner] DungeonGenerator 를 찾지 못했습니다.", this);
    }

    private void OnEnable()
    {
        if (generator != null) generator.DungeonBuilt += Populate;
    }

    private void OnDisable()
    {
        if (generator != null) generator.DungeonBuilt -= Populate;
    }

    private void Populate()
    {
        // 이전 던전 생성물 제거
        if (spawnedParent != null)
        {
            spawnedParent.gameObject.SetActive(false);
            Destroy(spawnedParent.gameObject);
        }
        spawnedParent = new GameObject("SpawnedEntities").transform;

        var rng = new System.Random(unchecked(generator.LastSeed * 13 + 5));
        int monsters = 0, elites = 0, normalChests = 0, rareChests = 0, failed = 0;

        foreach (var data in generator.Rooms)
        {
            Room room = generator.GetSpawnedRoom(data.Position);
            if (room == null) continue;

            switch (data.Type)
            {
                case RoomType.Normal:
                    foreach (var sp in room.MonsterSpawns)
                    {
                        if (rng.NextDouble() > monsterChance) continue;
                        GameObject prefab = PickMonster(rng);
                        if (prefab == null) continue;
                        if (SpawnOnNavMesh(prefab, sp)) monsters++; else failed++;
                    }
                    foreach (var sp in room.ChestSpawns)
                    {
                        if (rng.NextDouble() > normalChestChance) continue;
                        if (SpawnChest(normalChestPrefab, sp)) normalChests++;
                    }
                    break;

                case RoomType.Treasure:
                    foreach (var sp in room.ChestSpawns)
                    {
                        if (rng.NextDouble() > rareChestChance) continue;
                        if (SpawnChest(rareChestPrefab, sp)) rareChests++;
                    }
                    if (room.GuardianSpawn != null && eliteGuardianPrefab != null)
                    {
                        if (SpawnOnNavMesh(eliteGuardianPrefab, room.GuardianSpawn)) elites++; else failed++;
                    }
                    break;
            }
        }

        if (logEvents)
            Debug.Log($"[RoomSpawner] 몬스터 {monsters} / 정예 {elites} / 일반 상자 {normalChests} / 고급 상자 {rareChests}" +
                      (failed > 0 ? $" (NavMesh 없음으로 실패 {failed})" : ""));
    }

    private GameObject PickMonster(System.Random rng)
    {
        float total = 0f;
        foreach (var e in normalMonsterPool)
            if (e != null && e.prefab != null && e.weight > 0f) total += e.weight;
        if (total <= 0f) return null;

        double v = rng.NextDouble() * total;
        foreach (var e in normalMonsterPool)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            v -= e.weight;
            if (v <= 0.0) return e.prefab;
        }
        return null;
    }

    private bool SpawnOnNavMesh(GameObject prefab, Transform sp)
    {
        if (NavMesh.SamplePosition(sp.position, out var hit, navSampleDistance, NavMesh.AllAreas))
        {
            Instantiate(prefab, hit.position, Quaternion.Euler(0f, sp.eulerAngles.y, 0f), spawnedParent);
            return true;
        }
        Debug.LogWarning($"[RoomSpawner] {sp.parent?.parent?.name}/{sp.name} 근처에 NavMesh 가 없어 {prefab.name} 을(를) 만들지 못했습니다.", sp);
        return false;
    }

    private bool SpawnChest(GameObject prefab, Transform sp)
    {
        if (prefab == null) return false;
        Instantiate(prefab, sp.position + Vector3.up * chestYOffset,
                    Quaternion.Euler(0f, sp.eulerAngles.y, 0f), spawnedParent);
        return true;
    }
}
