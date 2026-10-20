using System;
using UnityEngine;

// 체력 컴포넌트. 플레이어 / 몬스터 / 보스 / 훈련 더미 모두 이걸 붙여서 사용합니다.
// 같은 오브젝트에 IDamageInterceptor(PlayerParry 등)가 있으면, 데미지 적용 직전에 호출해
// 가드 감소 / 패링 무효 같은 보정을 먼저 거칩니다.
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool logDamage = true;   // 테스트용 콘솔 로그

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsDead { get; private set; }

    // 체력 바 UI, 피격 이펙트, 사망 처리 등이 구독 (보정이 끝난 최종 피해 기준)
    public event Action<DamageInfo> Damaged;
    public event Action Died;

    private IDamageInterceptor[] interceptors;

    private void Awake()
    {
        Current = maxHealth;
        interceptors = GetComponents<IDamageInterceptor>();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.amount <= 0f) return;

        // 가드 / 패링 / 그로기 보정
        foreach (var interceptor in interceptors)
        {
            if (!interceptor.Intercept(ref info)) return;   // 피해 무효
        }
        if (info.amount <= 0f) return;

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