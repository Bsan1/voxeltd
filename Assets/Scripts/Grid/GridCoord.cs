using System;
using UnityEngine;

[Serializable]
public struct GridCoord : IEquatable<GridCoord>
{
    public int x;
    public int level;
    public int z;

    public GridCoord(int x, int level, int z)
    {
        this.x = x;
        this.level = level;
        this.z = z;
    }

    public bool Equals(GridCoord other)
    {
        return x == other.x && level == other.level && z == other.z;
    }

    public override bool Equals(object obj)
    {
        return obj is GridCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + level;
            hash = hash * 31 + z;
            return hash;
        }
    }

    public override string ToString()
    {
        return $"({x}, {level}, {z})";
    }

    public static bool operator ==(GridCoord left, GridCoord right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridCoord left, GridCoord right)
    {
        return !left.Equals(right);
    }

    public Vector3Int ToVector3Int()
    {
        return new Vector3Int(x, level, z);
    }
}
