using System;
using UnityEngine;

// 체력 컴포넌트. 플레이어 / 몬스터 / 보스 / 훈련 더미 모두 이걸 붙여서 사용합니다.
// 가드, 패링 같은 피해 보정은 나중에 TakeDamage 앞단(예: PlayerGuard)에서 처리하고,
// 최종 피해만 여기로 넘기면 됩니다.
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool logDamage = true;   // 테스트용 콘솔 로그

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsDead { get; private set; }

    // 체력 바 UI, 피격 이펙트, 사망 처리 등이 구독
    public event Action<DamageInfo> Damaged;
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.amount <= 0f) return;

        Current = Mathf.Max(0f, Current - info.amount);
        if (logDamage)
            Debug.Log($"[{name}] -{info.amount} ({Current}/{maxHealth})");

        Damaged?.Invoke(info);

        if (Current <= 0f)
        {
            IsDead = true;
            Died?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    public void ResetHealth()
    {
        Current = maxHealth;
        IsDead = false;
    }
}
