using System;
using UnityEngine;

// 예고 -> 공격 -> 후딜을 반복하는 훈련용 적. 패링 / 가드 테스트 상대입니다.
//  - 이동 없이 제자리에서 플레이어를 바라보며, 사거리 안이면 공격
//  - 예고(Telegraph): 몸이 색으로 번쩍임 (패링 유도 공격 = 붉은색 / 일반 공격 = 노란색)
//  - 공격(Strike): SwingPivot 이 휘둘러지고, 그동안만 칼 WeaponHitbox 가 켜져 데미지
//  - 후딜(Recover): 무방비 (나중에 패링 성공 시 그로기로 확장)
//
// 설정 (Hierarchy)
//  Dummy (Health, AttackingDummy, Capsule Collider)
//   └ SwingPivot (빈 오브젝트, 더미 중심 높이)
//       └ Sword (Cube, 앞쪽으로 길게: 예) Position (0, 0, 1) Scale (0.2, 0.2, 1.5), WeaponHitbox)
//  - Sword 의 WeaponHitbox: Owner = Dummy, Hit Mask = Player 레이어만
[RequireComponent(typeof(Health))]
public class AttackingDummy : MonoBehaviour
{
    public enum State { Idle, Telegraph, Strike, Recover }

    [Header("Weapon")]
    [SerializeField] private WeaponHitbox weapon;
    [Tooltip("공격 때 좌우로 휘둘러질 피벗 (선택). 비우면 휘두르는 연출 없이 판정만 켜집니다.")]
    [SerializeField] private Transform swingPivot;
    [SerializeField] private float damage = 25f;               // 기획서: 약 4번 피격에 사망 (플레이어 체력 100 기준)
    [SerializeField] private float swingAngle = 120f;

    [Header("Timing (초)")]
    [Tooltip("공격 예고 시간. 패링 윈도우(0.25초)보다 길어야 읽고 대응할 수 있습니다.")]
    [SerializeField] private float telegraphTime = 0.5f;
    [SerializeField] private float strikeTime = 0.2f;          // 칼 판정이 켜져 있는 시간
    [SerializeField] private float recoverTime = 0.6f;         // 공격 후 무방비 시간
    [SerializeField] private float cooldown = 1.5f;            // 다음 공격 전 대기

    [Header("Targeting")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float turnSpeed = 360f;           // 초당 회전 각도 (예고 중까지만 조준)

    [Header("Telegraph")]
    [Tooltip("true = 패링 유도 공격(붉은 예고), false = 일반 공격(노란 예고)")]
    [SerializeField] private bool parryBait = true;
    [SerializeField] private Color parryBaitColor = Color.red;
    [SerializeField] private Color normalColor = Color.yellow;
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private float hitFlashTime = 0.1f;
    [SerializeField] private Renderer targetRenderer;          // 비우면 자식에서 자동 검색
    [SerializeField] private float respawnDelay = 2f;

    // ---- 다른 스크립트(패링/가드)가 읽을 상태 ----
    public State Current { get; private set; } = State.Idle;
    public bool IsTelegraphing => Current == State.Telegraph;
    public bool IsStriking => Current == State.Strike;
    public bool IsParryBait => parryBait;

    // 연출(사운드, 이펙트)이 구독
    public event Action Telegraphed;
    public event Action StrikeStarted;
    public event Action StrikeEnded;

    private Health health;
    private Transform target;
    private MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorId = Shader.PropertyToID("_Color");          // Built-in

    private float stateTimer;
    private float cooldownTimer;
    private float hitFlashTimer;
    private float respawnTimer;
    private bool colored;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        if (weapon == null) weapon = GetComponentInChildren<WeaponHitbox>(true);
        if (weapon == null)
            Debug.LogWarning($"[{name}] WeaponHitbox 를 찾지 못했습니다. 칼 오브젝트에 WeaponHitbox 를 붙여주세요.");
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        if (weapon != null) weapon.SetActive(false);
    }

    private void OnDamaged(DamageInfo info) => hitFlashTimer = hitFlashTime;

    private void OnDied()
    {
        EndStrike();
        Current = State.Idle;
        respawnTimer = respawnDelay;
    }

    private void Update()
    {
        if (health.IsDead)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                health.ResetHealth();
                cooldownTimer = cooldown;
            }
            ApplyColor();
            return;
        }

        if (target == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }

        hitFlashTimer -= Time.deltaTime;

        switch (Current)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Telegraph: UpdateTelegraph(); break;
            case State.Strike: UpdateStrike(); break;
            case State.Recover: UpdateRecover(); break;
        }

        ApplyColor();
    }

    // ---- 상태별 처리 ----

    private void UpdateIdle()
    {
        FaceTarget();
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f || target == null) return;
        if (FlatDistance(target.position) > attackRange) return;

        Current = State.Telegraph;
        stateTimer = 0f;
        Telegraphed?.Invoke();
    }

    private void UpdateTelegraph()
    {
        FaceTarget();   // 예고 중에는 플레이어를 계속 조준 (공격이 시작되면 방향 고정)
        stateTimer += Time.deltaTime;
        if (stateTimer < telegraphTime) return;

        Current = State.Strike;
        stateTimer = 0f;
        if (weapon != null)
        {
            weapon.BeginSwing(damage);
            weapon.SetActive(true);
        }
        SetSwing(0f);
        StrikeStarted?.Invoke();
    }

    private void UpdateStrike()
    {
        stateTimer += Time.deltaTime;
        SetSwing(Mathf.Clamp01(stateTimer / strikeTime));
        if (stateTimer < strikeTime) return;

        EndStrike();
        Current = State.Recover;
        stateTimer = 0f;
    }

    private void UpdateRecover()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer < recoverTime) return;

        Current = State.Idle;
        cooldownTimer = cooldown;
    }

    private void EndStrike()
    {
        if (weapon != null) weapon.SetActive(false);
        if (swingPivot != null) swingPivot.localRotation = Quaternion.identity;
        if (Current == State.Strike) StrikeEnded?.Invoke();
    }

    // t: 0~1, 왼쪽에서 오른쪽으로 휘두름
    private void SetSwing(float t)
    {
        if (swingPivot == null) return;
        float yaw = Mathf.Lerp(-swingAngle * 0.5f, swingAngle * 0.5f, t);
        swingPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---- 조준 ----

    private void FaceTarget()
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion want = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    private float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // ---- 색 표현 (예고 색 > 피격 번쩍임 > 원래 색) ----

    private void ApplyColor()
    {
        if (targetRenderer == null) return;

        if (health.IsDead)
        {
            SetColor(Color.gray);
        }
        else if (Current == State.Telegraph)
        {
            SetColor(parryBait ? parryBaitColor : normalColor);
        }
        else if (hitFlashTimer > 0f)
        {
            SetColor(hitColor);
        }
        else if (colored)
        {
            targetRenderer.SetPropertyBlock(null);
            colored = false;
        }
    }

    private void SetColor(Color c)
    {
        targetRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        targetRenderer.SetPropertyBlock(block);
        colored = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
