using UnityEngine;

// 데미지를 받을 수 있는 모든 대상(플레이어, 몬스터, 보스, 훈련 더미)이 구현합니다.
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(DamageInfo info);
}

// 한 번의 타격 정보. 나중에 패링 판정, 가드 판정, 그로기 값 등을 필드로 추가하면 됩니다.
public struct DamageInfo
{
    public float amount;       // 피해량
    public Transform source;   // 공격자 (가드 방향 판정, 넉백 방향에 사용)
    public Vector3 hitPoint;   // 타격 지점 (스파크 이펙트 위치)

    public DamageInfo(float amount, Transform source, Vector3 hitPoint)
    {
        this.amount = amount;
        this.source = source;
        this.hitPoint = hitPoint;
    }
}
