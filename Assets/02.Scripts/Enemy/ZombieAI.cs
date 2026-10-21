using UnityEngine;

// 좀비: 느리게 다가와 팔을 휘두르는 기본 근접 몬스터.
// 공통 FSM(감지 / 추적 / 수색 / 예고 / 공격 / 그로기)은 모두 EnemyBase 가 처리하므로,
// 좀비 전용으로 바꿀 것은 Inspector 수치와 애니메이션뿐입니다.
//
// 권장 시작 수치 (Inspector)
//  Chase Speed 2.5 / Search Speed 1.2 / Sight Range 9 / Sight Angle 100
//  Attack Range 1.8 / Telegraph 0.6 / Strike 0.25 / Recover 0.7 / Damage 25
public class ZombieAI : EnemyBase
{
    // 좀비 전용 동작이 필요하면 아래 훅을 override 하세요. (예시)

    // protected override void OnNoticed()
    // {
    //     SetTrigger(Animator.StringToHash("Roar"));   // 발견 시 울부짖기
    // }
}
