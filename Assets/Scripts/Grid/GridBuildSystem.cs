using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class GridBuildSystem : MonoBehaviour
{
    private const string DefaultLevelDataPath = "Assets/Data/Level_Mock.asset";
    private const string DefaultCellVisualPrefabPath = "Assets/Prefabs/Block_1x1.prefab";

    [Serializable]
    public sealed class CellVisualPrefab
    {
        public string placeableId = "cube";
        public GameObject prefab;
    }

    [Header("Data")]
    public GridLevelData levelData;
    public string defaultPlaceableId = "cube";
    public bool seedMockDataIfEmpty = true;

    [Header("Grid")]
    [Min(0.1f)] public float cellSize = 1f;
    [Min(1)] public int gridWidth = 100;
    [Min(1)] public int gridHeight = 100;
    public Transform gridRoot;

    [Header("Grid Visual")]
    [Tooltip("Unity Grid component whose transform is kept at (origin - CenterOffset) and whose cell size " +
             "mirrors this system's cellSize, so the Floor's rendered grid lines line up exactly with the " +
             "centered logical cells. Created/wired once via SetupGridVisual().")]
    public Grid gridVisual;

    [Header("Floor Visual")]
    [Tooltip("Single floor plane used as the visible grid surface. It is sized from gridWidth/gridHeight.")]
    public Transform floorVisual;
    public Material floorGridMaterial;
    [SerializeField] private float floorYOffset = 0f;

    [Header("Cell Visuals")]
    [Tooltip("Default prefab for one occupied grid cell. If empty in the editor, Assets/Prefabs/Block_1x1.prefab is used when present.")]
    [SerializeField] private GameObject defaultCellVisualPrefab;
    [Tooltip("Optional per-placeable single-cell visual overrides. Multi-cell buildings are still restored by BuildPlacementController as whole prefabs.")]
    [SerializeField] private CellVisualPrefab[] cellVisualPrefabs;
    [SerializeField] private bool usePrimitiveFallback = true;

    [Header("Input")]
    public bool enableMousePlacement = true;
    public bool allowRightClickRemove = true;

    private readonly Dictionary<Vector2Int, GridCellData> occupiedCells = new Dictionary<Vector2Int, GridCellData>();
    private readonly Dictionary<Vector2Int, GameObject> spawnedCubes = new Dictionary<Vector2Int, GameObject>();
#if UNITY_EDITOR
    private bool editorVisualAlignQueued;
#endif

    public int DictionaryCount => occupiedCells.Count;
    public int LevelDataCount => levelData != null ? levelData.Count : 0;
    public Vector2Int GridSize => new Vector2Int(gridWidth, gridHeight);
    private Vector3 CenterOffset => new Vector3(gridWidth, 0f, gridHeight) * cellSize * 0.5f;
    public event Action OccupancyChanged;

    private void Awake()
    {
        EnsureLevelDataAssigned();
    }

    private void Start()
    {
        EnsureLevelDataAssigned();
        EnsureGridRoot();
        EnsureGridVisual();
        EnsureFloorVisual();
        RebuildDictionaryFromLevelData();

        if (seedMockDataIfEmpty && occupiedCells.Count == 0)
        {
            LoadMockData();
        }

        SpawnAllSavedCells();
        OccupancyChanged?.Invoke();
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
        OccupancyChanged?.Invoke();
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

    public bool RemoveCell(int x, int y)
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
        OccupancyChanged?.Invoke();
        LogState($"Removed cell {coord}");
        return true;
    }

    // Wipe the grid to a clean slate: all occupancy, every spawned cube / placed
    // building under gridRoot, and the saved data. Used by the maze test runner so
    // each Play starts from an empty grid regardless of leftover data.
    public void ClearAll()
    {
        occupiedCells.Clear();
        spawnedCubes.Clear();

        if (gridRoot != null)
        {
            for (int i = gridRoot.childCount - 1; i >= 0; i--)
            {
                DestroySpawned(gridRoot.GetChild(i).gameObject);
            }
        }

        if (levelData != null)
        {
            levelData.cells?.Clear();
            levelData.buildings?.Clear();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(levelData);
#endif
        }

        OccupancyChanged?.Invoke();
        LogState("ClearAll");
    }

    public bool TryGet(Vector2Int coord, out GridCellData cell)
    {
        return occupiedCells.TryGetValue(coord, out cell);
    }

    public bool IsOccupied(Vector2Int coord)
    {
        return occupiedCells.ContainsKey(coord);
    }

    public bool InBounds(Vector2Int coord)
    {
        return IsWithinGrid(coord);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        return WorldToGrid(worldPosition);
    }

    public Vector3 CellToWorld(Vector2Int coord)
    {
        return GridToWorld(coord);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition) + CenterOffset;
        return new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.z / cellSize));
    }

    // Continuous (un-floored) grid coordinates of a world point, in cell units.
    // Used to centre a building footprint on the cursor instead of snapping the
    // cursor to a corner cell.
    public Vector2 WorldToGridFloat(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition) + CenterOffset;
        return new Vector2(local.x / cellSize, local.z / cellSize);
    }

    public Vector3 GridToWorld(Vector2Int coord)
    {
        return GridToWorld(coord, Vector2Int.one);
    }

    public Vector3 GridToWorld(Vector2Int coord, Vector2Int footprintSize)
    {
        Vector2Int clampedSize = new Vector2Int(
            Mathf.Max(1, footprintSize.x),
            Mathf.Max(1, footprintSize.y));

        Vector3 local = new Vector3(
            (coord.x + clampedSize.x * 0.5f) * cellSize,
            cellSize * 0.5f,
            (coord.y + clampedSize.y * 0.5f) * cellSize) - CenterOffset;

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
        RefreshCellWorldPositions();
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
        cell.worldPos = GridToWorld(cell.coord);

        GameObject cube = CreateCellVisual(cell.placeableId);
        cube.name = $"GridCell_{cell.coord.x}_{cell.coord.y}_{cell.placeableId}";
        cube.transform.SetParent(gridRoot, true);
        cube.transform.position = cell.worldPos;
        cube.transform.rotation = transform.rotation;
        spawnedCubes[cell.coord] = cube;
        cell.Occupant = cube;
    }

    private GameObject CreateCellVisual(string placeableId)
    {
        GameObject prefab = ResolveCellVisualPrefab(placeableId);
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab);
            instance.transform.localScale = prefab.transform.localScale * cellSize;
            return instance;
        }

        if (!usePrimitiveFallback)
        {
            return new GameObject("GridCellVisual");
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.localScale = Vector3.one * (cellSize * 0.9f);
        return cube;
    }

    private GameObject ResolveCellVisualPrefab(string placeableId)
    {
        if (cellVisualPrefabs != null)
        {
            for (int i = 0; i < cellVisualPrefabs.Length; i++)
            {
                CellVisualPrefab entry = cellVisualPrefabs[i];
                if (entry == null
                    || entry.prefab == null
                    || string.IsNullOrWhiteSpace(entry.placeableId))
                {
                    continue;
                }

                if (string.Equals(entry.placeableId, placeableId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.prefab;
                }
            }
        }

        if (defaultCellVisualPrefab != null)
        {
            return defaultCellVisualPrefab;
        }

#if UNITY_EDITOR
        defaultCellVisualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCellVisualPrefabPath);
        if (defaultCellVisualPrefab != null)
        {
            return defaultCellVisualPrefab;
        }
#endif

        return null;
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

        RefreshCellWorldPositions();
        levelData.WriteFromDictionary(occupiedCells);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
    }

    private void RefreshCellWorldPositions()
    {
        foreach (GridCellData cell in occupiedCells.Values)
        {
            if (cell != null)
            {
                cell.worldPos = GridToWorld(cell.coord);
            }
        }
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

    // Creates (or adopts) the dedicated Unity Grid that anchors the Floor's
    // rendered lines, retires any legacy Grid that sat on this object at the
    // wrong (un-centered) origin, and points the Floor's GridLineDriver at it.
    // Safe to call repeatedly. Also exposed so an editor/bridge call can wire
    // the scene without entering Play mode.
    public void SetupGridVisual()
    {
        EnsureGridVisual();
        EnsureFloorVisual();
        AlignGridVisual();
        AlignFloorVisual();
    }

    private void EnsureGridVisual()
    {
        // A Grid sitting directly on this object renders its lines from the
        // logical origin, not the centered corner, so it must not drive the
        // Floor. Remember it for removal once the proper one exists.
        Grid legacy = GetComponent<Grid>();

        if (gridVisual == null)
        {
            Transform existing = transform.Find("GridVisual");
            if (existing != null)
            {
                gridVisual = existing.GetComponent<Grid>();
            }
        }

        if (gridVisual == null)
        {
            GameObject go = new GameObject("GridVisual");
            go.transform.SetParent(transform, false);
            gridVisual = go.AddComponent<Grid>();
        }

        // The Floor's line driver must read its origin/cell size from the
        // correctly-placed Grid.
        foreach (GridLineDriver driver in FindObjectsByType<GridLineDriver>(FindObjectsSortMode.None))
        {
            driver.grid = gridVisual;
        }

        if (legacy != null && legacy != gridVisual)
        {
            if (Application.isPlaying)
            {
                Destroy(legacy);
            }
            else
            {
                DestroyImmediate(legacy);
            }
        }

        AlignGridVisual();
        AlignFloorVisual();
    }

    // Keeps the visual Grid's transform at (origin - CenterOffset) and its cell
    // size in lockstep with this system, so the rendered lines always match the
    // centered cells even as gridWidth/gridHeight/cellSize change.
    private void AlignGridVisual()
    {
        if (gridVisual == null)
        {
            return;
        }

        Transform t = gridVisual.transform;
        t.position = transform.TransformPoint(-CenterOffset);
        t.rotation = transform.rotation;
        gridVisual.cellSize = new Vector3(cellSize, cellSize, 0f);
    }

    private void EnsureFloorVisual()
    {
        if (floorVisual == null)
        {
            Transform existing = transform.Find("Floor");
            if (existing != null)
            {
                floorVisual = existing;
            }
        }

        if (floorVisual == null)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);
            floorVisual = floor.transform;
        }

        if (!floorVisual.TryGetComponent(out GridLineDriver driver))
        {
            driver = floorVisual.gameObject.AddComponent<GridLineDriver>();
        }

        driver.grid = gridVisual;

        if (floorGridMaterial == null)
        {
            floorGridMaterial = LoadFloorGridMaterial();
        }

        if (floorGridMaterial != null && floorVisual.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = floorGridMaterial;
        }

        AlignFloorVisual();
    }

    private Material LoadFloorGridMaterial()
    {
#if UNITY_EDITOR
        Material floorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FloorGrid.mat");
        if (floorMaterial != null)
        {
            return floorMaterial;
        }

        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GridLines.mat");
#else
        return null;
#endif
    }

    private void AlignFloorVisual()
    {
        if (floorVisual == null)
        {
            return;
        }

        floorVisual.localPosition = new Vector3(0f, floorYOffset, 0f);
        floorVisual.localRotation = Quaternion.identity;
        floorVisual.localScale = new Vector3(
            Mathf.Max(0.0001f, gridWidth * cellSize / 10f),
            1f,
            Mathf.Max(0.0001f, gridHeight * cellSize / 10f));
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

    private void OnValidate()
    {
        EnsureLevelDataAssigned();
        // Keep the rendered grid lines aligned as gridWidth/gridHeight/cellSize
        // change in the inspector. Grid.cellSize sends Unity grid-dimension
        // messages, so in edit mode we defer the sync until after OnValidate.
        // Creation/migration happens in SetupGridVisual()/Start to stay clear
        // of OnValidate's restrictions on spawning objects.
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorVisualAlign();
            return;
        }
#endif
        AlignGridVisual();
        AlignFloorVisual();
    }

#if UNITY_EDITOR
    private void QueueEditorVisualAlign()
    {
        if (editorVisualAlignQueued)
        {
            return;
        }

        editorVisualAlignQueued = true;
        UnityEditor.EditorApplication.delayCall += ApplyQueuedEditorVisualAlign;
    }

    private void ApplyQueuedEditorVisualAlign()
    {
        editorVisualAlignQueued = false;
        if (this == null)
        {
            return;
        }

        AlignGridVisual();
        AlignFloorVisual();
    }
#endif
}
