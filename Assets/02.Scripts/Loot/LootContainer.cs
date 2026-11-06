using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 시체 / 상자 공통 루팅 컴포넌트. IInteractable 구현 (Day 5 의 Interactor 가 E 홀드 게이지를 처리).
//
// [시체 사용법]  몬스터 프리팹 루트에 붙입니다 (Health 가 같은 오브젝트에 있어야 함)
//   - Roll On Death 켬: Health.Died 순간에 드롭을 "미리 굴려서" 보관. 그 전에는 상호작용 불가
//   - 몬스터의 EnemyBase > Destroy On Death 는 반드시 끄세요 (시체 제거는 이 컴포넌트가 담당)
//   - 자식에 "LootTrigger" 오브젝트 (Interactable 레이어 + Sphere Collider Is Trigger) 필요
// [상자 사용법]  Chest 컴포넌트(이 클래스를 상속)를 쓰면 기본값이 상자용으로 채워집니다
//
// 시체 제거 규칙
//   - 드롭이 하나도 없으면 emptyCorpseLinger 초 뒤 제거
//   - 내용물을 다 가져가면 emptyDestroyDelay 초 뒤 제거 (Destroy When Empty 켠 경우)
//   - 아무도 안 열어도 corpseLifetime 초 뒤 제거 (0 이면 계속 유지)
//   - 루팅 창이 열려 있는 동안에는 제거하지 않고 닫힐 때까지 기다림
public class LootContainer : MonoBehaviour, IInteractable
{
    public const int MaxItems = 6;   // 루팅 창 칸 수 (LootWindowUI 가 6칸을 만듭니다)

    [Header("내용물")]
    [SerializeField] protected LootTable lootTable;
    [Tooltip("켜면 Health.Died 때 드롭을 굴림 (시체). 끄면 시작할 때 굴림 (상자)")]
    [SerializeField] protected bool rollOnDeath = true;
    [Tooltip("테스트용: 드롭 테이블과 상관없이 항상 추가되는 아이템 (평소엔 비워두세요)")]
    [SerializeField] protected List<ItemData> debugItems = new List<ItemData>();

    [Header("상호작용")]
    [SerializeField] protected string title = "시체";
    [SerializeField] protected string prompt = "시체 뒤지기";
    [Tooltip("E 를 누르고 있어야 하는 시간(초)")]
    [SerializeField] protected float interactDuration = 1.5f;
    [Tooltip("거리 계산 기준점 (비우면 이 오브젝트의 위치)")]
    [SerializeField] protected Transform interactPoint;

    [Header("시체 처리 (Roll On Death 켠 경우)")]
    [Tooltip("사망 시 Trigger 가 아닌 콜라이더를 꺼서 플레이어가 시체를 통과하게 함")]
    [SerializeField] protected bool disableCollidersOnDeath = true;
    [Tooltip("아무도 열지 않았을 때 시체가 남아 있는 시간(초). 0 = 계속 유지")]
    [SerializeField] protected float corpseLifetime = 120f;
    [Tooltip("드롭이 하나도 없을 때 시체가 남아 있는 시간(초)")]
    [SerializeField] protected float emptyCorpseLinger = 3f;

    [Header("비었을 때")]
    [SerializeField] protected bool destroyWhenEmpty = true;
    [SerializeField] protected float emptyDestroyDelay = 1.5f;

    [SerializeField] protected bool logEvents = true;

    private readonly List<ItemData> items = new List<ItemData>();
    private Health health;
    private bool isDead;
    private bool rolled;
    private int viewers;

    // ---- 다른 스크립트(루팅 창 등)가 읽는 값 ----
    public string Title => title;
    public int Count => items.Count;
    public bool IsEmpty => items.Count == 0;
    public bool IsLootable => items.Count > 0 && (!rollOnDeath || isDead);

    // 내용이 바뀔 때마다 (루팅 창 갱신)
    public event Action Changed;

    // ---- IInteractable ----
    public string Prompt => prompt;
    public float InteractDuration => interactDuration;
    public Vector3 InteractPosition => interactPoint != null ? interactPoint.position : transform.position;
    public bool CanInteract(Interactor who) => IsLootable;

