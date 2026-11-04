using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어의 상호작용 담당 (Player 오브젝트에 붙입니다).
//  - 범위 안에서 가장 가까운 IInteractable 을 Target 으로 고름 (프롬프트 표시용)
//  - E 를 "누르는 순간" 시작, 계속 누르고 있는 동안 Progress(0~1)가 차오르고, 1이 되면 OnInteracted 호출
//  - 아래 취소 규칙 중 하나라도 걸리면 즉시 취소 (Progress 초기화)
//      1) E 를 뗌 (항상)
//      2) 대상에서 breakRange 보다 멀어짐
//      3) 피격 (Health.Damaged)
//      4) 이동 (시작 위치에서 moveCancelDistance 이상 벗어남)
//      5) 공격/패링 모션 시작
//      6) UI 가 열림 / 플레이어 사망 / 대상이 사라짐
//  - E 키는 Input Action 없이 Keyboard.current 로 직접 읽습니다 (CameraLook 의 ESC 처리와 같은 방식).
[DisallowMultipleComponent]
public class Interactor : MonoBehaviour
{
    [Header("탐색")]
    [Tooltip("탐색 중심 (비우면 이 오브젝트의 위치 = 발 쪽). 가슴 높이 빈 오브젝트를 넣어도 됩니다.")]
    [SerializeField] private Transform origin;
    [SerializeField] private float range = 2.5f;
    [Tooltip("상호작용 대상 콜라이더가 있는 레이어 (Interactable 만 체크)")]
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("취소 규칙 (E 를 뗐을 때는 항상 취소)")]
    [SerializeField] private bool cancelOnDistance = true;
    [Tooltip("진행 중에 이 거리보다 멀어지면 취소. Range 보다 조금 크게")]
    [SerializeField] private float breakRange = 3.5f;
    [SerializeField] private bool cancelOnDamaged = true;
    [SerializeField] private bool cancelOnMove = true;
    [Tooltip("시작한 위치에서 이만큼(수평, m) 이상 움직이면 취소")]
    [SerializeField] private float moveCancelDistance = 0.3f;
    [Tooltip("공격 모션 / 패링(가드) 모션이 시작되면 취소")]
    [SerializeField] private bool cancelOnCombat = true;
    [SerializeField] private bool logEvents = true;

    // ---- 다른 스크립트가 읽는 상태 ----
    public bool IsInteracting { get; private set; }
    public float Progress { get; private set; }   // 0 ~ 1

    // 현재 선택된 대상 (없으면 null). 파괴된 오브젝트는 자동으로 null 처리
    public IInteractable Target
    {
        get
        {
            if (target is UnityEngine.Object o && o == null) target = null;
            return target;
        }
        private set { target = value; }
    }

    // ---- 이벤트 ----
    public event Action<IInteractable> Started;
    public event Action<IInteractable> Completed;
    public event Action<IInteractable, string> Cancelled;   // (대상, 취소 이유)

    private IInteractable target;
    private IInteractable current;     // 진행 중인 대상
    private float elapsed;
    private float duration;
    private Vector3 startPos;

    private Health health;
    private PlayerCombat combat;
    private PlayerParry parry;
    private readonly Collider[] buffer = new Collider[32];

    private void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        parry = GetComponent<PlayerParry>();
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (IsInteracting) End();
    }

    private void OnDamaged(DamageInfo info)
    {
        if (cancelOnDamaged) Cancel("피격");
    }

    private Vector3 OriginPos => origin != null ? origin.position : transform.position;

    private void Update()
    {
        var kb = Keyboard.current;
        bool pressed = kb != null && kb.eKey.wasPressedThisFrame;
        bool held = kb != null && kb.eKey.isPressed;

        // 사망 / UI 열림 중에는 상호작용 불가
        if ((health != null && health.IsDead) || UIState.IsOpen)
        {
            Cancel(UIState.IsOpen ? "UI 열림" : "사망");
            Target = null;
            return;
        }

        if (!IsInteracting)
        {
            Target = FindTarget();
            if (pressed && Target != null) Begin(Target);
            return;
        }

        // ---- 진행 중 ----
        Target = current;

        if (!held) { Cancel("E 뗌"); return; }

        if (current == null || (current is UnityEngine.Object o && o == null) || !current.CanInteract(this))
        {
            Cancel("대상 사라짐");
            return;
        }

        if (cancelOnDistance)
        {
            float d = Vector3.Distance(OriginPos, current.InteractPosition);
            if (d > breakRange) { Cancel("거리 이탈"); return; }
        }

        if (cancelOnMove)
        {
            Vector3 delta = transform.position - startPos;
            delta.y = 0f;
            if (delta.magnitude > moveCancelDistance) { Cancel("이동"); return; }
        }

        if (cancelOnCombat && ((combat != null && combat.IsAttacking) || (parry != null && parry.IsGuarding)))
        {
            Cancel("공격/패링");
            return;
        }

        elapsed += Time.deltaTime;
        Progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        if (Progress >= 1f) Complete();
    }

    // 범위 안에서 가장 가까운, 상호작용 가능한 대상
    private IInteractable FindTarget()
    {
        Vector3 center = OriginPos;
        int count = Physics.OverlapSphereNonAlloc(center, range, buffer, interactMask, QueryTriggerInteraction.Collide);

        IInteractable best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var it = buffer[i].GetComponentInParent<IInteractable>();
            if (it == null || !it.CanInteract(this)) continue;

            float sqr = (it.InteractPosition - center).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = it;
            }
        }
        return best;
    }

    private void Begin(IInteractable t)
    {
        current = t;
        elapsed = 0f;
        duration = Mathf.Max(0f, t.InteractDuration);
        startPos = transform.position;
        Progress = 0f;
        IsInteracting = true;
        if (logEvents) Debug.Log($"[Interact] 시작: {t.Prompt} ({duration:0.0}초)");
        Started?.Invoke(t);

        if (duration <= 0f) Complete();   // 시간이 0 이면 즉시 완료
    }

    private void Complete()
    {
        IInteractable t = current;
        End();
        if (logEvents) Debug.Log("[Interact] 완료");
        t.OnInteracted(this);   // 상태를 먼저 정리한 뒤 호출 (대상이 UI 를 열어도 안전)
        Completed?.Invoke(t);
    }

    // 외부(예: 포탈 피격 중단)에서 강제로 취소할 때도 사용
    public void Cancel(string reason)
    {
        if (!IsInteracting) return;
        IInteractable t = current;
        End();
        if (logEvents) Debug.Log($"[Interact] 취소: {reason}");
        Cancelled?.Invoke(t, reason);
    }

    private void End()
    {
        IsInteracting = false;
        Progress = 0f;
        elapsed = 0f;
        current = null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = origin != null ? origin.position : transform.position;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(c, range);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(c, breakRange);
    }
}
