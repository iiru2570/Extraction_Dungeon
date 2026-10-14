using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 오른쪽 클릭 한 번으로 패링 (지금은 애니메이션 + 상태 용도만)
// Player 오브젝트에 PlayerController, PlayerCombat 과 함께 붙입니다.
// 실제 패링 판정(타이밍 창, 적 공격 튕겨내기)은 공격 시스템을 설계할 때 IsParrying 을 읽어서 구현합니다.
//
// Animator Controller 필요 사항
//  - 상태 이름: Parry
//  - 파라미터 : Parry (Trigger)  ← PlayerAnimator 가 발동
//  - 전환     : Any State -> Parry (Parry), Parry -> Idle (Has Exit Time)
[RequireComponent(typeof(PlayerController))]
public class PlayerParry : MonoBehaviour
{
    [SerializeField] private Animator animator;   // X Bot 의 Animator (비우면 자식에서 자동 검색)

    [Tooltip("패링을 연타하지 못하게 하는 대기 시간(초)")]
    [SerializeField] private float cooldown = 0.5f;

    // 패링 모션 중인지 (나중에 피격 처리에서 읽을 값)
    public bool IsParrying { get; private set; }

    // 패링 시작 시 발생 (PlayerAnimator 가 구독)
    public event Action Parried;

    private PlayerController player;
    private PlayerCombat combat;   // 없어도 동작함
    private float lastParryTime = -10f;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        combat = GetComponent<PlayerCombat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // Events > Player > Parry 에 연결
    public void OnParry(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // 커서가 풀려 있을 때(ESC 이후 화면 클릭으로 다시 잠글 때)는 무시
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (!player.IsGrounded) return;                              // 공중에서는 불가
        if (combat != null && combat.IsAttacking) return;            // 공격 중에는 불가
        if (IsParrying) return;                                      // 패링 모션 중에는 불가
        if (Time.time - lastParryTime < cooldown) return;            // 쿨타임

        lastParryTime = Time.time;
        Parried?.Invoke();
    }

    private void Update()
    {
        if (animator == null)
        {
            IsParrying = false;
            return;
        }

        // Parry 상태 재생 중이거나 진입 중(전환 중)이면 패링 중
        bool inState = animator.GetCurrentAnimatorStateInfo(0).IsName("Parry");
        bool entering = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Parry");
        bool justRequested = Time.time - lastParryTime < 0.1f;

        IsParrying = inState || entering || justRequested;
    }
}
