using UnityEngine;
using UnityEngine.AI;

// 모든 몬스터가 공유하는 FSM 부모 클래스. 직접 붙이지 않고, ZombieAI / WolfAI 같은 자식 클래스를 붙입니다.
// 몬스터마다 다른 수치(체력, 속도, 시야, 공격 타이밍)는 Inspector 에서, 다른 "동작"은 아래 훅을 override 해서 바꿉니다.
//
// 상태 흐름
//  Idle      : 제자리 대기 -> Search
//  Search    : 현재 위치 주변 searchRadius 안에서 랜덤 searchPointCount(3)곳을 차례로 수색 -> 끝나면 Idle
//  Chase     : 시야에 들어온 플레이어를 NavMeshAgent 로 추적. 공격 가능하면 Telegraph
//              (loseSightTime 동안 시야를 놓치면 마지막으로 본 위치부터 Search)
//  Telegraph : 공격 준비(선딜). 색 예고는 없고, 공격 모션의 준비 동작이 이 시간 동안 재생됨
//  Strike    : 이 구간에만 WeaponHitbox 가 켜져 데미지
//  Recover   : 후딜 (무방비)
//  Groggy    : 패링당하면 공격이 끊기고 무방비 + 받는 피해 증가
//  Dead      : 사망
//
// 자식 클래스가 override 할 수 있는 훅 (모두 선택)
//  CanAttackNow()          공격을 시작할 조건 (기본: 시야 + 쿨다운 + attackRange 안)
//  OnTelegraphBegin()      예고 시작 (전용 애니메이션 트리거, 울음소리 등)
//  OnStrikeBegin()         공격 판정 시작 직후
//  OnStrikeUpdate(t)       공격 중 매 프레임 (t = 0~1). 돌진 이동 등
//  OnStrikeEnd()           공격 판정 종료 (패링당해 끊겨도 호출됨)
//  OnGroggyBegin()         그로기 시작
//  OnDeath()               사망
//  OnNoticed()             플레이어를 처음 발견
//  Awake / Start / OnEnable / OnDisable / Update 도 override 가능 (반드시 base.XXX() 호출)
//
// 공통 설정 (Hierarchy)
//  Enemy (Health, 자식 클래스, NavMeshAgent, Collider)  ← 레이어는 Enemy 등
//   └ Model (Animator)
//       └ ... 손/팔/입 └ Hitbox (Collider(Trigger), WeaponHitbox: Owner = Enemy 루트, Hit Mask = Player)
//  - NavMesh 가 Bake 되어 있어야 합니다. (랜덤 던전이면 생성 후 NavMeshSurface.BuildNavMesh())
//  - Obstacle Mask 에는 벽/바닥 레이어만 (Player / Enemy 레이어 제외)
//
// Animator Controller (파라미터가 없으면 해당 연출만 건너뜁니다)
//  Speed (Float, 0=정지 / 1=걷는 중. 달리기 없음) / Attack, Groggy, Hit, Die (Trigger)
//  몬스터 전용 파라미터는 자식에서 SetTrigger(Animator.StringToHash("이름")) 으로 사용
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyBase : MonoBehaviour, IParryable, IDamageInterceptor
{
    public enum State { Idle, Search, Chase, Telegraph, Strike, Recover, Groggy, Dead }

    [Header("Weapon")]
    [SerializeField] protected WeaponHitbox weapon;
    [SerializeField] protected float damage = 25f;             // 플레이어 체력 100 기준 약 4번 피격에 사망

    [Header("Sight (시야)")]
    [SerializeField] protected float sightRange = 10f;
    [Tooltip("시야각 (전체 각도). 110 = 정면 좌우 55도")]
    [SerializeField, Range(10f, 360f)] protected float sightAngle = 110f;
    [Tooltip("이 거리 안에서는 시야각과 상관없이 감지 (등 뒤로 바짝 붙어도 눈치챔)")]
    [SerializeField] protected float closeSenseRange = 2f;
    [Tooltip("추적 중에는 이 거리까지 계속 시야로 인정 (360도)")]
    [SerializeField] protected float chaseSightRange = 14f;
    [Tooltip("시야선을 막는 레이어 (벽, 장애물). Player / Enemy 레이어는 넣지 마세요")]
    [SerializeField] protected LayerMask obstacleMask = ~0;
    [SerializeField] protected float eyeHeight = 1.6f;
    [SerializeField] protected float targetHeight = 1.0f;
    [SerializeField] protected float sensorInterval = 0.1f;
    [Tooltip("추적 중 이 시간(초) 동안 플레이어를 못 보면 추적을 포기하고 수색")]
    [SerializeField] protected float loseSightTime = 3f;

    [Header("Idle / Search (수색)")]
    [SerializeField] protected float idleTimeMin = 1.5f;
    [SerializeField] protected float idleTimeMax = 3f;
    [SerializeField] protected float searchRadius = 8f;
    [SerializeField] protected float searchMinDistance = 2f;
    [SerializeField] protected int searchPointCount = 3;
    [SerializeField] protected float searchSpeed = 1.5f;
    [Tooltip("수색 지점에 도착한 뒤 주변을 둘러보는 시간")]
    [SerializeField] protected float searchLookTime = 1.5f;
    [SerializeField] protected float lookAroundSpeed = 60f;
    [Tooltip("한 지점에 이 시간(초) 안에 도착하지 못하면 다음 지점으로 넘어감 (끼임 방지)")]
    [SerializeField] protected float searchPointTimeout = 8f;

    [Header("Chase")]
    [SerializeField] protected float chaseSpeed = 3f;
    [SerializeField] protected float repathInterval = 0.2f;
    [SerializeField] protected float turnSpeed = 360f;

    [Header("Attack Timing (초)")]
    [SerializeField] protected float attackRange = 1.8f;
    [Tooltip("공격 예고 시간. 패링 윈도우(0.25초)보다 길어야 읽고 대응할 수 있습니다.")]
    [SerializeField] protected float telegraphTime = 0.6f;
    [SerializeField] protected float strikeTime = 0.25f;       // 무기 판정이 켜져 있는 시간
    [SerializeField] protected float recoverTime = 0.7f;
    [SerializeField] protected float cooldown = 1.2f;

    [Header("Groggy (패링당했을 때)")]
    [Tooltip("패링당했을 때 그로기 시간(초)")]
    [SerializeField] protected float groggyTime = 1.5f;
    [SerializeField] protected float groggyDamageMultiplier = 2f;

    [Header("Etc")]
    [SerializeField] protected Animator animator;              // 비우면 자식에서 자동 검색
    [Header("Death (사망)")]
    [Tooltip("사망 모션이 끝난 뒤 오브젝트를 제거할지. 끄면 시체가 남습니다.")]
    [SerializeField] protected bool destroyOnDeath = true;
    [Tooltip("Animator Controller 의 사망 모션 상태 이름")]
    [SerializeField] protected string deathStateName = "Death";
    [Tooltip("사망 모션이 끝난 뒤 시체가 남아 있는 시간(초)")]
    [SerializeField] protected float deathLingerTime = 1.5f;
    [Tooltip("사망 모션이 시작되지 않거나 끝나지 않을 때를 대비한 최대 대기 시간(초)")]
    [SerializeField] protected float deathMaxWait = 8f;

    // ---- 다른 스크립트가 읽을 상태 ----
    public State Current { get; private set; } = State.Idle;
    public bool IsTelegraphing => Current == State.Telegraph;
    public bool IsStriking => Current == State.Strike;
    public bool IsGroggy => Current == State.Groggy;

    // 연출(사운드, 이펙트)이 구독
    public event System.Action Telegraphed;
    public event System.Action StrikeStarted;
    public event System.Action StrikeEnded;
    public event System.Action Groggied;
    public event System.Action Noticed;

    // 자식 클래스가 쓸 수 있는 참조
    protected Health health;
    protected NavMeshAgent agent;
    protected Transform target;
    protected Health targetHealth;
    protected bool canSee;                 // 현재 플레이어가 보이는지
    protected Vector3 lastKnownPos;        // 플레이어를 마지막으로 본 위치
    protected float stateTimer;            // 현재 상태에 들어온 뒤 경과 시간 (Idle/Telegraph/Strike/Recover/Groggy)

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int GroggyHash = Animator.StringToHash("Groggy");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private readonly System.Collections.Generic.HashSet<int> animParams = new System.Collections.Generic.HashSet<int>();

    private float cooldownTimer;
    private float idleDuration;
    private float sensorTimer;
    private float repathTimer;
    private float loseTimer;

    // 수색
    private readonly System.Collections.Generic.List<Vector3> searchPoints = new System.Collections.Generic.List<Vector3>();
    private int searchIndex;
    private bool searchWaiting;
    private float searchWaitTimer;
    private float searchPointTimer;
    private NavMeshPath pathCache;

    // ================= 자식이 override 하는 훅 =================

    // 공격을 시작할 조건. 돌진형은 사거리를 넓히고, 정예는 추가 조건을 붙이는 식으로 바꿉니다.
    protected virtual bool CanAttackNow()
        => canSee && cooldownTimer <= 0f && FlatDistance(target.position) <= attackRange;

    protected virtual void OnNoticed() { }
    protected virtual void OnTelegraphBegin() { }
    protected virtual void OnStrikeBegin() { }
    protected virtual void OnStrikeUpdate(float normalizedTime) { }
    protected virtual void OnStrikeEnd() { }
    protected virtual void OnGroggyBegin() { }
    protected virtual void OnDeath() { }

    // 시야 판정 자체를 바꾸고 싶을 때 (예: 소리 감지 추가)
    protected virtual bool ComputeSight()
    {
        if (!EnsureTarget()) return false;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        bool chasing = Current == State.Chase || Current == State.Telegraph
                    || Current == State.Strike || Current == State.Recover || Current == State.Groggy;
        float range = chasing ? chaseSightRange : sightRange;
        if (dist > range) return false;

        // 추적 중에는 360도, 평소에는 시야각 안 (가까운 거리는 각도 무시)
        if (!chasing && dist > closeSenseRange)
        {
            if (Vector3.Angle(transform.forward, to) > sightAngle * 0.5f) return false;
        }

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 goal = target.position + Vector3.up * targetHeight;
        return !Physics.Linecast(eye, goal, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // ================= Unity 메시지 =================

    protected virtual void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;   // 회전은 이 스크립트가 직접 처리
        pathCache = new NavMeshPath();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weapon == null) weapon = GetComponentInChildren<WeaponHitbox>(true);
        if (weapon == null)
            Debug.LogWarning($"[{name}] WeaponHitbox 를 찾지 못했습니다. 공격 부위에 WeaponHitbox 를 붙여주세요.");

        if (animator != null)
            foreach (var p in animator.parameters) animParams.Add(p.nameHash);
    }

    protected virtual void Start()
    {
        EnterState(State.Idle);
    }

    protected virtual void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    protected virtual void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        if (weapon != null) weapon.SetActive(false);
    }

    // ================= 이벤트 =================

    private void OnDamaged(DamageInfo info)
    {
        if (health.IsDead) return;

        // 수색/대기 중에 맞으면 플레이어 쪽으로 즉시 추적 시작
        if (Current == State.Idle || Current == State.Search)
        {
            if (EnsureTarget())
            {
                lastKnownPos = target.position;
                StartChase();
            }
        }

        // 공격 동작을 끊지 않도록, 이동 중일 때만 피격 모션
        if (Current == State.Idle || Current == State.Search || Current == State.Chase)
            SetTrigger(HitHash);
    }

    private void OnDied()
    {
        EndStrike();            // Strike 중이었다면 StrikeEnded 발생
        EnterState(State.Dead);

        // 남아 있던 다른 트리거가 Die 이후에 발동해 사망 모션을 덮어쓰는 것을 방지
        ResetTrigger(AttackHash);
        ResetTrigger(GroggyHash);
        ResetTrigger(HitHash);
        SetTrigger(DieHash);

        if (agent.enabled) agent.enabled = false;
        OnDeath();
        if (destroyOnDeath) StartCoroutine(DestroyAfterDeath());
    }

    // 패링을 당함 (PlayerParry 가 호출): 공격을 끊고 그로기
    public void OnParried(Transform parrier)
    {
        if (health.IsDead) return;

        EndStrike();   // 아직 Strike 상태일 때 호출해야 StrikeEnded 가 발생
        EnterState(State.Groggy);
        SetTrigger(GroggyHash);
        OnGroggyBegin();
        Groggied?.Invoke();
    }

    // Health.TakeDamage 직전에 호출: 그로기 중에는 받는 피해 증가
    public bool Intercept(ref DamageInfo info)
    {
        if (Current == State.Groggy) info.amount *= groggyDamageMultiplier;
        return true;
    }

    // ================= 메인 루프 =================

    protected virtual void Update()
    {
        if (health.IsDead || Current == State.Dead) return;

        cooldownTimer -= Time.deltaTime;

        sensorTimer -= Time.deltaTime;
        if (sensorTimer <= 0f)
        {
            sensorTimer = sensorInterval;
            canSee = ComputeSight();
        }

        switch (Current)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Search: UpdateSearch(); break;
            case State.Chase: UpdateChase(); break;
            case State.Telegraph: UpdateTelegraph(); break;
            case State.Strike: UpdateStrike(); break;
            case State.Recover: UpdateRecover(); break;
            case State.Groggy: UpdateGroggy(); break;
        }

        UpdateAnimatorSpeed();
    }

    private void EnterState(State s)
    {
        Current = s;
        stateTimer = 0f;

        switch (s)
        {
            case State.Idle:
                StopMoving();
                idleDuration = Random.Range(idleTimeMin, idleTimeMax);
                break;
            case State.Search:
                ResumeMoving(searchSpeed, 0.3f);
                break;
            case State.Chase:
                ResumeMoving(chaseSpeed, attackRange * 0.7f);
                repathTimer = 0f;
                break;
            default:   // Telegraph, Strike, Recover, Groggy, Dead
                StopMoving();
                break;
        }
    }

    // ================= 상태별 처리 =================

    private void UpdateIdle()
    {
        if (canSee) { StartChase(); return; }

        stateTimer += Time.deltaTime;
        if (stateTimer < idleDuration) return;

        EnterState(State.Search);
        BeginSearch(transform.position, false);
    }

    private void UpdateSearch()
    {
        if (canSee) { StartChase(); return; }

        if (searchWaiting)
        {
            transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f);
            searchWaitTimer -= Time.deltaTime;
            if (searchWaitTimer > 0f) return;

            searchWaiting = false;
            searchIndex++;
            if (searchIndex >= searchPoints.Count) EnterState(State.Idle);
            else GoToSearchPoint();
            return;
        }

        searchPointTimer += Time.deltaTime;
        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
        if (arrived || searchPointTimer >= searchPointTimeout)
        {
            searchWaiting = true;
            searchWaitTimer = searchLookTime;
            StopMoving();
            return;
        }

        RotateAlongPath();
    }

    private void UpdateChase()
    {
        if (!EnsureTarget()) { EnterState(State.Idle); return; }

        if (canSee)
        {
            lastKnownPos = target.position;
            loseTimer = 0f;
        }
        else
        {
            loseTimer += Time.deltaTime;
            if (loseTimer >= loseSightTime)
            {
                EnterState(State.Search);
                BeginSearch(lastKnownPos, true);   // 마지막으로 본 위치부터 수색
                return;
            }
        }

        if (CanAttackNow())
        {
            StartTelegraph();
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            if (agent.isOnNavMesh) agent.SetDestination(canSee ? target.position : lastKnownPos);
        }

        if (canSee && FlatDistance(target.position) <= attackRange) FaceTarget();
        else RotateAlongPath();
    }

    private void StartChase()
    {
        if (Current != State.Chase)
        {
            OnNoticed();
            Noticed?.Invoke();
        }
        loseTimer = 0f;
        EnterState(State.Chase);
    }

    private void StartTelegraph()
    {
        EnterState(State.Telegraph);
        ResetTrigger(HitHash);   // 직전에 걸린 피격 트리거가 공격 모션을 덮어쓰지 않도록
        SetTrigger(AttackHash);
        OnTelegraphBegin();
        Telegraphed?.Invoke();
    }

    private void UpdateTelegraph()
    {
        FaceTarget();   // 예고 중에는 계속 조준 (공격이 시작되면 방향 고정)
        stateTimer += Time.deltaTime;
        if (stateTimer < telegraphTime) return;

        EnterState(State.Strike);
        if (weapon != null)
        {
            weapon.BeginSwing(damage);
            weapon.SetActive(true);
        }
        OnStrikeBegin();
        StrikeStarted?.Invoke();
    }

    private void UpdateStrike()
    {
        stateTimer += Time.deltaTime;
        OnStrikeUpdate(Mathf.Clamp01(stateTimer / strikeTime));
        if (stateTimer < strikeTime) return;

        EndStrike();
        EnterState(State.Recover);
    }

    private void UpdateRecover()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer < recoverTime) return;

        cooldownTimer = cooldown;
        ResumeAfterAction();
    }

    private void UpdateGroggy()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer < groggyTime) return;

        cooldownTimer = cooldown;
        ResumeAfterAction();
    }

    // 후딜 / 그로기가 끝난 뒤: 플레이어가 보이면 계속 추적, 아니면 마지막 위치 수색
    private void ResumeAfterAction()
    {
        canSee = ComputeSight();
        if (canSee)
        {
            StartChase();
        }
        else
        {
            EnterState(State.Search);
            BeginSearch(lastKnownPos != Vector3.zero ? lastKnownPos : transform.position, true);
        }
    }

    private void EndStrike()
    {
        if (weapon != null) weapon.SetActive(false);
        if (Current == State.Strike)
        {
            OnStrikeEnd();
            StrikeEnded?.Invoke();
        }
    }

    // ================= 감지 =================

    protected bool EnsureTarget()
    {
        if (target == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player == null) return false;
            target = player.transform;
            targetHealth = player.GetComponent<Health>();
        }
        return targetHealth == null || !targetHealth.IsDead;
    }

    // ================= 수색 =================

    private void BeginSearch(Vector3 center, bool visitCenterFirst)
    {
        searchPoints.Clear();
        searchIndex = 0;
        searchWaiting = false;

        int randomCount = searchPointCount;
        if (visitCenterFirst && NavMesh.SamplePosition(center, out var first, 2f, NavMesh.AllAreas))
        {
            searchPoints.Add(first.position);   // 마지막으로 본 위치를 먼저 확인
            randomCount = Mathf.Max(0, searchPointCount - 1);
        }

        for (int i = 0; i < randomCount; i++)
        {
            if (TryRandomPoint(center, out Vector3 p)) searchPoints.Add(p);
        }

        if (searchPoints.Count == 0)
        {
            EnterState(State.Idle);   // 갈 곳이 없음 (NavMesh 없음 등)
            return;
        }
        GoToSearchPoint();
    }

    private void GoToSearchPoint()
    {
        searchPointTimer = 0f;
        searchWaiting = false;
        agent.isStopped = false;
        if (agent.isOnNavMesh) agent.SetDestination(searchPoints[searchIndex]);
    }

    private bool TryRandomPoint(Vector3 center, out Vector3 result)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(searchMinDistance, Mathf.Max(searchMinDistance, searchRadius));
            Vector3 candidate = center + new Vector3(dir.x, 0f, dir.y) * dist;

            if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) continue;
            // 실제로 걸어서 갈 수 있는 곳인지 확인 (벽 너머 방지)
            if (agent.isOnNavMesh && agent.CalculatePath(hit.position, pathCache)
                && pathCache.status == NavMeshPathStatus.PathComplete)
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    // ================= 이동 / 회전 =================

    protected void StopMoving()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    protected void ResumeMoving(float speed, float stopDistance)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.speed = speed;
        agent.stoppingDistance = stopDistance;
    }

    protected void RotateAlongPath()
    {
        Vector3 v = agent.desiredVelocity;
        v.y = 0f;
        if (v.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(v);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    protected void FaceTarget()
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion want = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    protected float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // ================= 애니메이터 =================

    // 해당 파라미터가 Controller 에 있을 때만 발동 (자식 클래스도 사용 가능)
    protected void SetTrigger(int hash)
    {
        if (animator != null && animParams.Contains(hash)) animator.SetTrigger(hash);
    }

    protected void ResetTrigger(int hash)
    {
        if (animator != null && animParams.Contains(hash)) animator.ResetTrigger(hash);
    }

    // 사망 모션이 끝날 때까지 기다린 뒤, 시체가 잠시 남아 있다가 제거
    private System.Collections.IEnumerator DestroyAfterDeath()
    {
        bool canWaitForClip = animator != null && animParams.Contains(DieHash);
        float waited = 0f;

        while (canWaitForClip && waited < deathMaxWait)
        {
            waited += Time.deltaTime;
            var st = animator.GetCurrentAnimatorStateInfo(0);
            // 전환이 끝나 Death 상태에 들어와 있고, 모션을 끝까지 재생했을 때
            if (!animator.IsInTransition(0) && st.IsName(deathStateName) && st.normalizedTime >= 1f) break;
            yield return null;
        }

        yield return new WaitForSeconds(deathLingerTime);
        Destroy(gameObject);
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator == null || !animParams.Contains(SpeedHash)) return;
        // 달리기 없음: 수색(천천히) / 추적(조금 빠르게) 모두 걷기 모션이므로, 현재 목표 속도 대비 비율로 계산
        // 움직이면 약 1, 멈추면 0 -> Blend Tree 는 Idle(0) / Walk(1) 두 개만 있으면 됨
        float speed = agent.enabled ? agent.velocity.magnitude / Mathf.Max(0.01f, agent.speed) : 0f;
        animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
    }

    // ================= Scene 뷰 표시 =================

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(pos, attackRange);

        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(pos, sightRange);
        Vector3 l = Quaternion.AngleAxis(-sightAngle * 0.5f, Vector3.up) * transform.forward * sightRange;
        Vector3 r = Quaternion.AngleAxis(sightAngle * 0.5f, Vector3.up) * transform.forward * sightRange;
        Gizmos.DrawLine(pos, pos + l);
        Gizmos.DrawLine(pos, pos + r);

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(pos, closeSenseRange);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(pos, searchRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < searchPoints.Count; i++)
                Gizmos.DrawSphere(searchPoints[i] + Vector3.up * 0.2f, i == searchIndex ? 0.3f : 0.15f);
        }
    }
}