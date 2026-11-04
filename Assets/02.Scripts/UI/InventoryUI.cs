using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 인벤토리 창. Tab 으로 열고 닫고(ESC 로도 닫힘), 열려 있는 동안 UIState 로 커서/입력을 제어합니다.
// 이 스크립트는 "항상 켜져 있는" 오브젝트에 붙이고, 실제로 켜고 끄는 대상은 Panel 입니다.
//
// 조작: 가방 아이템 좌클릭 = 장착(장비칸과 교환) / 우클릭 = 버리기
//       장비칸 좌클릭 = 해제(가방으로)  / 칸 위에 마우스 = 설명 표시
// Day 6 의 루팅 창이 Open()/Close() 를 호출해서 인벤토리를 함께 열 수 있습니다.
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;       // 비우면 자동 검색
    [SerializeField] private GameObject panel;           // 켜고 끌 창 (자식 오브젝트)
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Transform backpackRoot;     // 가방 슬롯 6개가 생성될 부모 (Grid Layout Group)
    [SerializeField] private Transform equipRoot;        // 장비 슬롯 4개가 생성될 부모 (Horizontal Layout Group)
    [SerializeField] private TMP_Text infoText;          // 설명 표시 (선택)
    [SerializeField] private Key toggleKey = Key.Tab;

    private static readonly string[] EquipLabels = { "무기", "방어구", "소모품 1", "소모품 2" };

    private InventorySlotUI[] backpackSlots;
    private InventorySlotUI[] equipSlots;
    private Health playerHealth;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null)
        {
            Debug.LogError("[InventoryUI] Inventory 를 찾지 못했습니다. Player 에 Inventory 를 붙였는지 확인하세요.");
            enabled = false;
            return;
        }
        playerHealth = inventory.GetComponent<Health>();

        if (panel == null || slotPrefab == null || backpackRoot == null || equipRoot == null)
        {
            Debug.LogError("[InventoryUI] Panel / Slot Prefab / Backpack Root / Equip Root 를 모두 연결해주세요.");
            enabled = false;
            return;
        }

        BuildSlots();
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.Changed += OnInventoryChanged;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= OnInventoryChanged;
    }

    private void OnDestroy()
    {
        // 씬 재로드 등으로 열린 채 사라져도 UIState 가 남지 않게 정리
        UIState.Close(this, false);
    }

    private void BuildSlots()
    {
        backpackSlots = new InventorySlotUI[Inventory.BackpackSize];
        for (int i = 0; i < backpackSlots.Length; i++)
        {
            var s = Instantiate(slotPrefab, backpackRoot);
            s.name = $"BackpackSlot_{i + 1}";
            s.Index = i;
            s.SetEmptyLabel("");
            Hook(s, OnBackpackClicked);
            backpackSlots[i] = s;
        }

        equipSlots = new InventorySlotUI[EquipLabels.Length];
        for (int i = 0; i < equipSlots.Length; i++)
        {
            var s = Instantiate(slotPrefab, equipRoot);
            s.name = $"EquipSlot_{EquipLabels[i]}";
            s.Index = i;
            s.SetEmptyLabel(EquipLabels[i]);
            Hook(s, OnEquipClicked);
            equipSlots[i] = s;
        }
    }

    private void Hook(InventorySlotUI s, System.Action<InventorySlotUI, PointerEventData.InputButton> click)
    {
        s.Clicked += click;
        s.HoverEntered += OnHoverEnter;
        s.HoverExited += OnHoverExit;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 사망하면 닫음 (커서는 게임오버 화면이 쓰므로 건드리지 않음)
        if (IsOpen && playerHealth != null && playerHealth.IsDead)
        {
            Close(false);
            return;
        }

        if (kb[toggleKey].wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
        }
        else if (IsOpen && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void Open()
    {
        if (IsOpen) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        IsOpen = true;
        panel.SetActive(true);
        Refresh();
        UIState.Open(this);
    }

    public void Close(bool restoreCursor = true)
    {
        if (!IsOpen) return;
        IsOpen = false;
        panel.SetActive(false);
        UIState.Close(this, restoreCursor);
    }

    private void OnInventoryChanged()
    {
        if (IsOpen) Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < backpackSlots.Length; i++)
            backpackSlots[i].SetItem(inventory.GetBackpack(i));
        for (int i = 0; i < equipSlots.Length; i++)
            equipSlots[i].SetItem(inventory.GetEquipped((EquipSlot)i));
        if (infoText != null) infoText.text = "";
    }

    // ---- 클릭 ----

    private void OnBackpackClicked(InventorySlotUI slot, PointerEventData.InputButton button)
    {
        if (slot.Item == null) return;
        if (button == PointerEventData.InputButton.Left) inventory.TryEquip(slot.Index);
        else if (button == PointerEventData.InputButton.Right) inventory.Discard(slot.Index);
    }

    private void OnEquipClicked(InventorySlotUI slot, PointerEventData.InputButton button)
    {
        if (slot.Item == null) return;
        if (button == PointerEventData.InputButton.Left) inventory.TryUnequip((EquipSlot)slot.Index);
    }

    // ---- 설명 ----

    private void OnHoverEnter(InventorySlotUI slot)
    {
        if (infoText == null) return;
        infoText.text = slot.Item != null ? slot.Item.BuildDescription() : "";
    }

    private void OnHoverExit(InventorySlotUI slot)
    {
        if (infoText != null) infoText.text = "";
    }
}
