using UnityEngine;
using UnityEngine.InputSystem;

// 소모품 사용 (Day 7). 1 키 = 소모품 슬롯 1, 2 키 = 소모품 슬롯 2.
// Player 루트에 붙입니다 (Health, Inventory 가 같은 오브젝트에 있어야 함).
// 입력은 Input Action 없이 Keyboard.current 를 직접 읽습니다 (E / Tab 과 같은 방식).
//
// 사용 조건: UI 가 닫혀 있고, 살아 있고, 체력이 가득 차지 않았고, 쿨다운이 지났을 때
// (체력이 가득이면 붕대를 낭비하지 않도록 사용되지 않음)
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Inventory))]
public class ConsumableUse : MonoBehaviour
{
    [SerializeField] private Key slot1Key = Key.Digit1;
    [SerializeField] private Key slot2Key = Key.Digit2;
    [Tooltip("한 번 사용한 뒤 다음 사용까지 대기 시간(초)")]
    [SerializeField, Min(0f)] private float cooldown = 1f;
    [Tooltip("체력이 가득일 때도 사용 가능하게 함 (보통 끔)")]
    [SerializeField] private bool allowWhenFull = false;
    [SerializeField] private bool logEvents = true;

    private Health health;
    private Inventory inventory;
    private float nextUseTime;

    private void Awake()
    {
        health = GetComponent<Health>();
        inventory = GetComponent<Inventory>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (UIState.IsOpen) return;
        if (health.Current <= 0f) return;

        if (kb[slot1Key].wasPressedThisFrame) TryUse(EquipSlot.Consumable1);
        else if (kb[slot2Key].wasPressedThisFrame) TryUse(EquipSlot.Consumable2);
    }

    private void TryUse(EquipSlot slot)
    {
        if (Time.time < nextUseTime) return;

        ItemData item = inventory.GetEquipped(slot);
        if (item == null || item.ItemType != ItemType.Consumable) return;

        if (!allowWhenFull && health.Current >= health.Max)
        {
            if (logEvents) Debug.Log("[Consumable] 체력이 가득 차서 사용하지 않았습니다.");
            return;
        }

        inventory.ConsumeEquipped(slot);
        health.Heal(item.HealAmount);
        nextUseTime = Time.time + cooldown;

        if (logEvents) Debug.Log($"[Consumable] {item.ItemName} 사용: 체력 +{item.HealAmount:0.#} → {health.Current:0.#}/{health.Max:0.#}");
    }
}
