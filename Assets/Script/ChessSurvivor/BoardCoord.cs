using System;
using UnityEngine;

[Serializable]
public struct BoardCoord : IEquatable<BoardCoord>
{
    public int x;
    public int y;

    public BoardCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool Equals(BoardCoord other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is BoardCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (x * 397) ^ y;
        }
    }

    public static BoardCoord operator +(BoardCoord a, BoardCoord b)
    {
        return new BoardCoord(a.x + b.x, a.y + b.y);
    }

    public override string ToString()
    {
        return $"({x},{y})";
    }
}
