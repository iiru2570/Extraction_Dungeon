using System;
using UnityEngine;

public enum EquipSlot { Weapon, Armor, Consumable1, Consumable2 }

// 플레이어 인벤토리: 가방 6칸 + 장비 슬롯 4개(무기 1, 방어구 1, 소모품 2).
// Player 오브젝트에 붙입니다. 아이템은 스택 없이 한 칸에 하나씩입니다.
//
// 다른 스크립트가 쓰는 API
//  - TryAdd(item)              : 가방 빈칸에 추가 (가득 차면 false) → 루팅/픽업에서 사용
//  - TryEquip(index)           : 가방 아이템을 장비 슬롯에 장착 (기존 장비는 그 칸으로 교환)
//  - TryUnequip(slot)          : 장비를 가방으로 해제
//  - Discard(index)            : 가방 아이템 버리기
//  - Clear() / GiveStartingGear() : 사망 시 전부 삭제 후 기본 장비 지급 (Day 6 에서 PlayerDeath 가 호출)
//  - TryRemoveOne(type)        : 가방에서 해당 종류 하나 소모 (Day 10 균열석)
//  - Changed 이벤트            : 내용이 바뀔 때마다 (UI 갱신)
public class Inventory : MonoBehaviour
{
    public const int BackpackSize = 6;

    [Header("기본 장비 (비우면 없음)")]
    [Tooltip("게임 시작/사망 후 무료로 지급되는 무기. 장착 상태로 시작합니다.")]
    [SerializeField] private ItemData startingWeapon;
    [SerializeField] private ItemData startingArmor;

    [Header("테스트용")]
    [Tooltip("시작할 때 가방에 미리 넣어둘 아이템 (UI 테스트용, 평소엔 비워두세요)")]
    [SerializeField] private ItemData[] debugStartItems;
    [SerializeField] private bool logEvents = true;

    private readonly ItemData[] backpack = new ItemData[BackpackSize];
    private readonly ItemData[] equipped = new ItemData[4];

    public event Action Changed;

