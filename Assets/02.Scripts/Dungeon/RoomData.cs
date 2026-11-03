using UnityEngine;

/// <summary>
/// 던전 한 칸(방)의 순수 데이터. MonoBehaviour 가 아니라서 알고리즘만 따로 반복 테스트할 수 있다.
/// Day 4 에서 이 데이터를 보고 프리팹을 고르고 회전시킨다.
/// </summary>
public class RoomData
{
    /// <summary>격자 좌표.</summary>
    public Vector2Int Position;

    /// <summary>방 종류.</summary>
    public RoomType Type = RoomType.Normal;

    /// <summary>연결된 문 방향 (비트마스크).</summary>
    public Direction Doors = Direction.None;

    /// <summary>입구방부터의 경로 거리 (문 기준 BFS). 계산 전에는 -1.</summary>
    public int Distance = -1;

    public RoomData(Vector2Int position)
    {
        Position = position;
    }

    /// <summary>연결된 문 개수.</summary>
    public int DoorCount => Doors.Count();

    /// <summary>막다른 방인지 (문이 1개).</summary>
    public bool IsDeadEnd => DoorCount == 1;

    public bool HasDoor(Direction dir) => (Doors & dir) != 0;

    public override string ToString() => $"{Type} {Position} (거리 {Distance}, 문 {Doors})";
}
