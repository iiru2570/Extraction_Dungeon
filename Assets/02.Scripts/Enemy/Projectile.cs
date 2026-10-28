using System;
using UnityEngine;

// 원거리 해골이 쏘는 투사체. 프리팹에 이 컴포넌트만 붙이면 됩니다 (Collider / Rigidbody 불필요).
// 매 프레임 "이번 프레임에 이동할 거리"만큼 SphereCast 를 쏴서 충돌을 검사하므로,
// 투사체가 빨라도 플레이어나 벽을 뚫고 지나가지 않습니다.
//
// 충돌 규칙
//  - damageMask(Player) 에 속한 대상에 닿으면: IDamageable.TakeDamage(DamageInfo) 호출 후 투사체 제거
//      -> Health 가 PlayerParry.Intercept 를 부르므로 패링/가드/무적이 근접 공격과 똑같이 동작
//      -> DamageInfo.source = 발사한 해골의 Transform (PlayerParry 의 정면 판정 + 그로기 반응에 필요)
//  - blockMask(벽/바닥) 에 닿으면: 피해 없이 투사체 제거
//  - lifeTime 이 지나면 제거
//
// 프리팹 설정
//  - 임시 구체(Sphere) 하나 + 이 스크립트. 구체의 Collider 는 지워도 됩니다.
//  - Damage Mask = Player 레이어, Block Mask = 벽/바닥 레이어 (Enemy 레이어는 넣지 마세요)
public class Projectile : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float speed = 11f;
    [Tooltip("충돌 검사용 구의 반지름. 클수록 맞추기 쉬움")]
    [SerializeField] private float radius = 0.15f;
    [Tooltip("이 시간(초)이 지나면 어디에도 닿지 않아도 사라짐")]
    [SerializeField] private float lifeTime = 4f;

    [Header("Collision")]
    [Tooltip("피해를 줄 대상 레이어 (Player)")]
    [SerializeField] private LayerMask damageMask;
    [Tooltip("투사체를 막는 레이어 (벽, 바닥, 장애물). 피해는 주지 않고 사라짐")]
    [SerializeField] private LayerMask blockMask = ~0;

    // 명중 / 벽에 맞음 시 발생 (Day 13 에서 사운드/스파크가 구독). bool = 피해 대상에 맞았는지
    public event Action<Projectile, bool> Impacted;

    private float damage;
    private Transform source;
    private Vector3 direction;
    private float age;
    private bool launched;

    // 발사. speedOverride 가 0 보다 크면 프리팹 속도 대신 사용
    public void Launch(float damage, Transform source, Vector3 direction, float speedOverride = -1f)
    {
        this.damage = damage;
        this.source = source;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        if (speedOverride > 0f) speed = speedOverride;

        transform.rotation = Quaternion.LookRotation(this.direction);
        age = 0f;
        launched = true;

        if (damageMask.value == 0)
            Debug.LogWarning($"[{name}] Damage Mask 가 비어 있습니다. Player 레이어를 지정하세요.");
    }

    private void Update()
    {
        if (!launched) return;

        age += Time.deltaTime;
        if (age >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        LayerMask mask = damageMask | blockMask;

        if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, step,
                mask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            Impact(hit);
            return;
        }

        transform.position += direction * step;
    }

    private void Impact(RaycastHit hit)
    {
        launched = false;   // 한 번만 처리

        bool hitTarget = false;
        if (((1 << hit.collider.gameObject.layer) & damageMask.value) != 0)
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead)
            {
                hitTarget = true;
                // source = 발사한 해골. (해골이 이미 죽어서 파괴됐어도 PlayerParry 가 null 을 처리함)
                target.TakeDamage(new DamageInfo(damage, source, hit.point));
            }
        }

        Impacted?.Invoke(this, hitTarget);
        Destroy(gameObject);
    }
}
