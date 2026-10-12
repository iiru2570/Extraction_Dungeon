using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Player Input 의 Behavior = Invoke Unity Events 용
// 이동/점프 로직만 담당하고, 애니메이션은 PlayerAnimator, 공격은 PlayerCombat 이 처리합니다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 12f;
    [SerializeField] private Transform model;   // X Bot (회전시킬 대상)

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    // ---- 다른 스크립트가 읽는 상태 ----
    public bool IsMoving { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float VerticalVelocity => verticalVelocity;
    public float MoveSpeedNormalized { get; private set; }   // 0 ~ 1

    // true 이면 이동/점프 입력을 무시합니다 (공격 중 등). 중력은 계속 적용됩니다.
    public bool MovementLocked { get; set; }

    // ---- 이벤트 ----
    public event Action Jumped;

    private CharacterController controller;
    private Vector2 moveInput;
    private float verticalVelocity;
    private Transform cam;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null) cam = Camera.main.transform;
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
    }

    // Events > Player > Move 에 연결
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Events > Player > Jump 에 연결
    public void OnJump(InputAction.CallbackContext context)
    {
        if (MovementLocked) return; // 공격 중에는 점프 불가

        // 누른 순간에만, 바닥에 있을 때만 점프 (더블점프 불가)
        if (context.performed && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Jumped?.Invoke();
        }
    }

    // 모델이 카메라가 보는 방향(수평)을 바라보게 합니다. (공격 시작 시 사용)
    public void FaceCameraDirection()
    {
        if (cam == null || model == null) return;
        Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (f.sqrMagnitude > 0.001f)
            model.rotation = Quaternion.LookRotation(f.normalized);
    }

    private void Update()
    {
        Vector2 input = MovementLocked ? Vector2.zero : moveInput;

        // 카메라 기준 이동 방향
        Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
        Vector3 moveDir = forward * input.y + right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // 중력 / 착지 처리
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // 상태 갱신
        IsMoving = moveDir.sqrMagnitude > 0.001f;
        MoveSpeedNormalized = moveDir.magnitude;

        // 이동 방향으로 모델 회전
        if (model != null && IsMoving)
        {
            Quaternion target = Quaternion.LookRotation(moveDir);
            model.rotation = Quaternion.Slerp(model.rotation, target, rotateSpeed * Time.deltaTime);
        }
    }
}