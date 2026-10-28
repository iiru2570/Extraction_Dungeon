using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 화면 HUD 체력바 (Screen Space Canvas 의 체력바 1개).
// 머리 위 / 월드 스페이스 체력바는 만들지 않습니다. 적 체력바도 없음.
//
// Health 에는 회복/리셋 이벤트가 없으므로 매 프레임 Health.Current 를 읽어 갱신합니다.
//
// 설정
//  Canvas (Screen Space - Overlay)
//   └ HealthBar
//       ├ Background (Image)
//       └ Fill (Image, Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left)
//  - Fill 을 이 스크립트의 Fill Image 에 연결 (Health 는 비워두면 Player 에서 자동 검색)
//  - 숫자 텍스트(선택): TextMeshPro - Text 를 Health Text 에 연결
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private Health playerHealth;      // 비우면 PlayerController 가 있는 오브젝트에서 자동 검색
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text healthText;      // 선택: "75 / 100"

    private void Awake()
    {
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    private void Update()
    {
        if (playerHealth == null && !FindPlayerHealth()) return;

        float ratio = playerHealth.Max > 0f ? playerHealth.Current / playerHealth.Max : 0f;
        if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(ratio);
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(playerHealth.Current)} / {Mathf.CeilToInt(playerHealth.Max)}";
    }

    private bool FindPlayerHealth()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return false;
        playerHealth = player.GetComponent<Health>();
        return playerHealth != null;
    }
}
