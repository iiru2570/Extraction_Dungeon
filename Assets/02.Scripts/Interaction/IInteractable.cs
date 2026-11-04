using UnityEngine;

// E 로 상호작용할 수 있는 오브젝트가 구현하는 인터페이스 (시체/상자/포탈 등).
// 반드시 MonoBehaviour 에 구현하고, 콜라이더가 "Interactable" 레이어에 있어야 Interactor 가 찾습니다.
// (콜라이더가 자식에 있어도 됩니다: 부모에서 이 컴포넌트를 찾습니다)
public interface IInteractable
{
    // 화면에 표시할 문구 (예: "철검 줍기"). 앞의 "[E]" 는 UI 가 붙입니다.
    string Prompt { get; }

    // E 를 누르고 있어야 하는 시간(초). 0 이면 누르는 즉시 완료
    float InteractDuration { get; }

    // 거리 계산에 쓰는 위치 (보통 transform.position)
    Vector3 InteractPosition { get; }

    // 지금 상호작용할 수 있는지 (이미 비어 있는 상자 등은 false)
    bool CanInteract(Interactor who);

    // 게이지가 다 차서 상호작용이 완료됐을 때 호출
    void OnInteracted(Interactor who);
}
