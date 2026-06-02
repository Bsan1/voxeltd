using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class GridBuildSystem : MonoBehaviour
{
    private const string DefaultLevelDataPath = "Assets/Data/Level_Mock.asset";

    [Header("Data")]
    public GridLevelData levelData;
    public string defaultPlaceableId = "cube";
    public bool seedMockDataIfEmpty = true;

    [Header("Grid")]
    [Min(0.1f)] public float cellSize = 1f;
    [Min(1)] public int gridWidth = 10;
    [Min(1)] public int gridHeight = 10;
    public Transform gridRoot;

    [Header("Input")]
    public bool enableMousePlacement = true;
    public bool allowRightClickRemove = true;

    private readonly Dictionary<Vector2Int, GridCellData> occupiedCells = new Dictionary<Vector2Int, GridCellData>();
    private readonly Dictionary<Vector2Int, GameObject> spawnedCubes = new Dictionary<Vector2Int, GameObject>();

    public int DictionaryCount => occupiedCells.Count;
    public int LevelDataCount => levelData != null ? levelData.Count : 0;

    private void Awake()
    {
        EnsureLevelDataAssigned();
    }

    private void Start()
    {
        EnsureLevelDataAssigned();
        EnsureGridRoot();
        RebuildDictionaryFromLevelData();

        if (seedMockDataIfEmpty && occupiedCells.Count == 0)
        {
            LoadMockData();
        }

        SpawnAllSavedCells();
        LogState("Start");
    }

    private void Update()
    {
        if (!enableMousePlacement)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && TryGetMouseGridCoord(out Vector2Int placeCoord))
        {
            PlaceAt(placeCoord, defaultPlaceableId);
        }

        if (allowRightClickRemove && Input.GetMouseButtonDown(1) && TryGetMouseGridCoord(out Vector2Int removeCoord))
        {
            RemoveAt(removeCoord);
        }
    }

    public bool PlaceAt(int x, int y)
    {
        return PlaceAt(new Vector2Int(x, y), defaultPlaceableId);
    }

    public bool PlaceAt(Vector2Int coord)
    {
        return PlaceAt(coord, defaultPlaceableId);
    }

    public bool PlaceAt(Vector2Int coord, string placeableId)
    {
        if (!IsWithinGrid(coord))
        {
            Debug.LogWarning($"{nameof(GridBuildSystem)} rejected placement outside grid at {coord}.", this);
            return false;
        }

        if (occupiedCells.ContainsKey(coord))
        {
            Debug.LogWarning($"{nameof(GridBuildSystem)} rejected placement on occupied cell {coord}.", this);
            return false;
        }

        GridCellData cell = new GridCellData(coord, placeableId, GridToWorld(coord));
        occupiedCells.Add(coord, cell);
        SpawnCube(cell);
        SyncLevelData();
        LogState($"Placed {cell.placeableId} at {coord}");
        return true;
    }

    // --- Deterministic, bridge-callable entry points -----------------------
    // PlaceAt is overloaded, so reflection-based script.execute can't pick one
    // ("Ambiguous match found"). These uniquely-named methods give validation
    // (and UI) a single resolvable target, and PlaceCell is also a clean API.

    public bool PlaceCell(int x, int y, string placeableId)
    {
        string id = string.IsNullOrEmpty(placeableId) ? defaultPlaceableId : placeableId;
        return PlaceAt(new Vector2Int(x, y), id);
    }

    // Parameterless smoke test for bridge validation: places one cube at a
    // deterministic free cell and returns the resulting snapshot, so a single
    // no-arg call both acts and reports (expect DictionaryCount to grow by 1).
    public string TestPlaceOnce()
    {
        Vector2Int coord = new Vector2Int(7, 7);
        bool ok = PlaceAt(coord, defaultPlaceableId);
        return $"TestPlaceOnce at {coord} -> {(ok ? "PLACED" : "REJECTED")} | {GetDebugSnapshot()}";
    }

    public bool RemoveAt(int x, int y)
    {
        return RemoveAt(new Vector2Int(x, y));
    }

    public bool RemoveAt(Vector2Int coord)
    {
        if (!occupiedCells.Remove(coord))
        {
            Debug.LogWarning($"{nameof(GridBuildSystem)} cannot remove empty cell {coord}.", this);
            return false;
        }

        if (spawnedCubes.TryGetValue(coord, out GameObject cube) && cube != null)
        {
            DestroySpawned(cube);
        }

        spawnedCubes.Remove(coord);
        SyncLevelData();
        LogState($"Removed cell {coord}");
        return true;
    }

    public bool TryGet(Vector2Int coord, out GridCellData cell)
    {
        return occupiedCells.TryGetValue(coord, out cell);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.z / cellSize));
    }

    public Vector3 GridToWorld(Vector2Int coord)
    {
        Vector3 local = new Vector3(
            (coord.x + 0.5f) * cellSize,
            cellSize * 0.5f,
            (coord.y + 0.5f) * cellSize);

        return transform.TransformPoint(local);
    }

    public string GetDebugSnapshot()
    {
        return BuildStateMessage("Snapshot");
    }

    private void LoadMockData()
    {
        Vector2Int[] mockCoords =
        {
            new Vector2Int(1, 1),
            new Vector2Int(3, 2),
            new Vector2Int(5, 4)
        };

        for (int i = 0; i < mockCoords.Length; i++)
        {
            Vector2Int coord = mockCoords[i];
            if (IsWithinGrid(coord) && !occupiedCells.ContainsKey(coord))
            {
                occupiedCells.Add(coord, new GridCellData(coord, $"mock_cube_{i + 1}", GridToWorld(coord)));
            }
        }

        SyncLevelData();
    }

    private void RebuildDictionaryFromLevelData()
    {
        occupiedCells.Clear();

        if (levelData == null)
        {
            Debug.LogWarning($"{nameof(GridBuildSystem)} has no GridLevelData asset assigned.", this);
            return;
        }

        levelData.RebuildDictionary(occupiedCells);
    }

    private void SpawnAllSavedCells()
    {
        foreach (GameObject cube in spawnedCubes.Values)
        {
            if (cube != null)
            {
                DestroySpawned(cube);
            }
        }

        spawnedCubes.Clear();

        foreach (GridCellData cell in occupiedCells.Values)
        {
            SpawnCube(cell);
        }
    }

    private void SpawnCube(GridCellData cell)
    {
        if (cell == null || spawnedCubes.ContainsKey(cell.coord))
        {
            return;
        }

        EnsureGridRoot();

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"GridCube_{cell.coord.x}_{cell.coord.y}_{cell.placeableId}";
        cube.transform.SetParent(gridRoot, true);
        cube.transform.position = cell.worldPos;
        cube.transform.rotation = transform.rotation;
        cube.transform.localScale = Vector3.one * (cellSize * 0.9f);
        spawnedCubes[cell.coord] = cube;
        cell.Occupant = cube;
    }

    private bool TryGetMouseGridCoord(out Vector2Int coord)
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            coord = default;
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        Plane gridPlane = new Plane(transform.up, transform.position);

        if (!gridPlane.Raycast(ray, out float distance))
        {
            coord = default;
            return false;
        }

        coord = WorldToGrid(ray.GetPoint(distance));
        return IsWithinGrid(coord);
    }

    private bool IsWithinGrid(Vector2Int coord)
    {
        return coord.x >= 0 && coord.x < gridWidth && coord.y >= 0 && coord.y < gridHeight;
    }

    private void SyncLevelData()
    {
        if (levelData == null)
        {
            return;
        }

        levelData.WriteFromDictionary(occupiedCells);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
    }

    private void EnsureGridRoot()
    {
        if (gridRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("GridRoot");
        root.transform.SetParent(transform, false);
        gridRoot = root.transform;
    }

    private void EnsureLevelDataAssigned()
    {
#if UNITY_EDITOR
        if (levelData == null)
        {
            levelData = UnityEditor.AssetDatabase.LoadAssetAtPath<GridLevelData>(DefaultLevelDataPath);
        }
#endif
    }

    private void DestroySpawned(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void LogState(string context)
    {
        Debug.Log(BuildStateMessage(context), this);
    }

    private string BuildStateMessage(string context)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(nameof(GridBuildSystem));
        builder.Append(' ');
        builder.Append(context);
        builder.Append(" dictionary count=");
        builder.Append(DictionaryCount);
        builder.Append(" levelData.cells.Count=");
        builder.Append(LevelDataCount);
        builder.Append(" coords=");

        bool wroteCoord = false;
        foreach (Vector2Int coord in occupiedCells.Keys)
        {
            if (wroteCoord)
            {
                builder.Append(", ");
            }

            builder.Append(coord);
            wroteCoord = true;
        }

        if (!wroteCoord)
        {
            builder.Append("<empty>");
        }

        return builder.ToString();
    }

    private void OnDrawGizmos()
    {
        cellSize = Mathf.Max(0.1f, cellSize);
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.45f);

        float width = gridWidth * cellSize;
        float height = gridHeight * cellSize;

        for (int x = 0; x <= gridWidth; x++)
        {
            float localX = x * cellSize;
            Gizmos.DrawLine(new Vector3(localX, 0f, 0f), new Vector3(localX, 0f, height));
        }

        for (int y = 0; y <= gridHeight; y++)
        {
            float localZ = y * cellSize;
            Gizmos.DrawLine(new Vector3(0f, 0f, localZ), new Vector3(width, 0f, localZ));
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void OnValidate()
    {
        EnsureLevelDataAssigned();
    }
}
