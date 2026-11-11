using UnityEngine;

// 장비 수치 반영 (Day 7). Player 루트에 붙입니다.
// Inventory.Changed 를 구독해서 장착 무기/방어구의 수치를 계산하고 다른 컴포넌트에 반영합니다.
//  - 무기 AttackPower  → PlayerCombat.DamageMultiplier = AttackPower / Base Attack Power
//  - 방어구 MoveSpeedMultiplier → PlayerController.MoveSpeedMultiplier
//  - 방어구 Defense    → 이 컴포넌트의 Defense / DamageTakenMultiplier 로 노출
//                        + IDamageInterceptor 로 Health 가 받는 피해에 방어력을 곱함 (Health.Awake 에서 자동 수집)
public class EquipmentStats : MonoBehaviour, IDamageInterceptor
{
    [Tooltip("이 공격력을 '기본(배율 1)' 으로 봅니다. PlayerCombat 의 Slash 피해량(10)이 이 공격력 기준입니다. 기본 철검 = 10 이면 10 으로 두세요.")]
    [SerializeField, Min(1f)] private float baseAttackPower = 10f;
    [Tooltip("받는 피해 계산: 피해 × 100 / (100 + 방어력). 방어력 25 = 20% 감소")]
    [SerializeField, Min(1f)] private float defenseScale = 100f;
    [SerializeField] private bool logEvents = true;

    private Inventory inventory;
    private PlayerCombat combat;
    private PlayerController controller;

    public float AttackPower { get; private set; }
    public float Defense { get; private set; }
    public float MoveSpeedMultiplier { get; private set; } = 1f;
    public float DamageMultiplier { get; private set; } = 1f;
    /// <summary>받는 피해에 곱할 값 (1 = 변화 없음, 0.8 = 20% 감소).</summary>
    public float DamageTakenMultiplier => defenseScale / (defenseScale + Defense);

    // Health.TakeDamage 가 피해 적용 직전에 호출. 방어력만큼 피해를 줄입니다.
    // PlayerParry 의 가드(×0.5)와는 곱해지므로 컴포넌트 순서와 상관없이 결과가 같고,
    // 패링 성공(피해 무효)은 PlayerParry 가 false 를 반환해서 그대로 유지됩니다.
    public bool Intercept(ref DamageInfo info)
    {
        if (Defense > 0f) info.amount *= DamageTakenMultiplier;
        return true;
    }

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        combat = GetComponent<PlayerCombat>();
        controller = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Refresh;
    }

    private void Start()
    {
        Refresh();   // Inventory.Awake 에서 기본 장비를 지급한 뒤라서 시작 시 한 번 반영
    }

    private void Refresh()
    {
        if (inventory == null) return;

        ItemData weapon = inventory.GetEquipped(EquipSlot.Weapon);
        ItemData armor = inventory.GetEquipped(EquipSlot.Armor);

        AttackPower = weapon != null ? weapon.AttackPower : 0f;
        // 무기가 없으면 맨손 취급 (배율 0.5)
        DamageMultiplier = weapon != null ? weapon.AttackPower / baseAttackPower : 0.5f;
        Defense = armor != null ? armor.Defense : 0f;
        MoveSpeedMultiplier = armor != null ? armor.MoveSpeedMultiplier : 1f;

        if (combat != null) combat.DamageMultiplier = DamageMultiplier;
        if (controller != null) controller.MoveSpeedMultiplier = MoveSpeedMultiplier;

        if (logEvents)
            Debug.Log($"[Equip] 공격 배율 ×{DamageMultiplier:0.##} (공격력 {AttackPower:0.#}) | 방어력 {Defense:0.#} (받는 피해 ×{DamageTakenMultiplier:0.##}) | 이동속도 ×{MoveSpeedMultiplier:0.##}");
    }
}