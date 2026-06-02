using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    [Min(1)] public int width = 5;
    [Min(1)] public int depth = 5;
    [Min(0.01f)] public float cellSize = 1f;
    [Min(0.01f)] public float levelHeight = 1f;
    public List<GridLevel> levels = new List<GridLevel>();

    public int LevelCount => levels.Count;

    public void AddLevel()
    {
        EnsureLevelsInitialized();

        GridLevel level = new GridLevel
        {
            WorldY = levels.Count * levelHeight
        };
        level.Resize(width, depth);
        levels.Add(level);
    }

    public bool RemoveTopLevel()
    {
        EnsureLevelsInitialized();

        if (levels.Count == 0)
        {
            Debug.LogWarning($"{nameof(GridSystem)} has no levels to remove.", this);
            return false;
        }

        levels.RemoveAt(levels.Count - 1);
        return true;
    }

    public GridCoord WorldToCell(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        int level = Mathf.RoundToInt(local.y / levelHeight);
        return new GridCoord(x, level, z);
    }

    public Vector3 CellToWorld(GridCoord coord)
    {
        float y = coord.level >= 0 && coord.level < levels.Count
            ? levels[coord.level].WorldY
            : coord.level * levelHeight;

        Vector3 local = new Vector3(
            (coord.x + 0.5f) * cellSize,
            y,
            (coord.z + 0.5f) * cellSize);

        return transform.TransformPoint(local);
    }

    public bool TrySet(GridCoord coord, GridCellData data)
    {
        EnsureLevelsInitialized();

        if (!IsValid(coord))
        {
            Debug.LogWarning($"{nameof(GridSystem)} cannot set invalid coord {coord}.", this);
            return false;
        }

        GridCellData cell = levels[coord.level].GetCell(coord.x, coord.z, width);
        if (cell != null && cell.IsOccupied)
        {
            Debug.LogWarning($"{nameof(GridSystem)} cell {coord} is already occupied.", this);
            return false;
        }

        levels[coord.level].SetCell(coord.x, coord.z, width, data);
        return true;
    }

    public bool TryGet(GridCoord coord, out GridCellData data)
    {
        EnsureLevelsInitialized();

        if (!IsValid(coord))
        {
            data = null;
            return false;
        }

        data = levels[coord.level].GetCell(coord.x, coord.z, width);
        return data != null;
    }

    public bool Clear(GridCoord coord)
    {
        EnsureLevelsInitialized();

        if (!IsValid(coord))
        {
            Debug.LogWarning($"{nameof(GridSystem)} cannot clear invalid coord {coord}.", this);
            return false;
        }

        levels[coord.level].ClearCell(coord.x, coord.z, width);
        return true;
    }

    public void ClearAll()
    {
        EnsureLevelsInitialized();

        for (int i = 0; i < levels.Count; i++)
        {
            levels[i]?.ClearAll();
        }
    }

    public bool IsValid(GridCoord coord)
    {
        return coord.x >= 0
            && coord.x < width
            && coord.z >= 0
            && coord.z < depth
            && coord.level >= 0
            && coord.level < levels.Count;
    }

    public int GetOccupiedCount(int levelIndex)
    {
        EnsureLevelsInitialized();

        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            return 0;
        }

        return levels[levelIndex].CountOccupied();
    }

    public IEnumerable<GridCoord> GetOccupiedCoords()
    {
        EnsureLevelsInitialized();

        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            GridLevel level = levels[levelIndex];
            if (level == null)
            {
                continue;
            }

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCellData cell = level.GetCell(x, z, width);
                    if (cell != null && cell.IsOccupied)
                    {
                        yield return new GridCoord(x, levelIndex, z);
                    }
                }
            }
        }
    }

    public void EnsureLevelsInitialized()
    {
        width = Mathf.Max(1, width);
        depth = Mathf.Max(1, depth);
        cellSize = Mathf.Max(0.01f, cellSize);
        levelHeight = Mathf.Max(0.01f, levelHeight);
        levels ??= new List<GridLevel>();

        for (int i = 0; i < levels.Count; i++)
        {
            levels[i] ??= new GridLevel();
            levels[i].WorldY = i * levelHeight;
            levels[i].Resize(width, depth);
        }
    }

    private void OnValidate()
    {
        EnsureLevelsInitialized();
    }

    private void OnDrawGizmos()
    {
        EnsureLevelsInitialized();

        Gizmos.matrix = transform.localToWorldMatrix;

        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            GridLevel level = levels[levelIndex];
            if (level == null)
            {
                continue;
            }

            float y = level.WorldY;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.75f);
            DrawLevelGrid(y);

            Gizmos.color = new Color(1f, 0.72f, 0.2f, 0.9f);
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCellData cell = level.GetCell(x, z, width);
                    if (cell == null || !cell.IsOccupied)
                    {
                        continue;
                    }

                    Vector3 center = new Vector3((x + 0.5f) * cellSize, y, (z + 0.5f) * cellSize);
                    Vector3 size = new Vector3(cellSize * 0.45f, Mathf.Min(levelHeight * 0.35f, cellSize * 0.35f), cellSize * 0.45f);
                    Gizmos.DrawCube(center, size);
                }
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawLevelGrid(float y)
    {
        float totalWidth = width * cellSize;
        float totalDepth = depth * cellSize;

        for (int x = 0; x <= width; x++)
        {
            float localX = x * cellSize;
            Gizmos.DrawLine(new Vector3(localX, y, 0f), new Vector3(localX, y, totalDepth));
        }

        for (int z = 0; z <= depth; z++)
        {
            float localZ = z * cellSize;
            Gizmos.DrawLine(new Vector3(0f, y, localZ), new Vector3(totalWidth, y, localZ));
        }
    }
}
