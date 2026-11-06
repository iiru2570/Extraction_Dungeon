using System;
using System.Collections.Generic;
using UnityEngine;

// 드롭 테이블 (ScriptableObject). Project 창에서 우클릭 > Create > Extraction > Loot Table 로 만듭니다.
//
// 굴리는 방식 (LootContainer 가 "사망 시점" 또는 "상자 생성 시점"에 한 번 호출)
//  1) Extra Drops: 항목마다 독립적으로 확률(0~1) 판정. 균열석 / 확정 드롭(확률 1)에 사용
//  2) Rolls: minRolls ~ maxRolls 번 굴림. 매번 Entries 의 가중치(weight)와 Nothing Weight 로 한 개 선택
//     (Nothing Weight 가 크면 "아무것도 안 나옴" 확률이 올라감)
//  3) 최대 maxItems 개까지만 (루팅 창 칸 수). Extra Drops 가 먼저 들어가므로 잘리지 않음
//
// 등급별 확률 = 등급이 높은 아이템의 weight 를 작게 주는 것으로 조절합니다.
[CreateAssetMenu(fileName = "Loot_New", menuName = "Extraction/Loot Table")]
public class LootTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ItemData item;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public class ExtraDrop
    {
        public ItemData item;
        [Range(0f, 1f)] public float chance = 0.1f;
    }

    [Header("굴리는 횟수")]
    [SerializeField, Min(0)] private int minRolls = 0;
    [SerializeField, Min(0)] private int maxRolls = 2;

    [Header("가중치 후보")]
    [Tooltip("이 값이 클수록 한 번 굴렸을 때 '아무것도 안 나옴' 확률이 올라갑니다. (예: 후보 weight 합 10, Nothing 5 → 33%)")]
    [SerializeField, Min(0f)] private float nothingWeight = 0f;
    [SerializeField] private List<Entry> entries = new List<Entry>();
    [Tooltip("끄면 한 번의 드롭에서 같은 아이템이 두 번 나오지 않습니다.")]
    [SerializeField] private bool allowDuplicates = true;

    [Header("독립 확률 드롭 (균열석 / 확정 드롭)")]
    [SerializeField] private List<ExtraDrop> extraDrops = new List<ExtraDrop>();

    private void OnValidate()
    {
        if (maxRolls < minRolls) maxRolls = minRolls;
    }

    public List<ItemData> Roll(int maxItems)
    {
        var result = new List<ItemData>();

        // 1) 독립 확률 드롭
        foreach (var e in extraDrops)
        {
            if (e == null || e.item == null) continue;
            if (result.Count >= maxItems) break;
            if (UnityEngine.Random.value <= e.chance) result.Add(e.item);
        }

        // 2) 가중치 굴림
        int rolls = UnityEngine.Random.Range(minRolls, maxRolls + 1);
        for (int r = 0; r < rolls && result.Count < maxItems; r++)
        {
            ItemData picked = PickWeighted(result);
            if (picked != null) result.Add(picked);
        }
        return result;
    }

    private ItemData PickWeighted(List<ItemData> already)
    {
        float total = nothingWeight;
        foreach (var e in entries)
        {
            if (!IsCandidate(e, already)) continue;
            total += e.weight;
        }
        if (total <= 0f) return null;

        float v = UnityEngine.Random.Range(0f, total);
        foreach (var e in entries)
        {
            if (!IsCandidate(e, already)) continue;
            v -= e.weight;
            if (v <= 0f) return e.item;
        }
        return null;   // Nothing 구간
    }

    private bool IsCandidate(Entry e, List<ItemData> already)
    {
        if (e == null || e.item == null || e.weight <= 0f) return false;
        if (!allowDuplicates && already.Contains(e.item)) return false;
        return true;
    }
}
