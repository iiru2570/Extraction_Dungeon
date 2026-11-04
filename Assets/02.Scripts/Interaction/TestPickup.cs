using UnityEngine;

// Day 5 테스트 전용: E 를 일정 시간 누르면 아이템이 인벤토리에 들어가는 바닥 아이템.
// 임시 큐브에 붙여서 상호작용/인벤토리를 확인합니다. Day 6 의 LootContainer(시체/상자)로 대체됩니다.
// 콜라이더가 "Interactable" 레이어에 있어야 합니다.
public class TestPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemData item;
    [SerializeField] private string prompt = "줍기";
    [Tooltip("E 를 누르고 있어야 하는 시간(초)")]
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private bool destroyOnPickup = true;

    public string Prompt => item != null ? $"{item.ItemName} {prompt}" : prompt;
    public float InteractDuration => duration;
    public Vector3 InteractPosition => transform.position;

    public bool CanInteract(Interactor who) => item != null;

    public void OnInteracted(Interactor who)
    {
        var inventory = who.GetComponent<Inventory>();
        if (inventory == null) return;

        // 가방이 가득 차면 실패 → 물건은 그대로 남음 (시체 루팅도 같은 규칙)
        if (inventory.TryAdd(item) && destroyOnPickup)
            Destroy(gameObject);
    }
}
