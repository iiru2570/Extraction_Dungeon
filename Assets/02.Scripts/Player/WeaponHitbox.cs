using System;
using System.Collections.Generic;
using UnityEngine;

// 손에 붙은 칼(무기)의 트리거 콜라이더가 실제로 데미지를 주는 컴포넌트.
// 칼 오브젝트(손 본의 자식)에 붙입니다.
//
// 설정
//  - 칼에 Collider 추가 + Is Trigger 체크 (이 스크립트가 자동으로도 켬)
//  - Rigidbody 는 자동 추가됨 (Is Kinematic, Use Gravity 꺼짐). 트리거 이벤트에 필요합니다.
//  - 평소에는 콜라이더가 꺼져 있고, PlayerCombat 이 공격 판정 구간에만 켭니다.
[RequireComponent(typeof(Collider))]
public class WeaponHitbox : MonoBehaviour
{
    [Tooltip("공격자(플레이어). 비우면 부모에서 PlayerController 를 찾습니다. 자기 자신은 맞추지 않습니다.")]
    [SerializeField] private Transform owner;
    [SerializeField] private LayerMask hitMask = ~0;

    // 타격 성공 시 발생 (히트스톱 / 사운드 / 스파크가 구독)
    public event Action<IDamageable, DamageInfo> Hit;

    private Collider col;
    private float damage;
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    public bool IsActive => col != null && col.enabled;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 빠르게 휘둘러도 놓치지 않도록

        if (owner == null)
        {
            var pc = GetComponentInParent<PlayerController>();
            owner = pc != null ? pc.transform : null;
        }
    }

    // 새 스윙 시작: 맞은 대상 기록 초기화 + 이번 스윙의 피해량 설정
    public void BeginSwing(float swingDamage)
    {
        damage = swingDamage;
        hitThisSwing.Clear();
    }

    // 판정 켜기/끄기
    public void SetActive(bool on)
    {
        if (col != null) col.enabled = on;
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);

    // 판정이 켜질 때 이미 겹쳐 있던 대상, 다음 스윙에서 계속 겹쳐 있는 대상도 처리
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        if (!col.enabled) return;
        if (owner != null && other.transform.IsChildOf(owner)) return;       // 자기 자신 제외
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return;
        if (!hitThisSwing.Add(target)) return;                                // 한 스윙당 1번

        Vector3 point = other.bounds.ClosestPoint(transform.position);
        var info = new DamageInfo(damage, owner != null ? owner : transform, point);
        target.TakeDamage(info);
        Hit?.Invoke(target, info);
    }
}
