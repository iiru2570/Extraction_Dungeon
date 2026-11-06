using UnityEngine;

// 상자: LootContainer 를 그대로 재사용합니다. 컴포넌트를 처음 붙일 때(Reset) 상자용 기본값이 채워집니다.
//  - 시작할 때 드롭을 굴림 (Roll On Death 꺼짐)
//  - 비어도 상자는 사라지지 않음
// 일반 상자 / 고급 상자는 Loot Table 에셋만 다르게 연결해서 구분합니다.
public class Chest : LootContainer
{
    private void Reset()
    {
        rollOnDeath = false;
        title = "상자";
        prompt = "상자 열기";
        interactDuration = 1.2f;
        destroyWhenEmpty = false;
        disableCollidersOnDeath = false;
    }
}
