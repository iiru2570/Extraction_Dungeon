using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 슬롯 한 칸의 UI (프리팹에 붙입니다). InventoryUI 가 생성/갱신/클릭 처리를 담당합니다.
// 프리팹 구조: 루트(Image = 배경/테두리, Raycast Target 켬) > Icon(Image) + Label(TMP)
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image border;       // 루트 Image (등급 색으로 칠함)
    [SerializeField] private Image icon;         // 아이콘 (스프라이트 없으면 숨김)
    [SerializeField] private TMP_Text label;     // 아이템 이름 / 빈 칸일 때 칸 이름
    [SerializeField] private Color emptyColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);

    private string emptyLabel = "";

    public ItemData Item { get; private set; }

    // 가방 칸 번호(0~5) 또는 장비 슬롯 번호(EquipSlot). InventoryUI 가 생성할 때 지정합니다.
    public int Index { get; set; }

    public event Action<InventorySlotUI, PointerEventData.InputButton> Clicked;
    public event Action<InventorySlotUI> HoverEntered;
    public event Action<InventorySlotUI> HoverExited;

    public void SetEmptyLabel(string text) => emptyLabel = text;

    public void SetItem(ItemData item)
    {
        Item = item;
        bool has = item != null;

        if (icon != null)
        {
            bool showIcon = has && item.Icon != null;
            icon.sprite = showIcon ? item.Icon : null;
            icon.enabled = showIcon;
        }
        if (label != null) label.text = has ? item.ItemName : emptyLabel;
        if (border != null) border.color = has ? Color.Lerp(emptyColor, item.GradeColor, 0.55f) : emptyColor;
    }

    public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this, e.button);
    public void OnPointerEnter(PointerEventData e) => HoverEntered?.Invoke(this);
    public void OnPointerExit(PointerEventData e) => HoverExited?.Invoke(this);
}
