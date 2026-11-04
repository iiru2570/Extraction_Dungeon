using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 던전 NavMesh 런타임 빌드 (Day 4).
/// 방 프리팹들이 생성된 뒤 DungeonGenerator 가 Build() 를 호출한다.
/// GeneratedRooms 오브젝트에 NavMeshSurface 와 함께 붙인다 (방들이 이 오브젝트의 자식으로 생성됨).
/// 에디터에서 수동으로 Bake 하지 않는다 (런타임 빌드가 기준).
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class DungeonNavMesh : MonoBehaviour
{
    [SerializeField] private NavMeshSurface surface;

    /// <summary>마지막 빌드가 끝났는지.</summary>
    public bool IsBuilt { get; private set; }

    private void Reset()
    {
        surface = GetComponent<NavMeshSurface>();
    }

    private void Awake()
    {
        if (surface == null) surface = GetComponent<NavMeshSurface>();
    }

    /// <summary>NavMesh 를 새로 빌드한다 (동기 처리). 이전 데이터는 먼저 제거한다.</summary>
    public bool Build()
    {
        if (surface == null) surface = GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            Debug.LogError("[DungeonNavMesh] NavMeshSurface 컴포넌트가 없습니다.", this);
            return false;
        }

        var sw = Stopwatch.StartNew();
        surface.RemoveData();
        surface.BuildNavMesh();
        sw.Stop();

        IsBuilt = true;
        Debug.Log($"[DungeonNavMesh] NavMesh 빌드 완료 ({sw.ElapsedMilliseconds} ms)");
        return true;
    }
}
