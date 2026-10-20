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
//
// 타격 규칙
//  - 기본: 한 스윙(BeginSwing 부터 다음 BeginSwing 까지)에 대상 1명당 1번만 맞춥니다.
//  - 재타격 모드(rehit = true): 한 모션에 여러 번 베는 공격용.
//    칼이 적에서 "빠져나갔다가 다시 닿으면" 새 타격으로 처리합니다. (베기마다 한 번씩 맞음)
//    칼이 흔들려서 한 번 벨 때 여러 번 맞는 것을 막기 위해 rehitInterval 이내의 재타격은 무시합니다.
[RequireComponent(typeof(Collider))]
public class WeaponHitbox : MonoBehaviour
{
    [Tooltip("공격자(플레이어). 비우면 부모에서 PlayerController 를 찾습니다. 자기 자신은 맞추지 않습니다.")]
    [SerializeField] private Transform owner;
    [SerializeField] private LayerMask hitMask = ~0;

    [Tooltip("재타격 모드에서, 같은 대상을 다시 맞출 수 있는 최소 간격(초). 한 번 벨 때 여러 번 맞으면 늘리세요.")]
    [SerializeField] private float rehitInterval = 0.25f;

    // 타격 성공 시 발생 (히트스톱 / 사운드 / 스파크가 구독)
    public event Action<IDamageable, DamageInfo> Hit;

    private Collider col;
    private float damage;
    private bool rehit;
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
    private readonly Dictionary<IDamageable, float> lastHitTime = new Dictionary<IDamageable, float>();
    private readonly HashSet<IDamageable> exited = new HashSet<IDamageable>();   // 맞은 뒤 칼이 한 번 빠져나간 대상

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

    // 새 스윙 시작: 맞은 대상 기록 초기화 + 이번 스윙의 피해량 / 재타격 모드 설정
    public void BeginSwing(float swingDamage, bool allowRehit = false)
    {
        damage = swingDamage;
        rehit = allowRehit;
        hitThisSwing.Clear();
        lastHitTime.Clear();
        exited.Clear();
    }

    // 판정 켜기/끄기
    public void SetActive(bool on)
    {
        if (col == null) return;

        // 판정을 끄는 것도 "빠져나간 것"으로 취급 (켜질 때 다시 닿으면 새 타격)
        if (!on && col.enabled)
        {
            foreach (var target in hitThisSwing) exited.Add(target);
        }
        col.enabled = on;
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);

    // 판정이 켜질 때 이미 겹쳐 있던 대상도 처리
    private void OnTriggerStay(Collider other) => TryHit(other);

    // 칼이 대상에서 빠져나감 -> 재타격 모드에서는 다음에 닿을 때 새 타격으로 인정
    private void OnTriggerExit(Collider other)
    {
        if (!rehit) return;
        var target = other.GetComponentInParent<IDamageable>();
        if (target != null && hitThisSwing.Contains(target)) exited.Add(target);
    }

    private void TryHit(Collider other)
    {
        if (!col.enabled) return;
        if (owner != null && other.transform.IsChildOf(owner)) return;       // 자기 자신 제외
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return;

        if (hitThisSwing.Contains(target))
        {
            // 이미 맞춘 대상: 재타격 모드에서, 칼이 한 번 빠져나갔다 다시 닿았고 간격이 지났을 때만 다시 맞춤
            if (!rehit) return;
            if (!exited.Contains(target)) return;
            if (lastHitTime.TryGetValue(target, out float t) && Time.time - t < rehitInterval) return;
            exited.Remove(target);
        }
        else
        {
            hitThisSwing.Add(target);
        }
        lastHitTime[target] = Time.time;

        Vector3 point = other.bounds.ClosestPoint(transform.position);
        var info = new DamageInfo(damage, owner != null ? owner : transform, point);
        target.TakeDamage(info);
        Hit?.Invoke(target, info);
    }
}