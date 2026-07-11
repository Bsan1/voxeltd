using UnityEngine;

public enum DragPlacement
{
    Single,
    Line
}

public enum BuildSurface
{
    FloorOnly,
    TopOnly,
    FloorOrTop
}

[System.Serializable]
public sealed class BuildingBlockCell
{
    [SerializeField] private Vector3Int localCell = Vector3Int.zero;
    [SerializeField] private bool requiresSupport = true;
    [SerializeField] private bool providesSupport = true;

    public Vector3Int LocalCell => localCell;
    public bool RequiresSupport => requiresSupport;
    public bool ProvidesSupport => providesSupport;

    public BuildingBlockCell()
    {
    }

    public BuildingBlockCell(Vector3Int localCell, bool requiresSupport = true, bool providesSupport = true)
    {
        this.localCell = localCell;
        this.requiresSupport = requiresSupport;
        this.providesSupport = providesSupport;
    }

    public BuildingBlockCell Clone()
    {
        return new BuildingBlockCell(localCell, requiresSupport, providesSupport);
    }
}

[CreateAssetMenu(fileName = "BuildingData", menuName = "Grid/Building Data")]
public class BuildingData : ScriptableObject
{
    public enum AnchorMode
    {
        Auto,
        Manual
    }

    [SerializeField] private string buildingId = "building";
    [SerializeField] private string displayName = "Building";
    [SerializeField] private Vector2Int size = Vector2Int.one;
    [SerializeField] private Vector2Int[] occupiedCells;
    [SerializeField] private BuildingBlockCell[] blockCells;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector2Int centerOffset;
    [SerializeField] private AnchorMode anchorMode = AnchorMode.Auto;
    [SerializeField] private Vector2 manualAnchorOffset;
    [SerializeField] private DragPlacement dragPlacement = DragPlacement.Single;
    [SerializeField] private BuildSurface buildSurface = BuildSurface.FloorOnly;
    [SerializeField] private bool supportsTopPlacement = true;

    public string BuildingId => buildingId;
    public string DisplayName => displayName;
    public Vector2Int Size => CalculateFootprintSize();
    public Vector2Int[] OccupiedCells => occupiedCells;
    public BuildingBlockCell[] BlockCells => blockCells;
    public GameObject Prefab => prefab;
    public Vector2Int CenterOffset => CalculateCenterOffset();
    public AnchorMode Anchor => anchorMode;
    public Vector2 ManualAnchorOffset => manualAnchorOffset;
    public DragPlacement DragPlacement => dragPlacement;
    public BuildSurface BuildSurface => buildSurface;
    public bool SupportsTopPlacement => supportsTopPlacement;

    private void OnValidate()
    {
        size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

        if (string.IsNullOrWhiteSpace(buildingId))
        {
            buildingId = name;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = buildingId;
        }

        if (occupiedCells == null || occupiedCells.Length == 0)
        {
            occupiedCells = blockCells != null && blockCells.Length > 0
                ? CreateFootprintFromBlockCells(blockCells)
                : CreateRectangularFootprint(size);
        }

        if (blockCells == null || blockCells.Length == 0)
        {
            blockCells = CreateBlockCellsFromOccupiedCells(occupiedCells);
        }

        NormalizeBlockCells();
        centerOffset = CalculateCenterOffset();
    }

    public Vector2Int[] GetOccupiedCells()
    {
        if (blockCells != null && blockCells.Length > 0)
        {
            return CreateFootprintFromBlockCells(blockCells);
        }

        if (occupiedCells == null || occupiedCells.Length == 0)
        {
            return CreateRectangularFootprint(size);
        }

        Vector2Int[] copy = new Vector2Int[occupiedCells.Length];
        occupiedCells.CopyTo(copy, 0);
        return copy;
    }

    public BuildingBlockCell[] GetBlockCells()
    {
        BuildingBlockCell[] source = blockCells;
        if (source == null || source.Length == 0)
        {
            source = CreateBlockCellsFromOccupiedCells(GetOccupiedCells());
        }

        BuildingBlockCell[] copy = new BuildingBlockCell[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            copy[i] = source[i] != null
                ? source[i].Clone()
                : new BuildingBlockCell(Vector3Int.zero);
        }

        return copy;
    }

    public void SetAnchorMode(AnchorMode mode)
    {
        anchorMode = mode;
    }

    public void SetManualAnchorOffset(float x, float y)
    {
        manualAnchorOffset = new Vector2(x, y);
    }

