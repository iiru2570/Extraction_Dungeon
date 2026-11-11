using UnityEngine;

// 정예 몬스터 (Day 7): 해골 근접을 강화한 변형.
// Day 1 결정대로 코드는 일반 해골과 같고, 차이는 Inspector 수치(체력/피해/속도/타이밍)와 모습(EliteVisual)뿐입니다.
// 별도 클래스로 둔 이유: 나중에 정예 전용 패턴이 필요해지면 여기에 override 를 추가하기 위해서입니다.
//
// 권장 수치 (일반 해골 근접 대비): Health 200 / Damage 35 / Chase Speed 3.8 / Sight 12 /
//   Attack Range 2.2 / Telegraph 0.6 / Strike 0.3 / Recover 0.6 / Cooldown 1.0 / Groggy Time 1.2
//   (Telegraph 는 패링 윈도우 0.25초보다 길게 유지: 읽고 패링할 수 있어야 함)
public class EliteAI : SkeletonMeleeAI
{
}
