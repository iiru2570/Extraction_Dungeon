using UnityEngine;
using UnityEngine.InputSystem;

// Player Input 의 Behavior = Invoke Unity Events 용
// Player 오브젝트에 PlayerController 와 함께 붙입니다.
//
// Day 5 변경점 (UIState 연동)
//  - UI(인벤토리 등)가 열려 있으면 시점 회전을 멈추고, "클릭하면 다시 잠금" 도 하지 않습니다.
//  - UI 가 열리고 닫힌 직후, 또는 커서 상태가 바뀐 직후 1프레임의 마우스 이동량은 버립니다 (시점이 튀는 것 방지).
//  - UI 를 ESC 로 닫은 그 프레임에는 ESC 가 커서 해제로 중복 처리되지 않게 막습니다.
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
    private int ignoreLookUntilFrame = -1;

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
        // UI 가 열려 있으면 커서는 UIState 가 관리합니다
        if (UIState.IsOpen)
        {
            lookInput = Vector2.zero;
            return;
        }

        // UI 가 방금 바뀐 프레임에는 ESC/클릭을 처리하지 않음 (같은 입력이 두 번 처리되는 것 방지)
        if (Time.frameCount == UIState.LastChangeFrame) return;

        // ESC로 커서 해제, 화면 클릭으로 다시 잠금
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
            SetCursor(true);
    }

    private void LateUpdate()
    {
        if (cameraTarget == null) return;

        // 커서가 풀려 있거나 UI 가 열려 있거나, 상태가 바뀐 직후에는 마우스 이동량을 버림
        bool justChanged = Time.frameCount <= Mathf.Max(ignoreLookUntilFrame, UIState.LastChangeFrame + 1);
        if (Cursor.lockState != CursorLockMode.Locked || UIState.IsOpen || justChanged)
        {
            lookInput = Vector2.zero;
            return;
        }

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
        ignoreLookUntilFrame = Time.frameCount + 1;
    }
}