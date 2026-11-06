using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 루팅 창. 시체/상자의 아이템 목록을 보여주고, 클릭하면 플레이어 인벤토리로 옮깁니다.
// 이 스크립트는 "항상 켜져 있는" 오브젝트(LootUI)에 붙이고, 실제로 켜고 끄는 대상은 Panel 입니다.
// (InventoryUI 와 같은 방식)
//
// 열릴 때: 인벤토리 창도 함께 열고(InventoryUI.Open), UIState 에 등록 -> 커서 해제 / 입력 차단
// 닫힐 때(아래 중 하나): 닫기 버튼 / Tab 또는 ESC (인벤토리가 닫히면 같이 닫힘)
//                        / 시체에서 closeRange 이상 멀어짐 / 대상이 사라짐 / 플레이어 사망
//
// 조작: 칸 좌클릭 또는 우클릭 = 인벤토리로 옮기기 (가방이 가득 차면 안내 문구, 아이템은 그대로 남음)
//       "모두 가져오기" = 위에서부터 차례로 옮기고, 가방이 가득 차면 멈춤
public class LootWindowUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;             // 켜고 끌 창 (자식 오브젝트)
    [SerializeField] private InventorySlotUI slotPrefab;   // Day 5 의 SlotUI 프리팹 재사용
    [SerializeField] private Transform slotRoot;           // 슬롯이 생성될 부모 (Grid Layout Group)
    [SerializeField] private TMP_Text titleText;           // "시체" / "상자" (선택)
    [SerializeField] private TMP_Text infoText;            // 설명 / 안내 (선택)
    [SerializeField] private Button takeAllButton;         // 모두 가져오기 (선택)
    [SerializeField] private Button closeButton;           // 닫기 (선택)
    [SerializeField] private InventoryUI inventoryUI;      // 비우면 자동 검색
    [Tooltip("플레이어가 대상에서 이 거리(m)보다 멀어지면 창을 닫음. Interactor 의 Break Range(3.5)보다 크게")]
    [SerializeField] private float closeRange = 4f;

    private InventorySlotUI[] slots;
    private LootContainer container;
    private Inventory inventory;
    private Health playerHealth;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (panel == null || slotPrefab == null || slotRoot == null)
        {
            Debug.LogError("[LootWindowUI] Panel / Slot Prefab / Slot Root 를 모두 연결해주세요.");
            enabled = false;
            return;
        }
        if (inventoryUI == null) inventoryUI = FindFirstObjectByType<InventoryUI>();

        slots = new InventorySlotUI[LootContainer.MaxItems];
        for (int i = 0; i < slots.Length; i++)
        {
            var s = Instantiate(slotPrefab, slotRoot);
            s.name = $"LootSlot_{i + 1}";
            s.Index = i;
            s.SetEmptyLabel("");
            s.Clicked += OnSlotClicked;
            s.HoverEntered += OnHoverEnter;
            s.HoverExited += OnHoverExit;
            slots[i] = s;
        }

        if (takeAllButton != null) takeAllButton.onClick.AddListener(TakeAll);
        if (closeButton != null) closeButton.onClick.AddListener(() => Close());

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (container != null) container.Changed -= Refresh;
        UIState.Close(this, false);
    }

    // LootContainer.OnInteracted 가 호출
    public void Show(LootContainer target, Inventory playerInventory)
    {
        if (!enabled || target == null) return;
        if (IsOpen) Close();

        container = target;
        inventory = playerInventory != null ? playerInventory : FindFirstObjectByType<Inventory>();
        playerHealth = inventory != null ? inventory.GetComponent<Health>() : null;
        if (inventory == null)
        {
            Debug.LogError("[LootWindowUI] Inventory 를 찾지 못했습니다.");
            container = null;
            return;
        }

        container.Changed += Refresh;
        container.SetViewed(true);

        IsOpen = true;
        panel.SetActive(true);
        if (inventoryUI != null) inventoryUI.Open();   // 인벤토리를 함께 표시
        UIState.Open(this);
        Refresh();
    }

    public void Close(bool restoreCursor = true)
    {
        if (!IsOpen) return;
        IsOpen = false;
        panel.SetActive(false);

        if (container != null)
        {
            container.Changed -= Refresh;
            container.SetViewed(false);
        }
        container = null;

        // 인벤토리도 같이 닫음 (이미 닫혀 있으면 아무 일 없음)
        if (inventoryUI != null && inventoryUI.IsOpen) inventoryUI.Close(restoreCursor);
        UIState.Close(this, restoreCursor);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 사망: 커서는 게임오버 화면이 쓰므로 건드리지 않음 (반드시 가장 먼저 검사)
        if (playerHealth != null && playerHealth.IsDead) { Close(false); return; }

        // 대상이 사라짐 (시체 제거 등)
        if (container == null) { Close(); return; }

        // Tab / ESC 로 인벤토리가 닫혔으면 함께 닫음
        if (inventoryUI != null && !inventoryUI.IsOpen) { Close(); return; }

        // 거리 이탈 (몬스터에게 밀려 멀어졌을 때 등)
        if (inventory != null)
        {
            float d = Vector3.Distance(inventory.transform.position, container.InteractPosition);
            if (d > closeRange) Close();
        }
    }

    private void Refresh()
    {
        if (container == null) return;
        if (titleText != null) titleText.text = container.Title;
        for (int i = 0; i < slots.Length; i++)
            slots[i].SetItem(container.GetItem(i));
        if (infoText != null) infoText.text = container.IsEmpty ? "비어 있습니다." : "";
    }

    // ---- 옮기기 ----

    private void OnSlotClicked(InventorySlotUI slot, PointerEventData.InputButton button)
    {
        if (button != PointerEventData.InputButton.Left && button != PointerEventData.InputButton.Right) return;
        if (slot.Item == null) return;
        if (!TryTake(slot.Index) && infoText != null) infoText.text = "가방이 가득 찼습니다.";
    }

    private bool TryTake(int index)
    {
        if (container == null || inventory == null) return false;
        ItemData item = container.GetItem(index);
        if (item == null) return false;
        if (!inventory.TryAdd(item)) return false;   // 가득 차면 시체에 그대로 남음
        container.RemoveAt(index);                    // Changed -> Refresh
        return true;
    }

    private void TakeAll()
    {
        if (container == null) return;
        bool full = false;
        while (container != null && container.Count > 0)
        {
            if (!TryTake(0)) { full = true; break; }
        }
        if (full && infoText != null) infoText.text = "가방이 가득 차서 일부 아이템이 남았습니다.";
    }

    // ---- 설명 ----

    private void OnHoverEnter(InventorySlotUI slot)
    {
        if (infoText == null) return;
        infoText.text = slot.Item != null ? slot.Item.BuildDescription() : "";
    }

    private void OnHoverExit(InventorySlotUI slot)
    {
        if (infoText == null) return;
        infoText.text = container != null && container.IsEmpty ? "비어 있습니다." : "";
    }
}