    public int FreeSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < BackpackSize; i++) if (backpack[i] == null) n++;
            return n;
        }
    }
    public bool HasFreeSlot => FreeSlots > 0;

    private void Awake()
    {
        GiveStartingGear();
        if (debugStartItems != null)
            foreach (var item in debugStartItems)
                if (item != null) TryAdd(item);
    }

    public ItemData GetBackpack(int index)
    {
        return index >= 0 && index < BackpackSize ? backpack[index] : null;
    }

    public ItemData GetEquipped(EquipSlot slot) => equipped[(int)slot];

    // 가방 빈칸에 추가. 가득 차면 false
    public bool TryAdd(ItemData item)
    {
        if (item == null) return false;
        for (int i = 0; i < BackpackSize; i++)
        {
            if (backpack[i] != null) continue;
            backpack[i] = item;
            if (logEvents) Debug.Log($"[Inventory] 획득: {item.ItemName} (가방 {i + 1}번 칸)");
            Changed?.Invoke();
            return true;
        }
        if (logEvents) Debug.Log($"[Inventory] 가방이 가득 차서 {item.ItemName} 을(를) 넣을 수 없습니다.");
        return false;
    }

    // 가방에서 꺼냄 (반환값 = 꺼낸 아이템, 없으면 null)
    public ItemData RemoveFromBackpack(int index)
    {
        if (index < 0 || index >= BackpackSize || backpack[index] == null) return null;
        ItemData item = backpack[index];
        backpack[index] = null;
        Changed?.Invoke();
        return item;
    }

    public bool Discard(int index)
    {
        ItemData item = RemoveFromBackpack(index);
        if (item == null) return false;
        if (logEvents) Debug.Log($"[Inventory] 버림: {item.ItemName}");
        return true;
    }

    // 가방 두 칸의 내용을 맞바꿈 (빈칸이면 이동)
    public bool SwapBackpack(int a, int b)
    {
        if (a < 0 || a >= BackpackSize || b < 0 || b >= BackpackSize || a == b) return false;
        (backpack[a], backpack[b]) = (backpack[b], backpack[a]);
        Changed?.Invoke();
        return true;
    }

    // 가방의 아이템을 알맞은 장비 슬롯에 장착. 이미 장착 중인 아이템은 그 가방 칸으로 교환됩니다.
    public bool TryEquip(int backpackIndex)
    {
        ItemData item = GetBackpack(backpackIndex);
        if (item == null) return false;

        EquipSlot slot;
        switch (item.ItemType)
        {
            case ItemType.Weapon: slot = EquipSlot.Weapon; break;
            case ItemType.Armor: slot = EquipSlot.Armor; break;
            case ItemType.Consumable:
                if (equipped[(int)EquipSlot.Consumable1] == null) slot = EquipSlot.Consumable1;
                else if (equipped[(int)EquipSlot.Consumable2] == null) slot = EquipSlot.Consumable2;
                else slot = EquipSlot.Consumable1;   // 둘 다 차 있으면 1번과 교환
                break;
            default:
                return false;   // 균열석 등은 장착 불가
        }

        ItemData old = equipped[(int)slot];
        equipped[(int)slot] = item;
        backpack[backpackIndex] = old;   // 이전 장비(없으면 null)가 그 칸으로
        if (logEvents) Debug.Log($"[Inventory] 장착: {item.ItemName} → {slot}");
        Changed?.Invoke();
        return true;
    }

    // 장비를 해제해서 가방 빈칸으로. 가방이 가득 차면 false
    public bool TryUnequip(EquipSlot slot)
    {
        ItemData item = equipped[(int)slot];
        if (item == null) return false;

        for (int i = 0; i < BackpackSize; i++)
        {
            if (backpack[i] != null) continue;
            backpack[i] = item;
            equipped[(int)slot] = null;
            if (logEvents) Debug.Log($"[Inventory] 해제: {item.ItemName}");
            Changed?.Invoke();
            return true;
        }
        if (logEvents) Debug.Log("[Inventory] 가방이 가득 차서 장비를 해제할 수 없습니다.");
        return false;
    }

    // 가방에서 해당 종류의 아이템을 하나 소모 (균열석 등)
    public bool TryRemoveOne(ItemType type)
    {
        for (int i = 0; i < BackpackSize; i++)
        {
            if (backpack[i] != null && backpack[i].ItemType == type)
            {
                backpack[i] = null;
                Changed?.Invoke();
                return true;
            }
        }
        return false;
    }

    // 장착 슬롯의 아이템을 하나 소모 (붕대 사용 등). 반환 = 소모한 아이템, 없으면 null
    public ItemData ConsumeEquipped(EquipSlot slot)
    {
        ItemData item = equipped[(int)slot];
        if (item == null) return null;
        equipped[(int)slot] = null;
        if (logEvents) Debug.Log($"[Inventory] 사용: {item.ItemName} ({slot})");
        Changed?.Invoke();
        return item;
    }

    public bool HasItemOfType(ItemType type)
    {
        for (int i = 0; i < BackpackSize; i++)
            if (backpack[i] != null && backpack[i].ItemType == type) return true;
        return false;
    }

    // 가방 + 장비 전부 삭제 (사망/붕괴 시)
    public void Clear()
    {
        for (int i = 0; i < BackpackSize; i++) backpack[i] = null;
        for (int i = 0; i < equipped.Length; i++) equipped[i] = null;
        Changed?.Invoke();
    }

    // 기본 장비를 장착 상태로 지급 (이미 장착한 슬롯은 덮어쓰지 않음)
    public void GiveStartingGear()
    {
        if (startingWeapon != null && equipped[(int)EquipSlot.Weapon] == null)
            equipped[(int)EquipSlot.Weapon] = startingWeapon;
        if (startingArmor != null && equipped[(int)EquipSlot.Armor] == null)
            equipped[(int)EquipSlot.Armor] = startingArmor;
        Changed?.Invoke();
    }
}