using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GridLevel
{
    [SerializeField] private float worldY;
    [SerializeField] private List<GridCellData> cells = new List<GridCellData>();

    public float WorldY
    {
        get => worldY;
        set => worldY = value;
    }

    public int CellCount => cells.Count;

    public void Resize(int width, int depth)
    {
        int targetCount = Mathf.Max(0, width) * Mathf.Max(0, depth);

        while (cells.Count < targetCount)
        {
            cells.Add(new GridCellData());
        }

        if (cells.Count > targetCount)
        {
            cells.RemoveRange(targetCount, cells.Count - targetCount);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            cells[i] ??= new GridCellData();
        }
    }

    public GridCellData GetCell(int x, int z, int width)
    {
        int index = ToIndex(x, z, width);
        return index >= 0 && index < cells.Count ? cells[index] : null;
    }

    public void SetCell(int x, int z, int width, GridCellData data)
    {
        int index = ToIndex(x, z, width);
        if (index < 0 || index >= cells.Count)
        {
            return;
        }

        cells[index] = data?.Clone() ?? new GridCellData();
    }

    public void ClearCell(int x, int z, int width)
    {
        GetCell(x, z, width)?.Clear();
    }

    public void ClearAll()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i]?.Clear();
        }
    }

    public int CountOccupied()
    {
        int count = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null && cells[i].IsOccupied)
            {
                count++;
            }
        }

        return count;
    }

    public IEnumerable<GridCellData> Cells => cells;

    private static int ToIndex(int x, int z, int width)
    {
        return z * width + x;
    }
}
