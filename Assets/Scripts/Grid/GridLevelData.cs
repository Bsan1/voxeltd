using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GridLevelData", menuName = "Grid/Grid Level Data")]
public class GridLevelData : ScriptableObject
{
    public List<GridCellData> cells = new List<GridCellData>();

    // A placed building as ONE entity (type + where + facing), so a multi-cell
    // building survives save/load as a single building — not loose cells. This is
    // what lets removal, the overlap-fade, and attached data treat it as a unit.
    [System.Serializable]
    public class BuildingRecord
    {
        public string buildingId;
        public int anchorX;
        public int anchorY;
        public int direction;
        public int layer;
    }

    public List<BuildingRecord> buildings = new List<BuildingRecord>();

    public int Count => cells?.Count ?? 0;

    public void RebuildDictionary(Dictionary<Vector2Int, GridCellData> target)
    {
        target.Clear();

        if (cells == null)
        {
            cells = new List<GridCellData>();
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            GridCellData cell = cells[i];
            if (cell == null || string.IsNullOrWhiteSpace(cell.placeableId))
            {
                continue;
            }

            target[cell.coord] = cell.Clone();
        }
    }

    public void WriteFromDictionary(Dictionary<Vector2Int, GridCellData> source)
    {
        cells ??= new List<GridCellData>();
        cells.Clear();

        foreach (KeyValuePair<Vector2Int, GridCellData> entry in source)
        {
            GridCellData cell = entry.Value != null
                ? entry.Value.Clone()
                : new GridCellData(entry.Key, "cube", Vector3.zero);

            cell.coord = entry.Key;
            cell.Occupant = null;
            cells.Add(cell);
        }
    }

    public bool TryGetCell(Vector2Int coord, out GridCellData cell)
    {
        if (cells != null)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null && cells[i].coord == coord)
                {
                    cell = cells[i];
                    return true;
                }
            }
        }

        cell = null;
        return false;
    }
}
