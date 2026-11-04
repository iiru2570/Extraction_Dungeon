using System.Text;
using UnityEngine;

public enum ItemGrade { Common, Rare, Legendary }          // 일반 / 희귀 / 전설
public enum ItemType { Weapon, Armor, Consumable, RiftStone } // 무기 / 방어구 / 소모품 / 균열석

// 아이템 데이터 (ScriptableObject). Project 창에서 우클릭 > Create > Extraction > Item Data 로 만듭니다.
// 수치의 실제 반영(공격력, 방어력, 이동속도, 회복)은 Day 7 에서 연결합니다. 지금은 값만 보관합니다.
[CreateAssetMenu(fileName = "Item_New", menuName = "Extraction/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("공통")]
    [SerializeField] private string itemName = "새 아이템";
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemGrade grade = ItemGrade.Common;
    [SerializeField] private ItemType itemType = ItemType.Weapon;
    [SerializeField, TextArea] private string description;

    [Header("무기 (Weapon)")]
    [Tooltip("무기 공격력 (Day 7 에서 PlayerCombat 피해량에 반영)")]
    [SerializeField] private float attackPower = 10f;

    [Header("방어구 (Armor)")]
    [Tooltip("방어력 (Day 7 에서 받는 피해 감소에 반영)")]
    [SerializeField] private float defense = 0f;
    [Tooltip("이동속도 배율. 1 = 변화 없음, 0.9 = 10% 느려짐 (방어구 트레이드오프)")]
    [SerializeField] private float moveSpeedMultiplier = 1f;

    [Header("소모품 (Consumable)")]
    [Tooltip("사용 시 회복량 (Day 7 ConsumableUse 에서 사용)")]
    [SerializeField] private float healAmount = 30f;

    public string ItemName => itemName;
    public Sprite Icon => icon;
    public ItemGrade Grade => grade;
    public ItemType ItemType => itemType;
    public string Description => description;
    public float AttackPower => attackPower;
    public float Defense => defense;
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float HealAmount => healAmount;

    public Color GradeColor
    {
        get
        {
            switch (grade)
            {
                case ItemGrade.Rare: return new Color(0.35f, 0.65f, 1f);
                case ItemGrade.Legendary: return new Color(1f, 0.65f, 0.1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }

    public string GradeLabel
    {
        get
        {
            switch (grade)
            {
                case ItemGrade.Rare: return "희귀";
                case ItemGrade.Legendary: return "전설";
                default: return "일반";
            }
        }
    }

    public string TypeLabel
    {
        get
        {
            switch (itemType)
            {
                case ItemType.Weapon: return "무기";
                case ItemType.Armor: return "방어구";
                case ItemType.Consumable: return "소모품";
                default: return "균열석";
            }
        }
    }

    // 툴팁/정보창용 설명 문장 (TextMeshPro 리치 텍스트 사용)
    public string BuildDescription()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(GradeColor)}>{itemName}</color>  ({GradeLabel} {TypeLabel})");
        switch (itemType)
        {
            case ItemType.Weapon:
                sb.AppendLine($"공격력 {attackPower:0.#}");
                break;
            case ItemType.Armor:
                sb.AppendLine($"방어력 {defense:0.#}");
                if (!Mathf.Approximately(moveSpeedMultiplier, 1f))
                    sb.AppendLine($"이동속도 {(moveSpeedMultiplier - 1f) * 100f:+0.#;-0.#}%");
                break;
            case ItemType.Consumable:
                sb.AppendLine($"체력 {healAmount:0.#} 회복");
                break;
        }
        if (!string.IsNullOrEmpty(description)) sb.AppendLine(description);
        return sb.ToString().TrimEnd();
    }
}
