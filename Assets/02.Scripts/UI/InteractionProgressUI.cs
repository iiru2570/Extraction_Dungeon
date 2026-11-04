using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면에 "[E] 철검 줍기" 프롬프트와 진행 게이지를 표시합니다 (Canvas 아래 오브젝트에 붙임).
//  - 대상이 범위 안에 있으면 프롬프트 표시
//  - E 를 누르고 있는 동안 게이지(Fill Image, Image Type = Filled) 표시
//  - UI(인벤토리 등)가 열려 있으면 모두 숨김
public class InteractionProgressUI : MonoBehaviour
{
    [SerializeField] private Interactor interactor;   // 비우면 자동 검색
    [SerializeField] private GameObject promptRoot;   // 프롬프트 묶음 (켜고 끌 대상)
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject gaugeRoot;    // 게이지 묶음 (켜고 끌 대상)
    [SerializeField] private Image gaugeFill;         // Image Type = Filled

    private void Awake()
    {
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        if (interactor == null)
            Debug.LogError("[InteractionProgressUI] Interactor 를 찾지 못했습니다. Player 에 Interactor 를 붙였는지 확인하세요.");
        SetVisible(false, false);
    }

    private void Update()
    {
        if (interactor == null) return;

        IInteractable target = UIState.IsOpen ? null : interactor.Target;
        bool hasTarget = target != null;
        bool interacting = hasTarget && interactor.IsInteracting;

        SetVisible(hasTarget, interacting);

        if (hasTarget && promptText != null)
            promptText.text = $"[E] {target.Prompt}";
        if (interacting && gaugeFill != null)
            gaugeFill.fillAmount = interactor.Progress;
    }

    private void SetVisible(bool prompt, bool gauge)
    {
        if (promptRoot != null && promptRoot.activeSelf != prompt) promptRoot.SetActive(prompt);
        if (gaugeRoot != null && gaugeRoot.activeSelf != gauge) gaugeRoot.SetActive(gauge);
    }
}
