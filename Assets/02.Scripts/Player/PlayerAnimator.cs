using UnityEngine;

// 애니메이션 파라미터 관리 전용 스크립트.
// Player 오브젝트에 PlayerController 와 함께 붙입니다.
// 새 애니메이션이 생기면 이 파일에 해시/파라미터 처리만 추가하면 됩니다.
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;   // X Bot 의 Animator (비우면 자식에서 자동 검색)

    // 파라미터 이름 (Animator Controller 의 Parameters 와 동일해야 함)
    private static readonly int WalkHash = Animator.StringToHash("Walk");         // Bool
    private static readonly int GroundedHash = Animator.StringToHash("Grounded"); // Bool (선택)
    private static readonly int JumpHash = Animator.StringToHash("Jump");         // Trigger (선택)
    private static readonly int YVelocityHash = Animator.StringToHash("YVelocity"); // Float (선택)

    private PlayerController player;
    private bool hasWalk, hasGrounded, hasJump, hasYVelocity;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheParameters();
    }

    private void OnEnable()
    {
        player.Jumped += OnJumped;
    }

    private void OnDisable()
    {
        player.Jumped -= OnJumped;
    }

    // Controller 에 실제로 존재하는 파라미터만 사용 (없는 파라미터 경고 방지)
    private void CacheParameters()
    {
        if (animator == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == WalkHash) hasWalk = true;
            else if (p.nameHash == GroundedHash) hasGrounded = true;
            else if (p.nameHash == JumpHash) hasJump = true;
            else if (p.nameHash == YVelocityHash) hasYVelocity = true;
        }
    }

    private void Update()
    {
        if (animator == null) return;

        // Walk: 이동 중이면 켜고, 멈추면 끔
        if (hasWalk) animator.SetBool(WalkHash, player.IsMoving);

        if (hasGrounded) animator.SetBool(GroundedHash, player.IsGrounded);
        if (hasYVelocity) animator.SetFloat(YVelocityHash, player.VerticalVelocity);
    }

    private void OnJumped()
    {
        if (animator != null && hasJump) animator.SetTrigger(JumpHash);
    }
}
