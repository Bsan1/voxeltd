using System.Collections.Generic;
using UnityEngine;

public static class BuildingFootprint
{
    public readonly struct ResolvedBlockCell
    {
        public readonly Vector3Int Cell;
        public readonly BuildingBlockCell Block;

        public ResolvedBlockCell(Vector3Int cell, BuildingBlockCell block)
        {
            Cell = cell;
            Block = block;
        }
    }

    public enum Dir
    {
        Down = 0,
        Left = 1,
        Up = 2,
        Right = 3
    }

    public static List<Vector2Int> GetOccupiedCells(Vector2Int anchorCell, BuildingData data)
    {
        return GetOccupiedCells(anchorCell, data, Dir.Down);
    }

    public static List<Vector2Int> GetOccupiedCells(Vector2Int anchorCell, BuildingData data, Dir dir)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        AddOccupiedCells(anchorCell, data, dir, cells);
        return cells;
    }

    public static void AddOccupiedCells(Vector2Int anchorCell, BuildingData data, ICollection<Vector2Int> destination)
    {
        AddOccupiedCells(anchorCell, data, Dir.Down, destination);
    }

    public static void AddOccupiedCells(Vector2Int anchorCell, BuildingData data, Dir dir, ICollection<Vector2Int> destination)
    {
        if (destination == null)
        {
            return;
        }

        if (data == null)
        {
            destination.Add(anchorCell);
            return;
        }

        Vector2Int[] occupiedCells = data.GetOccupiedCells();

        for (int i = 0; i < occupiedCells.Length; i++)
        {
            destination.Add(anchorCell + RotateLocalCell(occupiedCells[i], data.Size, dir));
        }
    }

    public static List<ResolvedBlockCell> GetOccupiedBlockCells(
        Vector2Int anchorCell,
        int baseLayer,
        BuildingData data,
        Dir dir)
    {
        List<ResolvedBlockCell> cells = new List<ResolvedBlockCell>();
        AddOccupiedBlockCells(anchorCell, baseLayer, data, dir, cells);
        return cells;
    }

    public static void AddOccupiedBlockCells(
        Vector2Int anchorCell,
        int baseLayer,
        BuildingData data,
        Dir dir,
        ICollection<ResolvedBlockCell> destination)
    {
        if (destination == null)
        {
            return;
        }

        if (data == null)
        {
            destination.Add(new ResolvedBlockCell(
                new Vector3Int(anchorCell.x, baseLayer, anchorCell.y),
                new BuildingBlockCell(Vector3Int.zero)));
            return;
        }

        BuildingBlockCell[] blockCells = data.GetBlockCells();
        Vector2Int size = data.Size;
        for (int i = 0; i < blockCells.Length; i++)
        {
            BuildingBlockCell block = blockCells[i];
            if (block == null)
            {
                continue;
            }

            Vector3Int local = RotateLocalBlockCell(block.LocalCell, size, dir);
            destination.Add(new ResolvedBlockCell(
                new Vector3Int(anchorCell.x + local.x, baseLayer + local.y, anchorCell.y + local.z),
                block));
        }
    }

    public static Vector3Int RotateLocalBlockCell(Vector3Int cell, Vector2Int size, Dir dir)
    {
        Vector2Int rotated = RotateLocalCell(new Vector2Int(cell.x, cell.z), size, dir);
        return new Vector3Int(rotated.x, Mathf.Max(0, cell.y), rotated.y);
    }

    public static Vector2Int RotateLocalCell(Vector2Int cell, Vector2Int size, Dir dir)
    {
        int width = Mathf.Max(1, size.x);
        int height = Mathf.Max(1, size.y);

        switch (dir)
        {
            case Dir.Left:
                return new Vector2Int(cell.y, width - 1 - cell.x);
            case Dir.Up:
                return new Vector2Int(width - 1 - cell.x, height - 1 - cell.y);
            case Dir.Right:
                return new Vector2Int(height - 1 - cell.y, cell.x);
            default:
                return cell;
        }
    }

    public static Vector2 RotateOffset(Vector2 offset, Dir dir)
    {
        switch (dir)
        {
            case Dir.Left:
                return new Vector2(-offset.y, offset.x);
            case Dir.Up:
                return -offset;
            case Dir.Right:
                return new Vector2(offset.y, -offset.x);
            default:
                return offset;
        }
    }
}
