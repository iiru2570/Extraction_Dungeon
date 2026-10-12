using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 이동 / 달리기 / 점프 + 카메라 타겟 회전(마우스 룩).
/// Cinemachine의 Third Person Follow가 cameraTarget의 위치와 회전을 따라간다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input (InputSystem_Actions의 Player 맵)")]
    [SerializeField] private InputActionReference moveAction;   // Vector2
    [SerializeField] private InputActionReference lookAction;   // Vector2 (Mouse delta)
    [SerializeField] private InputActionReference jumpAction;   // Button
    [SerializeField] private InputActionReference sprintAction; // Button

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float bodyTurnSpeed = 14f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;            // Player 자식, 머리 높이(0, 1.6, 0)
    [SerializeField] private float lookSensitivity = 0.1f;      // 마우스 delta(px) -> 도(degree)
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool lockCursor = true;

    /// <summary>스태미나 시스템이 생기면 여기서 달리기 허용 여부를 제어한다.</summary>
    public bool SprintAllowed { get; set; } = true;

    /// <summary>애니메이터 / 스태미나 연동용 읽기 전용 상태.</summary>
    public bool IsSprinting { get; private set; }
    public bool IsGrounded => controller.isGrounded;
    public float HorizontalSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;

    /// <summary>카메라가 바라보는 수평 방향 (공격 / 조준 방향에 사용).</summary>
    public Vector3 AimForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

    private CharacterController controller;
    private Vector3 velocity;   // x,z = 수평 속도, y = 수직 속도
    private float yaw;
    private float pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
        sprintAction.action.Disable();
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void LateUpdate()
    {
        // 부모(Player)가 회전해도 카메라 타겟은 독립적으로 yaw/pitch를 유지한다.
        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleLook()
    {
        Vector2 look = lookAction.action.ReadValue<Vector2>();
        // 마우스 delta는 이미 프레임 간 변화량이므로 Time.deltaTime을 곱하지 않는다.
        yaw += look.x * lookSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
    }

    private void HandleMove()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        bool hasInput = input.sqrMagnitude > 0.01f;

        // 카메라 yaw 기준으로 이동 방향 계산
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 wishDir = yawRot * new Vector3(input.x, 0f, input.y);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        IsSprinting = hasInput && SprintAllowed && sprintAction.action.IsPressed();
        float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;

        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        horizontal = Vector3.MoveTowards(horizontal, wishDir * targetSpeed, acceleration * Time.deltaTime);

        // 점프 / 중력
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        if (controller.isGrounded && jumpAction.action.WasPressedThisFrame())
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        velocity.y += gravity * Time.deltaTime;

        velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        controller.Move(velocity * Time.deltaTime);

        // 몸은 항상 카메라가 보는 방향을 향함 (마우스 조준 기반 전투용)
        transform.rotation = Quaternion.Slerp(
            transform.rotation, yawRot, bodyTurnSpeed * Time.deltaTime);
    }
}
