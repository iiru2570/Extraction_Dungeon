using UnityEngine;

// 해골 병사 (원거리): EnemyBase 를 상속하고, 근접 WeaponHitbox 대신 투사체를 발사합니다.
// 공통 FSM(감지 / 추적 / 예고 / 공격 / 후딜 / 그로기)은 EnemyBase 가 처리합니다.
//
//  Telegraph (활 당기는 모션) -> Strike 시작 순간 OnStrikeBegin 에서 투사체 발사 -> Recover (후딜)
//  플레이어는 "활을 당기는 모션"을 읽고, 날아오는 투사체가 닿는 순간에 맞춰 패링합니다.
//
// 패링 처리 (Day 2 결정)
//  - 투사체는 어떤 경우든 맞는 순간 사라짐 (패링 성공 시 피해 0)
//  - groggyWhenParried 가 켜져 있으면 발사한 해골도 그로기 (기존 규칙 그대로)
//    끄면 투사체만 무효화되고 해골은 그대로 (반격 기회만 남음)
//
// 권장 시작 수치 (Inspector)
//  Health(Max) 60 / Damage 20
//  Chase Speed 2.8 / Sight Range 14 / Chase Sight Range 16
//  Attack Range 10 (이 거리 안에서 쏘고, 추적 정지 거리는 자동으로 attackRange*0.7)
//  Telegraph 0.9 / Strike 0.1 / Recover 1.0 / Cooldown 2.0
//  Groggy Time 2.5 (멀리서 패링해도 달려가서 때릴 시간을 주기 위해 근접보다 길게)
//  Projectile Speed 11 (너무 빠르면 눈으로 읽고 패링하기 어려움)
//
// 계층
//  SkeletonArcher (Health, SkeletonArcherAI, NavMeshAgent, Collider)
//   └ Model (Animator) └ ... Bow └ Muzzle (빈 오브젝트, 화살이 나가는 위치. 비워도 동작)
//  WeaponHitbox 는 필요 없습니다.
//
// Animator: 근접 해골과 동일 (Speed / Attack / Groggy / Hit / Die, 사망 상태 이름 Death)
//  Attack 모션 = 활을 당겼다가 놓는 동작. "놓는 순간"이 Telegraph 시간(0.9초) 끝과 맞도록 조정하세요.
public class SkeletonArcherAI : EnemyBase
{
    [Header("Archer (원거리)")]
    [Tooltip("발사할 투사체 프리팹 (Projectile 컴포넌트)")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("투사체가 나가는 위치. 비우면 몸 앞쪽 가슴 높이에서 발사")]
    [SerializeField] private Transform muzzle;
    [Tooltip("투사체 속도 (m/s). 프리팹의 속도 대신 사용")]
    [SerializeField] private float projectileSpeed = 11f;
    [Tooltip("이 거리보다 가까우면 쏘지 않음 (0 = 제한 없음). 너무 붙으면 가만히 서 있게 되니 보통 0 권장")]
    [SerializeField] private float minAttackRange = 0f;
    [Tooltip("패링 성공 시 발사한 해골을 그로기로 만들지 (끄면 투사체만 무효화)")]
    [SerializeField] private bool groggyWhenParried = true;
    [Tooltip("muzzle 을 지정하지 않았을 때 몸 앞쪽으로 얼마나 떨어진 곳에서 발사할지")]
    [SerializeField] private float fallbackMuzzleForward = 0.6f;

    // 근접 무기가 없다는 경고를 띄우지 않도록 EnemyBase 에 알림
    protected override bool UsesMeleeWeapon => false;

    private Vector3 MuzzlePosition
        => muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * (eyeHeight * 0.9f) + transform.forward * fallbackMuzzleForward;

    // 기본 조건(시야 + 쿨다운 + 사거리 안) + 너무 가깝지 않을 것 + 발사 위치에서 플레이어까지 장애물 없음
    protected override bool CanAttackNow()
    {
        if (!base.CanAttackNow()) return false;
        if (FlatDistance(target.position) < minAttackRange) return false;

        Vector3 goal = target.position + Vector3.up * targetHeight;
        return !Physics.Linecast(MuzzlePosition, goal, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // Strike 가 시작되는 순간 투사체 발사. 조준 방향은 이 순간의 플레이어 위치로 고정됩니다.
    // (움직이는 플레이어를 앞질러 쏘지 않으므로, 계속 움직이면 피할 수 있음)
    protected override void OnStrikeBegin()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[{name}] Projectile Prefab 이 비어 있습니다.");
            return;
        }
        if (target == null) return;

        Vector3 origin = MuzzlePosition;
        Vector3 goal = target.position + Vector3.up * targetHeight;
        Vector3 dir = (goal - origin).normalized;

        Projectile p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
        p.Launch(damage, transform, dir, projectileSpeed);   // source = 이 해골 (패링 정면 판정 / 그로기 반응용)
    }

    // 패링당했을 때: 옵션에 따라 그로기 여부 결정
    public override void OnParried(Transform parrier)
    {
        if (!groggyWhenParried) return;
        base.OnParried(parrier);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (minAttackRange > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, minAttackRange);
        }
    }
}