    public void SetDragPlacement(DragPlacement placement)
    {
        dragPlacement = placement;
    }

    public void SetBuildSurface(BuildSurface surface)
    {
        buildSurface = surface;
    }

    public void SetSupportsTopPlacement(bool supports)
    {
        supportsTopPlacement = supports;
    }

    public void ConfigureRuntimeRectangular(string id, string nameOverride, int width, int height, GameObject sourcePrefab)
    {
        buildingId = string.IsNullOrWhiteSpace(id) ? "runtime_building" : id;
        displayName = string.IsNullOrWhiteSpace(nameOverride) ? buildingId : nameOverride;
        size = new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
        occupiedCells = CreateRectangularFootprint(size);
        blockCells = CreateBlockCellsFromOccupiedCells(occupiedCells);
        prefab = sourcePrefab;
        anchorMode = AnchorMode.Auto;
        manualAnchorOffset = Vector2.zero;
        dragPlacement = DragPlacement.Single;
        buildSurface = BuildSurface.FloorOnly;
        supportsTopPlacement = true;
        centerOffset = CalculateCenterOffset();
    }

    private Vector2Int CalculateCenterOffset()
    {
        Vector2Int[] cells = GetOccupiedCells();
        if (cells == null || cells.Length == 0)
        {
            return new Vector2Int(size.x / 2, size.y / 2);
        }

        Vector2Int min = cells[0];
        Vector2Int max = cells[0];

        for (int i = 1; i < cells.Length; i++)
        {
            Vector2Int cell = cells[i];
            min = Vector2Int.Min(min, cell);
            max = Vector2Int.Max(max, cell);
        }

        Vector2 center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        Vector2Int bestCell = cells[0];
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < cells.Length; i++)
        {
            float distance = Vector2.SqrMagnitude((Vector2)cells[i] - center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = cells[i];
            }
        }

        return bestCell;
    }

    private Vector2Int CalculateFootprintSize()
    {
        Vector2Int result = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

        if (occupiedCells != null)
        {
            for (int i = 0; i < occupiedCells.Length; i++)
            {
                result.x = Mathf.Max(result.x, occupiedCells[i].x + 1);
                result.y = Mathf.Max(result.y, occupiedCells[i].y + 1);
            }
        }

        if (blockCells != null)
        {
            for (int i = 0; i < blockCells.Length; i++)
            {
                if (blockCells[i] == null)
                {
                    continue;
                }

                Vector3Int cell = blockCells[i].LocalCell;
                result.x = Mathf.Max(result.x, cell.x + 1);
                result.y = Mathf.Max(result.y, cell.z + 1);
            }
        }

        return result;
    }

    private static Vector2Int[] CreateRectangularFootprint(Vector2Int footprintSize)
    {
        int width = Mathf.Max(1, footprintSize.x);
        int height = Mathf.Max(1, footprintSize.y);
        Vector2Int[] cells = new Vector2Int[width * height];
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells[index] = new Vector2Int(x, y);
                index++;
            }
        }

        return cells;
    }

    private static BuildingBlockCell[] CreateBlockCellsFromOccupiedCells(Vector2Int[] cells)
    {
        if (cells == null || cells.Length == 0)
        {
            cells = CreateRectangularFootprint(Vector2Int.one);
        }

        BuildingBlockCell[] result = new BuildingBlockCell[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            result[i] = new BuildingBlockCell(new Vector3Int(cells[i].x, 0, cells[i].y));
        }

        return result;
    }

    private static Vector2Int[] CreateFootprintFromBlockCells(BuildingBlockCell[] cells)
    {
        if (cells == null || cells.Length == 0)
        {
            return CreateRectangularFootprint(Vector2Int.one);
        }

        System.Collections.Generic.List<Vector2Int> result = new System.Collections.Generic.List<Vector2Int>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null)
            {
                continue;
            }

            Vector3Int local = cells[i].LocalCell;
            Vector2Int footprintCell = new Vector2Int(local.x, local.z);
            if (!result.Contains(footprintCell))
            {
                result.Add(footprintCell);
            }
        }

        if (result.Count == 0)
        {
            result.Add(Vector2Int.zero);
        }

        return result.ToArray();
    }

    private void NormalizeBlockCells()
    {
        if (blockCells == null)
        {
            return;
        }

        for (int i = 0; i < blockCells.Length; i++)
        {
            if (blockCells[i] == null)
            {
                blockCells[i] = new BuildingBlockCell(Vector3Int.zero);
            }
        }
    }
}