    public void OnInteracted(Interactor who)
    {
        var window = FindFirstObjectByType<LootWindowUI>();
        if (window == null)
        {
            Debug.LogError("[LootContainer] LootWindowUI 를 찾지 못했습니다. Canvas 에 LootUI 오브젝트(LootWindowUI)가 켜져 있는지 확인하세요.");
            return;
        }
        Inventory inv = who != null ? who.GetComponent<Inventory>() : null;
        window.Show(this, inv);
    }

    // ================= Unity 메시지 =================

    protected virtual void Awake()
    {
        if (rollOnDeath)
        {
            health = GetComponent<Health>();
            if (health == null)
                Debug.LogWarning($"[{name}] Roll On Death 가 켜져 있지만 Health 가 없습니다. 몬스터 루트에 붙여주세요.");
        }
    }

    protected virtual void OnEnable()
    {
        if (health != null) health.Died += OnDied;
    }

    protected virtual void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
    }

    protected virtual void Start()
    {
        if (!rollOnDeath) RollLoot();   // 상자는 시작할 때 굴림
    }

    // ================= 드롭 =================

    private void OnDied()
    {
        if (isDead) return;
        isDead = true;

        RollLoot();

        if (disableCollidersOnDeath)
        {
            foreach (var c in GetComponentsInChildren<Collider>())
                if (!c.isTrigger) c.enabled = false;   // LootTrigger(Trigger) 는 유지
        }

        if (items.Count == 0) StartCoroutine(DespawnRoutine(emptyCorpseLinger));
        else if (corpseLifetime > 0f) StartCoroutine(DespawnRoutine(corpseLifetime));

        Changed?.Invoke();
    }

    private void RollLoot()
    {
        if (rolled) return;
        rolled = true;

        if (lootTable != null)
        {
            foreach (var it in lootTable.Roll(MaxItems))
                if (it != null && items.Count < MaxItems) items.Add(it);
        }
        else if (debugItems == null || debugItems.Count == 0)
        {
            Debug.LogWarning($"[{name}] Loot Table 이 비어 있어 드롭이 없습니다.");
        }

        if (debugItems != null)
            foreach (var it in debugItems)
                if (it != null && items.Count < MaxItems) items.Add(it);

        if (logEvents) Debug.Log($"[Loot] {name} 드롭 {items.Count}개: {DescribeItems()}");
    }

    // 보스 확정 드롭(Day 9) 등 코드에서 아이템을 추가할 때 사용. 가득 차면 false
    public bool AddItem(ItemData item)
    {
        if (item == null || items.Count >= MaxItems) return false;
        items.Add(item);
        Changed?.Invoke();
        return true;
    }

    public ItemData GetItem(int index)
    {
        return index >= 0 && index < items.Count ? items[index] : null;
    }

    // 가져간 아이템을 목록에서 제거 (반환 = 제거한 아이템)
    public ItemData RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count) return null;
        ItemData item = items[index];
        items.RemoveAt(index);
        Changed?.Invoke();

        if (items.Count == 0 && destroyWhenEmpty)
            StartCoroutine(DespawnRoutine(emptyDestroyDelay));
        return item;
    }

    // 루팅 창이 열고 닫을 때 호출. 열려 있는 동안은 제거되지 않음
    public void SetViewed(bool viewed)
    {
        viewers = Mathf.Max(0, viewers + (viewed ? 1 : -1));
    }

    private IEnumerator DespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        while (viewers > 0) yield return null;
        // 기다리는 사이 내용물이 생겼다면(AddItem) 유지 (상자 제외 규칙: 시체만 해당)
        if (items.Count > 0 && delay == emptyDestroyDelay) yield break;
        Destroy(gameObject);
    }

    private string DescribeItems()
    {
        if (items.Count == 0) return "(없음)";
        var names = new List<string>();
        foreach (var it in items) names.Add(it.ItemName);
        return string.Join(", ", names);
    }
}
