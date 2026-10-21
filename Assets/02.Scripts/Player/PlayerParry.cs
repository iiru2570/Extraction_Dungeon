using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 오른쪽 클릭 "한 번" = 패링 모션 재생. 모션이 재생되는 동안 가드가 유지됩니다.
// Player 오브젝트에 PlayerController, PlayerCombat, Health 와 함께 붙입니다.
//
//  - 모션 시작 후 parryWindow(0.25초) 동안: 무적. 이 구간에 정면 공격을 받으면 패링 성공
//      -> 피해 무효 + 공격자 그로기 + 강공격을 바로 쓸 수 있는 상태(CanRiposte) 열림
//  - 그 이후 모션이 끝날 때까지: 정면 피해를 guardDamageMultiplier(0.5 = 50% 감소) 배로 줄임
//  - 패링 윈도우 동안 공격을 받지 못하면 parryFailLock(0.5초) 동안 패링(무적) 불가
//      (이때도 모션을 쓰면 50% 감소 가드는 적용됨)
//  - 같은 오브젝트의 Health 가 데미지 직전에 Intercept 를 호출합니다 (IDamageInterceptor)
//
// 입력: Events > Player > Parry 에 OnParry 연결 (기존과 동일, 누른 순간만 사용)
//
// Animator Controller (기존 그대로)
//  - 상태 이름: Parry / 파라미터 Parry (Trigger) : 우클릭 시 PlayerAnimator 가 발동
//  - 가드는 "Parry 상태가 재생되는 동안"으로 판단하므로, 상태 이름이 반드시 Parry 여야 합니다.
[RequireComponent(typeof(PlayerController))]
public class PlayerParry : MonoBehaviour, IDamageInterceptor
{
    [SerializeField] private Animator animator;   // X Bot 의 Animator (비우면 자식에서 자동 검색)
    [Tooltip("Animator Controller 에서 패링(가드) 모션 상태의 이름")]
    [SerializeField] private string parryStateName = "Blocking";

    [Header("Parry (모션 시작 직후 무적 구간)")]
    [Tooltip("우클릭 직후 이 시간(초) 동안 무적. 이 안에 공격을 받으면 패링 성공")]
    [SerializeField] private float parryWindow = 0.25f;
    [Tooltip("패링 윈도우가 빗나간 뒤 다시 패링(무적)할 수 없는 시간(초). 패링 스팸 방지")]
    [SerializeField] private float parryFailLock = 0.5f;
    [Tooltip("패링 성공 후 강공격을 쓸 수 있는 시간(초)")]
    [SerializeField] private float riposteWindow = 1.5f;

    [Header("Guard (패링 윈도우 이후 모션이 끝날 때까지)")]
    [Tooltip("일반 가드 시 받는 피해 배율 (0.5 = 50% 감소)")]
    [SerializeField, Range(0f, 1f)] private float guardDamageMultiplier = 0.5f;
    [Tooltip("가드가 막아주는 정면 각도 (전체 각도). 180 = 정면 반원")]
    [SerializeField, Range(60f, 360f)] private float guardAngle = 180f;
    [SerializeField] private bool logEvents = true;   // 테스트용 콘솔 로그

    // 패링 모션이 재생되는 동안 true (= 가드 중)
    public bool IsGuarding { get; private set; }

    // 기존 코드(PlayerCombat) 호환용: 가드/패링 모션 중에는 공격 불가
    public bool IsParrying => IsGuarding;

    // 무적(패링) 구간이 열려 있는지
    public bool IsParryWindowOpen => windowArmed && Time.time <= windowEnd;

    // 패링에 성공해서 강공격을 바로 쓸 수 있는지 (강공격 구현에서 읽고, 쓰면 ConsumeRiposte 호출)
    public bool CanRiposte => Time.time <= riposteEnd;

    // 패링 모션 시작 시 발생 (PlayerAnimator 가 구독)
    public event Action Parried;

    // 패링 성공 / 일반 가드 성공 시 발생 (히트스톱, 스파크, 사운드, 반격 보상이 구독할 자리)
    public event Action<DamageInfo> ParrySucceeded;
    public event Action<DamageInfo> Blocked;

    private PlayerController player;
    private PlayerCombat combat;   // 없어도 동작함
    private PlayerHitReaction hitReaction;   // 없어도 동작함
    private float lastPressTime = -10f;
    private bool windowArmed;      // 이번 모션에서 무적 구간이 유효한지
    private bool parryTriggered;   // 이번 모션에서 이미 패링 반응(그로기 등)을 보냈는지
    private float windowEnd;
    private float parryLockedUntil = -10f;
    private float riposteEnd = -10f;
    private bool wasGuarding;      // 측정 로그용

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        combat = GetComponent<PlayerCombat>();
        hitReaction = GetComponent<PlayerHitReaction>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // Events > Player > Parry 에 연결 (한 번 클릭)
    public void OnParry(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // 커서가 풀려 있을 때(ESC 이후 화면 클릭으로 다시 잠글 때)는 무시
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (!player.IsGrounded) return;                              // 공중에서는 불가
        if (hitReaction != null && hitReaction.IsStaggered) return;  // 피격 경직 중에는 불가
        if (combat != null && combat.IsAttacking) return;            // 공격 중에는 불가
        if (IsGuarding) return;                                      // 이미 패링 모션 중에는 불가

        lastPressTime = Time.time;
        IsGuarding = true;
        parryTriggered = false;

        // 패링 스팸 방지 중이 아니면 무적 구간을 엽니다 (잠금 중이면 50% 가드만)
        if (Time.time >= parryLockedUntil)
        {
            windowArmed = true;
            windowEnd = Time.time + parryWindow;
        }
        else
        {
            windowArmed = false;
        }

        Parried?.Invoke();
    }

