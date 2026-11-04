using System;
using System.Collections.Generic;
using UnityEngine;

// "UI 가 열려 있는가" 를 관리하는 정적 클래스.
//  - 인벤토리/루팅 창 같은 UI 가 Open(this) 하면 커서를 풀고, 모두 Close(this) 하면 다시 잠급니다.
//  - 여러 UI 가 동시에 열릴 수 있어서(인벤토리 + 루팅 창) 열린 UI 를 목록으로 관리합니다.
//  - IsOpen 이 true 인 동안 PlayerController(이동/점프), CameraLook(시점/재잠금), Interactor 가 입력을 무시합니다.
//    공격/패링은 커서가 풀려 있으면 기존 코드가 자동으로 무시합니다.
public static class UIState
{
    private static readonly HashSet<object> owners = new HashSet<object>();

    public static bool IsOpen => owners.Count > 0;

    // 마지막으로 열림/닫힘 상태가 바뀐 프레임 (CameraLook 이 ESC/클릭 충돌과 시점 튐을 막는 데 사용)
    public static int LastChangeFrame { get; private set; } = -100;

    // true = 열림, false = 닫힘
    public static event Action<bool> Changed;

    public static void Open(object owner)
    {
        if (owner == null) return;
        bool was = IsOpen;
        if (!owners.Add(owner)) return;
        if (!was) Apply(true, true);
    }

    // restoreCursor: false 로 닫으면 커서를 다시 잠그지 않습니다 (사망 후 게임오버 화면처럼 커서가 필요할 때)
    public static void Close(object owner, bool restoreCursor = true)
    {
        if (owner == null || !owners.Remove(owner)) return;
        if (!IsOpen) Apply(false, restoreCursor);
    }

    public static void CloseAll(bool restoreCursor = true)
    {
        if (owners.Count == 0) return;
        owners.Clear();
        Apply(false, restoreCursor);
    }

    private static void Apply(bool open, bool touchCursor)
    {
        LastChangeFrame = Time.frameCount;
        if (touchCursor)
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
        Changed?.Invoke(open);
    }

    // 도메인 리로드 끄기 옵션을 써도 이전 플레이의 상태가 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        owners.Clear();
        LastChangeFrame = -100;
        Changed = null;
    }
}
