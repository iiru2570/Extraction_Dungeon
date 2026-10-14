using UnityEngine;
using UnityEngine.InputSystem;

// Player Input 의 Behavior = Invoke Unity Events 용
// Player 오브젝트에 PlayerController 와 함께 붙입니다.
public class CameraLook : MonoBehaviour
{
    [SerializeField] private Transform cameraTarget;      // Player/CameraTarget
    [SerializeField] private float sensitivity = 0.1f;    // 마우스 감도
    [SerializeField] private float minPitch = -30f;       // 아래로 보는 한계
    [SerializeField] private float maxPitch = 70f;        // 위로 보는 한계
    [SerializeField] private bool lockCursor = true;

    private float yaw;
    private float pitch = 15f;
    private Vector2 lookInput;

    private void Start()
    {
        if (cameraTarget != null) yaw = cameraTarget.eulerAngles.y;
        SetCursor(lockCursor);
    }

    // Events > Player > Look 에 연결
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        // ESC로 커서 해제, 화면 클릭으로 다시 잠금
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
            SetCursor(true);
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || Cursor.lockState != CursorLockMode.Locked) return;

        // 마우스 delta는 이미 프레임당 이동량이므로 Time.deltaTime을 곱하지 않습니다.
        yaw += lookInput.x * sensitivity;
        pitch -= lookInput.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        lookInput = Vector2.zero; // 같은 delta를 두 번 적용하지 않도록 초기화
    }

    private void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}