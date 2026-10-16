using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 왼쪽 클릭 콤보 공격 (Slash1 -> Slash2 -> CounterAttack)
// Player 오브젝트에 PlayerController 와 함께 붙입니다.
//
// Animator Controller 필요 사항
//  - 상태 이름: Slash1, Slash2, CounterAttack, Idle
//  - 파라미터 : AttackStart, AttackNext, AttackEnd, Counter (모두 Trigger)
//  - 전환
//      Any State -> Slash1         : AttackStart
//      Slash1 -> Slash2            : AttackNext (Has Exit Time)
//      Slash2 -> CounterAttack     : AttackNext (Has Exit Time)
//      Any State -> CounterAttack  : Counter    (Has Exit Time 끄기, Can Transition To Self 끄기,
//                                                Transition Duration 0.05~0.1)  ← 패링 성공 후 즉시 반격용
//      Any State -> Idle           : AttackEnd  (콤보가 끊기면 코드가 발동)
//
// 반격: 패링에 성공하면(PlayerParry.CanRiposte) 좌클릭 즉시 CounterAttack 이 발동합니다.
//       패링 모션 도중이어도 모션을 끊고 선딜 없이 바로 나갑니다.
//
// 타격 방식: 손에 붙은 칼의 WeaponHitbox 가 실제로 데미지를 줍니다.
//  - 이 스크립트는 공격 모션 중 "판정 구간"에만 칼 콜라이더를 켜고, 단계별 피해량을 넘겨줍니다.
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;   // X Bot 의 Animator (비우면 자식에서 자동 검색)

    [Header("Weapon")]
    [Tooltip("손에 붙은 칼의 WeaponHitbox. 비우면 자식에서 자동 검색합니다.")]
    [SerializeField] private WeaponHitbox weapon;

    [Header("Combo")]
    [Tooltip("현재 슬래시 모션이 이 비율(0~1) 이상 재생된 뒤부터 다음 콤보 입력을 받습니다.")]
    [SerializeField, Range(0f, 1f)] private float comboInputStart = 0.25f;
    [Tooltip("모션이 이 비율(0~1)까지 재생됐는데 다음 입력이 없으면 콤보가 끊긴 것으로 보고 Idle 로 돌아갑니다.")]
    [SerializeField, Range(0.5f, 1f)] private float comboEndTime = 0.95f;
    [SerializeField] private bool faceCameraOnAttack = true;

    [System.Serializable]
    public class SlashData
    {
        [Tooltip("피해량")]
        public float damage = 10f;
        [Tooltip("칼 콜라이더가 켜지는 모션 진행 비율 (0~1). 휘두르기 시작 직전")]
        [Range(0f, 1f)] public float hitStart = 0.3f;
        [Tooltip("칼 콜라이더가 꺼지는 모션 진행 비율 (0~1). 휘두르기가 끝나는 지점")]
        [Range(0f, 1f)] public float hitEnd = 0.6f;
    }

    [Header("Hit Window (인덱스 0 = Slash1, 1 = Slash2, 2 = CounterAttack)")]
    [Tooltip("준비 동작·마무리 동작에서 칼이 닿아 데미지가 들어가지 않도록 구간을 제한합니다. 전체 모션 동안 켜려면 0 ~ 1 로 두세요.")]
    [SerializeField]
    private SlashData[] slashes =
    {
        new SlashData { damage = 10f, hitStart = 0.30f, hitEnd = 0.60f },
        new SlashData { damage = 10f, hitStart = 0.30f, hitEnd = 0.60f },
        new SlashData { damage = 15f, hitStart = 0.35f, hitEnd = 0.65f },
    };

    private static readonly int AttackStartHash = Animator.StringToHash("AttackStart");
    private static readonly int AttackNextHash = Animator.StringToHash("AttackNext");
    private static readonly int AttackEndHash = Animator.StringToHash("AttackEnd");
    private static readonly int CounterHash = Animator.StringToHash("Counter");
    private const int MaxCombo = 3;

    // 공격 모션 중인지 (PlayerParry 등 다른 스크립트가 참조)
    public bool IsAttacking { get; private set; }

    // 타격 성공 이벤트 (히트스톱, 사운드, 이펙트가 구독). 칼의 Hit 이벤트를 그대로 전달합니다.
    public event System.Action<IDamageable, DamageInfo> Hit;

    private PlayerController player;
    private PlayerParry parry;   // 없어도 동작함
    private float lastStartTime = -10f;
    private int lastStep;
    private bool nextQueued;     // 다음 콤보 입력이 이미 들어와 있는지
    private bool endRequested;   // Idle 복귀 요청을 이미 보냈는지
    private int swingStep;       // 현재 칼에 설정된 스윙 단계 (0 = 없음)

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        parry = GetComponent<PlayerParry>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weapon == null) weapon = GetComponentInChildren<WeaponHitbox>(true);
        if (weapon == null)
            Debug.LogWarning("[PlayerCombat] WeaponHitbox 를 찾지 못했습니다. 칼 오브젝트에 WeaponHitbox 를 붙여주세요.");
    }

    private void OnEnable()
    {
        if (weapon != null) weapon.Hit += OnWeaponHit;
    }

    private void OnDisable()
    {
        if (weapon != null)
        {
            weapon.Hit -= OnWeaponHit;
            weapon.SetActive(false);
        }
    }

    private void OnWeaponHit(IDamageable target, DamageInfo info) => Hit?.Invoke(target, info);

    // Events > Player > Attack 에 연결
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || animator == null) return;

        // 커서가 풀려 있을 때(ESC 이후 화면 클릭으로 다시 잠글 때)는 공격하지 않음
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // 패링 성공 직후: 패링 모션 중이어도 바로 반격 (CounterAttack)
        if (parry != null && parry.CanRiposte && player.IsGrounded)
        {
            StartCounter();
            return;
        }

        ReadState(out int step, out bool ending);

        if (step == 0 || ending)
        {
            // 공격 중이 아님 -> Slash1 시작 (바닥에 있을 때만)
            if (!player.IsGrounded) return;
            if (parry != null && parry.IsParrying) return; // 패링 모션 중에는 공격 불가
            if (Time.time - lastStartTime < 0.1f) return;

            lastStartTime = Time.time;
            nextQueued = false;
            endRequested = false;
            swingStep = 0;          // 새 콤보: 다음 판정 구간에서 칼 기록 초기화
            if (faceCameraOnAttack) player.FaceCameraDirection();
            animator.ResetTrigger(AttackNextHash);
            animator.ResetTrigger(AttackEndHash);
            animator.ResetTrigger(CounterHash);
            animator.SetTrigger(AttackStartHash);
        }
        else if (step < MaxCombo && !endRequested)
        {
            // 공격 중 -> 모션이 어느 정도 진행된 뒤의 클릭만 다음 콤보로 인정
            if (animator.IsInTransition(0)) return;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.normalizedTime >= comboInputStart)
            {
                animator.SetTrigger(AttackNextHash);
                nextQueued = true;
            }
        }
        // CounterAttack 중에는 추가 입력 무시
    }

    // 패링 성공 후 반격: 현재 모션(패링 등)을 끊고 CounterAttack 으로 바로 전환
    private void StartCounter()
    {
        parry.ConsumeRiposte();

        lastStartTime = Time.time;
        nextQueued = false;
        endRequested = false;
        swingStep = 0;              // 새 스윙: 칼 기록 초기화
        if (faceCameraOnAttack) player.FaceCameraDirection();

        animator.ResetTrigger(AttackStartHash);
        animator.ResetTrigger(AttackNextHash);
        animator.ResetTrigger(AttackEndHash);
        animator.SetTrigger(CounterHash);
    }

    private void Update()
    {
        if (animator == null) return;

        ReadState(out int step, out bool ending);
        bool inTransition = animator.IsInTransition(0);

        // 공격 모션 중에는 이동/점프 잠금 (Idle 로 돌아가는 전환이 시작되면 해제)
        IsAttacking = (step > 0 && !ending) || (Time.time - lastStartTime < 0.1f);
        player.MovementLocked = IsAttacking;

        if (step == 0 && !inTransition)
        {
            // 공격 상태가 아님 -> 남아 있는 트리거/플래그 정리
            animator.ResetTrigger(AttackNextHash);
            animator.ResetTrigger(AttackEndHash);
            nextQueued = false;
            endRequested = false;
            lastStep = 0;
            swingStep = 0;
            SetWeaponActive(false);
            return;
        }

        if (step > 0 && !ending && !inTransition)
        {
            // 다음 공격 단계로 넘어왔으면 플래그 초기화
            if (step != lastStep)
            {
                lastStep = step;
                nextQueued = false;
                endRequested = false;
            }

            var info = animator.GetCurrentAnimatorStateInfo(0);

            // 칼 판정 구간 갱신
            UpdateWeaponWindow(step, info.normalizedTime);

            // 모션이 거의 끝났는데 다음 입력이 없다 -> 콤보 끊김 -> Idle 로 복귀
            if (!nextQueued && !endRequested && info.normalizedTime >= comboEndTime)
            {
                animator.SetTrigger(AttackEndHash);
                endRequested = true;
            }
        }
        else
        {
            // 단계 전환 중(콤보 이어지는 중 / Idle 로 빠지는 중)에는 칼 판정을 꺼서 엉뚱한 타격 방지
            SetWeaponActive(false);
        }
    }

    // ---- 칼 판정 ----

    private void UpdateWeaponWindow(int step, float normalizedTime)
    {
        if (weapon == null || slashes == null || step - 1 >= slashes.Length) return;
        SlashData data = slashes[step - 1];

        // 단계가 바뀌었으면 새 스윙: 맞은 대상 기록 초기화 + 피해량 설정
        if (swingStep != step)
        {
            swingStep = step;
            weapon.BeginSwing(data.damage);
        }

        bool inWindow = normalizedTime >= data.hitStart && normalizedTime <= data.hitEnd;
        SetWeaponActive(inWindow);
    }

    private void SetWeaponActive(bool on)
    {
        if (weapon != null) weapon.SetActive(on);
    }

    // ---- 애니메이터 상태 읽기 ----

    // step  : 현재(또는 진입 중인) 공격 단계 (0 = 공격 아님)
    // ending: 공격 모션에서 다른 상태(Idle 등)로 빠져나가는 전환 중인지
    private void ReadState(out int step, out bool ending)
    {
        int curStep = GetStep(animator.GetCurrentAnimatorStateInfo(0));
        ending = false;

        if (animator.IsInTransition(0))
        {
            int nextStep = GetStep(animator.GetNextAnimatorStateInfo(0));
            if (nextStep > 0)
            {
                step = nextStep;
                return;
            }
            ending = curStep > 0;
        }

        step = curStep;
    }

    private static int GetStep(AnimatorStateInfo info)
    {
        if (info.IsName("Slash1")) return 1;
        if (info.IsName("Slash2")) return 2;
        if (info.IsName("CounterAttack")) return 3;
        return 0;
    }
}