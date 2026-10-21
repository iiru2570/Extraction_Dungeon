using System;
using UnityEngine;

// 플레이어가 피해를 입었을 때의 반응: 피격 모션 + 짧은 경직 + 짧은 무적 + 진행 중이던 공격 끊기
// Player 오브젝트에 Health, PlayerCombat, PlayerParry 와 함께 붙입니다.
//
// 규칙
//  - 실제로 피해를 입었을 때만 반응합니다 (패링으로 무효가 된 공격은 Health 가 Damaged 를 발생시키지 않음)
//  - 가드 중(IsGuarding)에 맞으면 피해만 받고 모션은 없습니다
//  - 경직(Stagger Time) 동안은 공격 / 패링 / 점프 / 이동이 막힙니다
//  - 맞은 직후 무적(Invulnerable Time) 동안은 추가 피해를 받지 않습니다 (연속 타격·스턴락 방지)
//  - (선택) Super Armor During Counter 를 켜면 패링 후 반격 중에는 맞아도 끊기지 않습니다 (피해는 받음)
//
// Animator Controller 필요 사항
//  - 파라미터 : Hit (Trigger)
//  - 상태     : Hit
//  - 전환     : Any State -> Hit (조건 Hit, Has Exit Time 끄기, Can Transition To Self 끄기, Transition Duration 0.05)
//               Hit -> Idle (Has Exit Time 켜기, Transition Duration 0.1 정도)
//  - Hit 에서 빠져나오지 못하면 Update 의 안전장치가 Idle 상태로 강제 복귀시킵니다. (Idle 상태 이름을 Inspector 에 맞춰 주세요)
//  - 경직 시간은 모션이 아니라 이 스크립트의 Stagger Time 으로 정해집니다. Hit 클립이 길면 Hit 상태의 Speed 를 올려서 맞추세요.
[RequireComponent(typeof(Health))]
public class PlayerHitReaction : MonoBehaviour
{
    [SerializeField] private Animator animator;   // X Bot 의 Animator (비우면 자식에서 자동 검색)

    [Header("Stagger")]
    [Tooltip("맞은 뒤 행동(공격/패링/점프/이동)이 막히는 시간(초)")]
    [SerializeField] private float staggerTime = 0.4f;
    [Tooltip("맞은 직후 추가 피해를 받지 않는 시간(초). 경직 시간보다 길게 두는 것을 권장")]
    [SerializeField] private float invulnerableTime = 0.6f;
    [Tooltip("켜면 패링 후 반격(ComboSlash) 중에는 맞아도 모션이 끊기지 않습니다 (피해는 받음)")]
    [SerializeField] private bool superArmorDuringCounter = false;

    [Header("Idle 복귀 (안전장치)")]
    [Tooltip("Animator Controller 의 피격 모션 상태 이름")]
    [SerializeField] private string hitStateName = "Hit";
    [Tooltip("피격이 끝나고 돌아갈 Idle 상태 이름. Hit -> Idle 전환이 없거나 막혀 있을 때 강제로 이 상태로 복귀시킵니다.")]
    [SerializeField] private string idleStateName = "Great Sword Idle";
    [Tooltip("경직이 끝난 뒤에도 Hit 상태에 머물러 있을 때, 이 시간(초)을 더 기다려도 안 나오면 강제로 Idle 로 보냅니다.")]
    [SerializeField] private float returnGrace = 0.3f;

    // 경직 중인지 (PlayerCombat / PlayerParry 가 참조)
    public bool IsStaggered => Time.time - lastHitTime < staggerTime;

    // 경직이 시작될 때 발생 (피격 사운드, 화면 효과가 구독)
    public event Action Staggered;

    private static readonly int HitHash = Animator.StringToHash("Hit");   // Trigger

    private Health health;
    private PlayerCombat combat;   // 없어도 동작함
    private PlayerParry parry;     // 없어도 동작함
    private bool hasHitParam;
    private float lastHitTime = -10f;

    private void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        parry = GetComponent<PlayerParry>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == HitHash) hasHitParam = true;
        }
        if (!hasHitParam)
            Debug.LogWarning("[PlayerHitReaction] Animator 에 'Hit'(Trigger) 파라미터가 없습니다. 피격 모션이 재생되지 않습니다.");
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
    }

    private void Update()
    {
        if (animator == null || IsStaggered) return;

        // 경직이 끝났는데 아직 Hit 상태에 머물러 있으면 Idle 로 강제 복귀
        // (Hit -> Idle 전환이 없거나, Has Exit Time 조건이 안 맞아 갇힌 경우 대비)
        if (Time.time - lastHitTime < staggerTime + returnGrace) return;
        if (animator.IsInTransition(0)) return;

        if (animator.GetCurrentAnimatorStateInfo(0).IsName(hitStateName))
            animator.CrossFadeInFixedTime(idleStateName, 0.1f);
    }

    // 피해가 실제로 적용된 뒤(가드 감소 등 보정 후)에 호출됨
    private void OnDamaged(DamageInfo info)
    {
        if (health.IsDead) return;
        if (parry != null && parry.IsGuarding) return;                                     // 가드 중에는 모션 없음
        if (superArmorDuringCounter && combat != null && combat.IsCountering) return;      // 반격 중 슈퍼 아머

        lastHitTime = Time.time;
        health.SetInvulnerable(invulnerableTime);

        // 진행 중이던 공격과 대기 중인 입력을 끊음
        if (combat != null) combat.CancelAttack();

        if (animator != null && hasHitParam) animator.SetTrigger(HitHash);
        Staggered?.Invoke();
    }
}