using UnityEngine;

// 데미지를 받을 수 있는 모든 대상(플레이어, 몬스터, 보스, 훈련 더미)이 구현합니다.
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(DamageInfo info);
}

// Health 가 데미지를 적용하기 "직전"에 개입하는 훅.
// 같은 오브젝트에 붙어 있으면 Health 가 자동으로 찾아 호출합니다.
//  - 플레이어: PlayerParry (가드 / 패링으로 피해 감소·무효)
//  - 그로기 상태의 적: 받는 피해 증가
// 반환값: true = 피해를 계속 적용 / false = 피해 무효 (info.amount 를 바꿔 감소·증가도 가능)
public interface IDamageInterceptor
{
    bool Intercept(ref DamageInfo info);
}

// 패링을 당할 수 있는 공격자(몬스터, 보스)가 구현합니다.
public interface IParryable
{
    void OnParried(Transform parrier);
}

// 한 번의 타격 정보. 나중에 필드를 추가하면 됩니다.
public struct DamageInfo
{
    public float amount;       // 피해량
    public Transform source;   // 공격자 (가드 방향 판정, 패링 대상 찾기에 사용)
    public Vector3 hitPoint;   // 타격 지점 (스파크 이펙트 위치)

    public DamageInfo(float amount, Transform source, Vector3 hitPoint)
    {
        this.amount = amount;
        this.source = source;
        this.hitPoint = hitPoint;
    }
}