    // 패링 성공 후 강공격을 시작할 때 호출 (소모)
    public void ConsumeRiposte()
    {
        riposteEnd = -10f;
    }

    private void Update()
    {
        // 패링 모션 재생 중이거나 진입 중(전환 중)인지
        bool motion = false;
        if (animator != null)
        {
            bool inState = animator.GetCurrentAnimatorStateInfo(0).IsName(parryStateName);
            bool entering = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(parryStateName);
            motion = inState || entering;
        }
        bool justRequested = Time.time - lastPressTime < 0.1f;   // 트리거 직후 전환이 시작되기 전까지

        IsGuarding = motion || justRequested;

        // 측정용 로그: 우클릭부터 패링 모션이 끝날 때까지 실제로 걸린 시간 (수치 조정 참고용)
        if (!IsGuarding && wasGuarding && logEvents)
            Debug.Log($"[Parry] 모션 지속 시간: {Time.time - lastPressTime:F2}초 (무적 구간 설정 {parryWindow:F2}초)");
        wasGuarding = IsGuarding;

        // 무적 구간이 지나도록 공격을 받지 못함 -> 잠금
        if (windowArmed && Time.time > windowEnd)
        {
            windowArmed = false;
            if (!parryTriggered) parryLockedUntil = windowEnd + parryFailLock;
        }
    }

    // Health.TakeDamage 가 데미지 적용 직전에 호출. true = 피해 계속 적용, false = 무효
    public bool Intercept(ref DamageInfo info)
    {
        if (!IsGuarding || !IsInFront(info.source)) return true;

        // 무적 구간: 모든 피해 무효. 처음 맞은 공격에 대해서만 패링 반응(그로기, 반격 기회)
        if (IsParryWindowOpen)
        {
            if (!parryTriggered)
            {
                parryTriggered = true;
                riposteEnd = Time.time + riposteWindow;

                if (info.source != null)
                {
                    var target = info.source.GetComponentInParent<IParryable>();
                    if (target != null) target.OnParried(transform);
                }

                if (logEvents) Debug.Log($"[Parry] 패링 성공! (우클릭 후 {Time.time - lastPressTime:F2}초에 피격) 강공격 가능");
                ParrySucceeded?.Invoke(info);
            }
            return false;
        }

        // 그 이후 모션이 끝날 때까지: 피해 감소
        info.amount *= guardDamageMultiplier;
        if (logEvents)
        {
            float delay = Time.time - lastPressTime;
            string why = delay <= parryWindow
                ? "패링 잠금 중이라 무적 구간이 없었음"
                : "너무 일찍 눌러서 무적 구간이 이미 끝남";
            Debug.Log($"[Guard] 패링 실패, 일반 가드 (우클릭 후 {delay:F2}초에 피격, 무적 {parryWindow:F2}초) → {why} (피해 {info.amount})");
        }
        Blocked?.Invoke(info);
        return info.amount > 0f;
    }

    // 공격자가 가드 방향(모델 정면) 안에 있는지
    private bool IsInFront(Transform source)
    {
        if (source == null) return true;

        Transform body = animator != null ? animator.transform : transform;
        Vector3 toSource = source.position - body.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(body.forward, toSource) <= guardAngle * 0.5f;
    }

    // Scene 뷰에서 가드 각도 표시 (Player 선택 시).
    //  평소 = 청록 / 가드 중 = 주황 / 무적(패링) 구간 = 초록
    private void OnDrawGizmosSelected()
    {
        Transform body = animator != null ? animator.transform : GetComponentInChildren<Animator>()?.transform;
        if (body == null) body = transform;

        Color color = new Color(0f, 0.8f, 1f, 0.9f);
        if (Application.isPlaying)
        {
            if (IsParryWindowOpen) color = Color.green;
            else if (IsGuarding) color = new Color(1f, 0.6f, 0f, 0.9f);
        }
        Gizmos.color = color;

        const float radius = 2f;
        const int segments = 24;
        Vector3 origin = body.position + Vector3.up * 0.1f;
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        float half = Mathf.Min(guardAngle, 360f) * 0.5f;

        // 부채꼴 호
        Vector3 prev = origin + Quaternion.AngleAxis(-half, Vector3.up) * forward * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)segments);
            Vector3 next = origin + Quaternion.AngleAxis(a, Vector3.up) * forward * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        // 양쪽 경계선 + 정면 선
        if (guardAngle < 360f)
        {
            Gizmos.DrawLine(origin, origin + Quaternion.AngleAxis(-half, Vector3.up) * forward * radius);
            Gizmos.DrawLine(origin, origin + Quaternion.AngleAxis(half, Vector3.up) * forward * radius);
        }
        Gizmos.DrawLine(origin, origin + forward * radius);
    }
}