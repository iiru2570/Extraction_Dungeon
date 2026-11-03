using System;
using UnityEngine;

/// <summary>방 종류. 기획서 2.4 의 입구/일반/보물/보스.</summary>
public enum RoomType
{
    Entrance,
    Normal,
    Treasure,
    Boss
}

/// <summary>
/// 문 슬롯 4방향 (비트마스크).
/// 격자 좌표 기준: North = +y, East = +x, South = -y, West = -x.
/// Day 4 에서 방 회전과 연결 안 된 문 막기에 그대로 사용한다.
/// </summary>
[Flags]
public enum Direction
{
    None = 0,
    North = 1,
    East = 2,
    South = 4,
    West = 8
}

/// <summary>Direction 보조 함수 모음.</summary>
public static class DirectionUtil
{
    /// <summary>단일 방향 4개 (순회용).</summary>
    public static readonly Direction[] All =
    {
        Direction.North, Direction.East, Direction.South, Direction.West
    };

    /// <summary>방향 → 격자 오프셋. 단일 방향만 지원.</summary>
    public static Vector2Int ToOffset(this Direction d)
    {
        switch (d)
        {
            case Direction.North: return new Vector2Int(0, 1);
            case Direction.East: return new Vector2Int(1, 0);
            case Direction.South: return new Vector2Int(0, -1);
            case Direction.West: return new Vector2Int(-1, 0);
            default: return Vector2Int.zero;
        }
    }

    /// <summary>반대 방향. 단일 방향만 지원.</summary>
    public static Direction Opposite(this Direction d)
    {
        switch (d)
        {
            case Direction.North: return Direction.South;
            case Direction.East: return Direction.West;
            case Direction.South: return Direction.North;
            case Direction.West: return Direction.East;
            default: return Direction.None;
        }
    }

    /// <summary>켜져 있는 문 개수.</summary>
    public static int Count(this Direction d)
    {
        int n = 0;
        foreach (var dir in All)
            if ((d & dir) != 0) n++;
        return n;
    }
}
