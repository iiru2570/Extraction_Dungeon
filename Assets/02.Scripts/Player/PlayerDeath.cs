using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이어 사망 처리. Player 오브젝트(Health, PlayerController 가 있는 곳)에 붙입니다.
//
// 사망 시
//  1) 이동/점프 잠금 (PlayerHitReaction 등이 잠금을 풀어도 매 프레임 다시 잠금)
//  2) 카메라 회전 중단 (CameraLook 비활성화 -> "커서가 풀려 있을 때 클릭하면 다시 잠금" 로직도 함께 멈춤)
//  3) 커서 해제 -> PlayerCombat / PlayerParry 는 커서가 잠겨 있지 않으면 입력을 무시하므로 공격/패링도 막힘
//  4) 몬스터는 EnsureTarget() 이 false 가 되어 자동으로 Idle 로 돌아감
//  5) 잠시 후 임시 게임오버 패널 표시 -> 재시작 버튼 (Restart)
//
// Day 6 에서 사망 시 소지품 삭제 / 기본 장비 지급을 이 스크립트의 Died 이벤트에 연결합니다.
//
// 설정
//  - Game Over Panel: Canvas 아래 패널(기본은 비활성으로 두기) + 안에 재시작 Button
//  - 재시작 Button 의 OnClick -> Player 의 PlayerDeath.Restart()
//  - 재시작하려면 현재 씬이 File > Build Profiles(Build Settings) 의 Scenes In Build 에 등록되어 있어야 합니다.
public class PlayerDeath : MonoBehaviour
{
    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("사망 후 게임오버 패널이 뜨기까지의 시간(초, 실제 시간)")]
    [SerializeField] private float gameOverDelay = 1.5f;

    [Header("Death")]
    [Tooltip("사망 시 비활성화할 추가 컴포넌트 (선택). CameraLook 은 자동으로 비활성화됩니다.")]
    [SerializeField] private Behaviour[] disableOnDeath;
    [Tooltip("플레이어 Animator 에 이 이름의 Trigger 가 있으면 사망 시 발동 (없으면 무시)")]
    [SerializeField] private string dieTrigger = "Die";

    public bool IsDead { get; private set; }

    // Day 6(소지품 삭제), Day 10(RunManager) 등이 구독
    public event Action Died;

    private Health health;
    private PlayerController controller;
    private Animator animator;

    private void Awake()
    {
        health = GetComponent<Health>();
        controller = GetComponent<PlayerController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnEnable()
    {
        health.Died += OnHealthDied;
    }

    private void OnDisable()
    {
        health.Died -= OnHealthDied;
    }

    private void Update()
    {
        // 피격 경직 종료 등이 이동 잠금을 풀어버리는 것을 방지
        if (IsDead && controller != null) controller.MovementLocked = true;
    }

    private void OnHealthDied()
    {
        if (IsDead) return;
        IsDead = true;
        Debug.Log("[Player] 사망");

        if (controller != null) controller.MovementLocked = true;

        var look = FindFirstObjectByType<CameraLook>();
        if (look != null) look.enabled = false;

        if (disableOnDeath != null)
            foreach (var b in disableOnDeath)
                if (b != null) b.enabled = false;

        TriggerDeathAnimation();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Died?.Invoke();
        StartCoroutine(ShowGameOver());
    }

    private void TriggerDeathAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(dieTrigger)) return;
        foreach (var p in animator.parameters)
        {
            if (p.name == dieTrigger && p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(dieTrigger);
                return;
            }
        }
    }

    private IEnumerator ShowGameOver()
    {
        yield return new WaitForSecondsRealtime(gameOverDelay);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // 재시작 버튼의 OnClick 에 연결
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
