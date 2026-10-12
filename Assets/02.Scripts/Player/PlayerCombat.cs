using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 왼쪽 클릭 콤보 공격 (Slash1 -> Slash2 -> Slash3)
// Player 오브젝트에 PlayerController 와 함께 붙입니다.
//
// Animator Controller 필요 사항
//  - 상태 이름: Slash1, Slash2, Slash3, Idle
//  - 파라미터 : AttackStart, AttackNext, AttackEnd (모두 Trigger)
//  - 전환
//      Any State -> Slash1 : AttackStart
//      Slash1 -> Slash2    : AttackNext (Has Exit Time)
//      Slash2 -> Slash3    : AttackNext (Has Exit Time)
//      Any State -> Idle   : AttackEnd  (콤보가 끊기면 코드가 발동)
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;   // X Bot 의 Animator (비우면 자식에서 자동 검색)

    [Header("Combo")]
    [Tooltip("현재 슬래시 모션이 이 비율(0~1) 이상 재생된 뒤부터 다음 콤보 입력을 받습니다.")]
    [SerializeField, Range(0f, 1f)] private float comboInputStart = 0.25f;
    [Tooltip("모션이 이 비율(0~1)까지 재생됐는데 다음 입력이 없으면 콤보가 끊긴 것으로 보고 Idle 로 돌아갑니다.")]
    [SerializeField, Range(0.5f, 1f)] private float comboEndTime = 0.95f;
    [SerializeField] private bool faceCameraOnAttack = true;

    private static readonly int AttackStartHash = Animator.StringToHash("AttackStart");
    private static readonly int AttackNextHash = Animator.StringToHash("AttackNext");
    private static readonly int AttackEndHash = Animator.StringToHash("AttackEnd");
    private const int MaxCombo = 3;

    private PlayerController player;
    private float lastStartTime = -10f;
    private int lastStep;
    private bool nextQueued;     // 다음 콤보 입력이 이미 들어와 있는지
    private bool endRequested;   // Idle 복귀 요청을 이미 보냈는지

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // Events > Player > Attack 에 연결
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || animator == null) return;

        // 커서가 풀려 있을 때(ESC 이후 화면 클릭으로 다시 잠글 때)는 공격하지 않음
        if (Cursor.lockState != CursorLockMode.Locked) return;

        ReadState(out int step, out bool ending);

        if (step == 0 || ending)
        {
            // 공격 중이 아님 -> Slash1 시작 (바닥에 있을 때만)
            if (!player.IsGrounded) return;
            if (Time.time - lastStartTime < 0.1f) return;

            lastStartTime = Time.time;
            nextQueued = false;
            endRequested = false;
            if (faceCameraOnAttack) player.FaceCameraDirection();
            animator.ResetTrigger(AttackNextHash);
            animator.ResetTrigger(AttackEndHash);
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
        // Slash3 중에는 추가 입력 무시
    }

    private void Update()
    {
        if (animator == null) return;

        ReadState(out int step, out bool ending);
        bool inTransition = animator.IsInTransition(0);

        // 공격 모션 중에는 이동/점프 잠금 (Idle 로 돌아가는 전환이 시작되면 해제)
        player.MovementLocked = (step > 0 && !ending) || (Time.time - lastStartTime < 0.1f);

        if (step == 0 && !inTransition)
        {
            // 공격 상태가 아님 -> 남아 있는 트리거/플래그 정리
            animator.ResetTrigger(AttackNextHash);
            animator.ResetTrigger(AttackEndHash);
            nextQueued = false;
            endRequested = false;
            lastStep = 0;
            return;
        }

        if (step > 0 && !ending && !inTransition)
        {
            // 다음 슬래시 단계로 넘어왔으면 플래그 초기화
            if (step != lastStep)
            {
                lastStep = step;
                nextQueued = false;
                endRequested = false;
            }

            // 모션이 거의 끝났는데 다음 입력이 없다 -> 콤보 끊김 -> Idle 로 복귀
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (!nextQueued && !endRequested && info.normalizedTime >= comboEndTime)
            {
                animator.SetTrigger(AttackEndHash);
                endRequested = true;
            }
        }
    }

    // step  : 현재(또는 진입 중인) 슬래시 단계 (0 = 공격 아님)
    // ending: 슬래시에서 다른 상태(Idle 등)로 빠져나가는 전환 중인지
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
        if (info.IsName("Slash3")) return 3;
        return 0;
    }
}