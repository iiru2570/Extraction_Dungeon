using UnityEngine;

// 해골 병사 (근접): 좀비와 같은 구조의 기본 근접 몬스터.
// 공통 FSM(감지 / 추적 / 수색 / 예고 / 공격 / 그로기)은 모두 EnemyBase 가 처리하므로,
// 좀비와의 차이는 Inspector 수치와 모델/애니메이션뿐입니다.
// (정예 몬스터가 이 클래스를 기반으로 만들어질 예정이라 클래스는 남겨둡니다.)
//
// 권장 시작 수치 (좀비보다 빠르고, 공격 템포가 짧음)
//  Health(Max) 80 (플레이어 약공격 ComboSlash 한 번 = 30 -> 약 3번)
//  Chase Speed 3.2 / Search Speed 1.5 / Sight Range 10 / Sight Angle 110
//  Attack Range 2.0 / Telegraph 0.7 / Strike 0.3 / Recover 0.8 / Cooldown 1.5 / Damage 25
//  - Telegraph 는 패링 윈도우(0.25초)보다 길어야 하며, 공격 모션의 "준비 동작"이 이 시간 안에 끝나고
//    휘두르는 순간이 Strike 시작과 맞도록 애니메이션을 맞추세요.
//
// Animator (몬스터 공통): Speed(Float), Attack / Groggy / Hit / Die (Trigger), 사망 상태 이름 Death
public class SkeletonMeleeAI : EnemyBase
{
    // 해골 전용 동작이 필요하면 아래 훅을 override 하세요. (예시)

    // protected override void OnStrikeUpdate(float t)
    // {
    //     // 공격 중 돌진 등
    // }
}