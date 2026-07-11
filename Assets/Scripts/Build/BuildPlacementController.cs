using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(50)]
public sealed class BuildPlacementController : MonoBehaviour
{
    private const int AutoLayer = -1;
    private const int FloorLayer = 0;
    private const int MaxBuildLayer = 8;
    private const float RaycastDistance = 10000f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int FillStrengthId = Shader.PropertyToID("_FillStrength");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    private const string DefaultGhostMaterialPath = "Assets/Materials/GhostHologram.mat";
    private const float MaxVisibleFadeAlpha = 0.35f;

    [Header("References")]
    [SerializeField] private GridBuildSystem gridBuildSystem;
    [SerializeField] private Camera activeCamera;
    [SerializeField] private Material ghostPreviewMaterial;

    [Header("Buildings")]
    [SerializeField] private BuildingData[] buildings;
    [SerializeField, Min(0)] private int selectedIndex;
    [SerializeField] private BuildingFootprint.Dir selectedDirection = BuildingFootprint.Dir.Down;

    [Header("Ghost Colors")]
    [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Header("Ghost")]
    [SerializeField, Tooltip("Extra height (world units) the ghost floats above the grid/placed blocks so it stays visible and never z-fights with them.")]
    private float ghostHeightOffset = 0.05f;
    [SerializeField, Tooltip("Ghost hologram fill/glow when placement is VALID.")]
    private float validGhostFill = 0.12f;
    [SerializeField] private float validGhostGlow = 0.8f;
    [SerializeField, Tooltip("Ghost hologram fill/glow when placement is INVALID; lower = a faded, see-through red instead of a solid block.")]
    private float invalidGhostFill = 0.04f;
    [SerializeField] private float invalidGhostGlow = 0.25f;

    [Header("Placement Overlap Fade")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = MaxVisibleFadeAlpha;

    [Header("Input")]
    [SerializeField] private bool disableGridBuildSystemMousePlacement = true;
    [SerializeField] private KeyCode buildModeToggleKey = KeyCode.B;
    [SerializeField] private bool buildModeActive = true;

    private readonly Dictionary<Vector3Int, PlacedBlockCell> occupiedBlocksByCell = new Dictionary<Vector3Int, PlacedBlockCell>();
    private readonly List<PlacedBuilding> placedBuildings = new List<PlacedBuilding>();
    private readonly List<Renderer> rendererScratch = new List<Renderer>();
    private readonly List<Renderer> boundsScratch = new List<Renderer>();
    private readonly List<Renderer> ghostRenderers = new List<Renderer>();
    private readonly List<Material> ghostMaterialInstances = new List<Material>();
    private readonly List<Renderer> removeGhostRenderers = new List<Renderer>();
    private readonly List<Material> removeGhostMaterialInstances = new List<Material>();
    private readonly List<Material> lineStretchMaterials = new List<Material>();
    private readonly List<Vector2Int> linePlacementCells = new List<Vector2Int>();
    private readonly List<PlacementQuery> linePlacementQueries = new List<PlacementQuery>();
    private readonly List<RemoveTarget> lineRemovalTargets = new List<RemoveTarget>();
    private readonly List<PlacedBuilding> placedTransactionScratch = new List<PlacedBuilding>();
    private readonly List<PlacedBuilding> cascadeScratch = new List<PlacedBuilding>();
    private readonly HashSet<PlacedBuilding> cascadeSetScratch = new HashSet<PlacedBuilding>();
    private readonly HashSet<PlacedBuilding> removeTargetBuildingSet = new HashSet<PlacedBuilding>();
    private readonly HashSet<Vector2Int> removeTargetGridSet = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector3Int, BlockPlacementCell> validationCellsScratch = new Dictionary<Vector3Int, BlockPlacementCell>();
    private readonly Dictionary<Renderer, RemovePaintState> removePaintOriginalBlocks = new Dictionary<Renderer, RemovePaintState>();

    private GameObject ghostInstance;
    private GameObject removeGhostInstance;
    private GameObject lineStretchGhost;
    private MaterialPropertyBlock removePaintBlock;
    private BuildingData selectedBuilding;
    private PlacedBuilding currentRemovePreviewBuilding;
    private bool currentCanPlace;
    private bool hadPreviousGridMousePlacement;
    private bool previousGridMousePlacement;
    private bool suppressFadeUntilGhostMoves;
    private bool linePlacementDragActive;
    private bool removalDragActive;
    private bool hasLastRemovalDragCell;
    private bool hasLineRemovalStartCell;
    private bool removeToolSelected;
    private int linePlacementTargetLayer = AutoLayer;
    private int nextPlacedBuildingId = 1;
    private Vector2Int suppressFadeAnchorCell;
    private Vector2Int currentGhostAnchorCell;
    private Vector2Int linePlacementStartCell;
    private Vector2Int lineRemovalStartCell;
    private Vector2Int lastRemovalDragCell;

    public event Action PathfindingProjectionChanged;
    public bool BuildModeActive => buildModeActive;
    public bool RemoveToolActive => removeToolSelected;
    public bool CanPlaceCurrentPreview => currentCanPlace;
    public int SelectedBuildingIndex => selectedIndex;
    public int BuildingCount => buildings != null ? buildings.Length : 0;
    public BuildingData SelectedBuilding
    {
        get
        {
            if (selectedBuilding != null)
            {
                return selectedBuilding;
            }

            if (buildings == null || buildings.Length == 0)
            {
                return null;
            }

            return buildings[Mathf.Clamp(selectedIndex, 0, buildings.Length - 1)];
        }
    }
    public BuildingFootprint.Dir SelectedDirection => selectedDirection;

    private void Awake()
    {
        EnsureReferences();
        SelectBuilding(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, buildings != null ? buildings.Length - 1 : 0)));
    }

    private void Start()
    {
        ReconstructSavedBuildings();
    }

    private void OnEnable()
    {
        if (gridBuildSystem != null && disableGridBuildSystemMousePlacement)
        {
            previousGridMousePlacement = gridBuildSystem.enableMousePlacement;
            hadPreviousGridMousePlacement = true;
            gridBuildSystem.enableMousePlacement = false;
        }
    }

    private void OnDisable()
    {
        if (gridBuildSystem != null && hadPreviousGridMousePlacement)
        {
            gridBuildSystem.enableMousePlacement = previousGridMousePlacement;
        }

        DestroyGhost();
        DestroyRemoveGhost();
        ClearRemovePaintPreview();
        ClearAllFadeCaches();
    }

    private void OnDestroy()
    {
        DestroyGhost();
        DestroyRemoveGhost();
        ClearRemovePaintPreview();
        ClearAllFadeCaches();
    }

    private void Update()
    {
        if (Input.GetKeyDown(buildModeToggleKey))
        {
            SetBuildModeActive(!buildModeActive);
        }

        if (!buildModeActive)
        {
            HideBuildModePreview();
            ClearPlacementInputState();
            return;
        }

        HandleSelectionInput();
        HandleScrollSelectionInput();
        HandleRotationInput();

        if (gridBuildSystem == null || (!removeToolSelected && selectedBuilding == null))
        {
            HideBuildModePreview();
            ClearPlacementInputState();
            return;
        }

        if (removeToolSelected)
        {
            HandleRemoveToolPreviewAndInput();
            return;
        }

        if (!TryGetMousePlacementQuery(out PlacementQuery placementQuery))
        {
            RestoreAllFaded();
            SetGhostVisible(false);
            SetLineGhostsVisible(false);
            SetRemoveGhostVisible(false);
            if (linePlacementDragActive && !Input.GetMouseButton(0))
            {
                EndLinePlacementDrag();
            }

            return;
        }

        HandlePlacementPreviewAndInput(placementQuery);
    }

    public void SelectBuilding(BuildingData building)
    {
        EnsureReferences();
        selectedBuilding = building;
        selectedIndex = IndexOf(building);
        removeToolSelected = false;
        ClearPlacementInputState();
        ClearPostPlacementFadeSuppression();
        DestroyRemoveGhost();
        ClearRemovePaintPreview();
        RebuildGhost();
    }

    public void SelectBuilding(int index)
    {
        EnsureReferences();

        if (buildings == null || buildings.Length == 0)
        {
            selectedBuilding = null;
            selectedIndex = 0;
            removeToolSelected = false;
            ClearPlacementInputState();
            DestroyRemoveGhost();
            ClearRemovePaintPreview();
            RebuildGhost();
            return;
        }

        selectedIndex = Mathf.Clamp(index, 0, buildings.Length - 1);
        selectedBuilding = buildings[selectedIndex];
        removeToolSelected = false;
        ClearPlacementInputState();
        ClearPostPlacementFadeSuppression();
        DestroyRemoveGhost();
        ClearRemovePaintPreview();
        RebuildGhost();
    }

    public bool SetSelectedBuildSurface(int surface)
    {
        EnsureReferences();
        if (selectedBuilding == null)
        {
            return false;
        }

        selectedBuilding.SetBuildSurface(NormalizeBuildSurface(surface));
        UpdateGhostIfVisible();
        return true;
    }

    public bool SetSelectedSupportsTopPlacement(bool supports)
    {
        EnsureReferences();
        if (selectedBuilding == null)
        {
            return false;
        }

        selectedBuilding.SetSupportsTopPlacement(supports);
        return true;
    }

    public bool SelectBuildingByIndex(int index)
    {
        if (buildings == null || index < 0 || index >= buildings.Length)
        {
            return false;
        }

        SelectBuilding(index);
        return selectedBuilding != null;
    }

    public bool SelectRemoveTool()
    {
        EnsureReferences();
        removeToolSelected = true;
        ClearPlacementInputState();
        ClearPostPlacementFadeSuppression();
        SetGhostVisible(false);
        SetLineGhostsVisible(false);
        DestroyGhost();
        DestroyRemoveGhost();
        ClearRemovePaintPreview();
        currentCanPlace = false;
        return true;
    }

    public bool IsRemoveToolSelected()
    {
        return removeToolSelected;
    }

    public bool SelectBuildingById(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId) || buildings == null)
        {
            return false;
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingData building = buildings[i];
            if (building != null && building.BuildingId == buildingId)
            {
                SelectBuilding(i);
                return true;
            }
        }

        return false;
    }

    public bool SelectDirectionByIndex(int direction)
    {
        selectedDirection = NormalizeDirection(direction);
        ClearPostPlacementFadeSuppression();
        UpdateGhostIfVisible();
        return true;
    }

    public bool PreviewSelectedAtCellWithDirection(int x, int y, int direction)
    {
        selectedDirection = NormalizeDirection(direction);
        return PreviewSelectedAtCell(x, y);
    }

    public bool PreviewSelectedAtCell(int x, int y)
    {
        EnsureReferences();

        if (selectedBuilding == null || gridBuildSystem == null)
        {
            RestoreAllFaded();
            SetGhostVisible(false);
            return false;
        }

        UpdateGhost(CenteredAnchorFromCell(x, y));
        return true;
    }

    public bool CanPlaceSelectedAtCell(int x, int y)
    {
        EnsureReferences();

        if (selectedBuilding == null || gridBuildSystem == null)
        {
            return false;
        }

        currentCanPlace = CanPlace(selectedBuilding, CenteredAnchorFromCell(x, y), selectedDirection);
        return currentCanPlace;
    }

    public bool CanPlaceSelectedAtCellWithDirection(int x, int y, int direction)
    {
        selectedDirection = NormalizeDirection(direction);
        return CanPlaceSelectedAtCell(x, y);
    }

    public bool TryPlaceSelectedAtCell(int x, int y)
    {
        EnsureReferences();

        if (selectedBuilding == null || gridBuildSystem == null)
        {
            RestoreAllFaded();
            return false;
        }

        Vector2Int anchorCell = CenteredAnchorFromCell(x, y);
        currentCanPlace = CanPlace(selectedBuilding, anchorCell, selectedDirection);
        bool placed = currentCanPlace && TryPlace(selectedBuilding, anchorCell, selectedDirection);
        if (placed)
        {
            RestoreAllFaded();
        }

        UpdateGhost(anchorCell);
        return placed;
    }

    public bool TryPlaceSelectedAtCellWithDirection(int x, int y, int direction)
    {
        selectedDirection = NormalizeDirection(direction);
        return TryPlaceSelectedAtCell(x, y);
    }

    public bool CreateRuntimeRectangularBuildingFromSelected(string buildingId, int width, int height)
    {
        EnsureReferences();

        if (selectedBuilding == null)
        {
            return false;
        }

        string safeId = string.IsNullOrWhiteSpace(buildingId) ? $"runtime_{width}x{height}" : buildingId;
        BuildingData runtimeBuilding = ScriptableObject.CreateInstance<BuildingData>();
        runtimeBuilding.name = safeId;
        runtimeBuilding.ConfigureRuntimeRectangular(safeId, safeId, width, height, selectedBuilding.Prefab);
        runtimeBuilding.SetBuildSurface(selectedBuilding.BuildSurface);
        runtimeBuilding.SetSupportsTopPlacement(selectedBuilding.SupportsTopPlacement);
        AppendRuntimeBuilding(runtimeBuilding);
        SelectBuilding(runtimeBuilding);
        return true;
    }

    public bool TryPlaceRuntimeRectangularAtCell(string buildingId, int width, int height, int x, int y)
    {
        return TryPlaceRuntimeRectangularAtCellWithDirection(buildingId, width, height, x, y, (int)BuildingFootprint.Dir.Down);
    }

    public bool TryPlaceRuntimeRectangularAtCellWithDirection(string buildingId, int width, int height, int x, int y, int direction)
    {
        if (!CreateRuntimeRectangularBuildingFromSelected(buildingId, width, height))
        {
            return false;
        }

        return TryPlaceSelectedAtCellWithDirection(x, y, direction);
    }

    public bool SetSelectedAnchorMode(int anchorMode)
    {
        EnsureReferences();

        if (selectedBuilding == null)
        {
            return false;
        }

        selectedBuilding.SetAnchorMode(anchorMode == (int)BuildingData.AnchorMode.Manual
            ? BuildingData.AnchorMode.Manual
            : BuildingData.AnchorMode.Auto);
        UpdateGhostIfVisible();
        return true;
    }

    public bool SetSelectedManualAnchorOffset(float x, float y)
    {
        EnsureReferences();

        if (selectedBuilding == null)
        {
            return false;
        }

        selectedBuilding.SetManualAnchorOffset(x, y);
        UpdateGhostIfVisible();
        return true;
    }

    public string GetSelectedFootprintCellsAtCellWithDirection(int x, int y, int direction)
    {
        EnsureReferences();

        if (selectedBuilding == null)
        {
            return "<none>";
        }

        List<Vector2Int> footprint = BuildingFootprint.GetOccupiedCells(
            new Vector2Int(x, y),
            selectedBuilding,
            NormalizeDirection(direction));
        return FormatCells(footprint);
    }

    public string GetSelectedWorldPositionAtCellWithDirection(int x, int y, int direction)
    {
        EnsureReferences();

        if (selectedBuilding == null || gridBuildSystem == null)
        {
            return "<none>";
        }

        Vector3 position = GetBuildingWorldPosition(selectedBuilding, new Vector2Int(x, y), NormalizeDirection(direction));
        return position.ToString("F3");
    }

    public bool TryRemoveAtCell(int x, int y)
    {
        EnsureReferences();

        if (gridBuildSystem == null)
        {
            return false;
        }

        Vector2Int cell = new Vector2Int(x, y);
        bool removed = TryRemove(cell);
        if (removeToolSelected)
        {
            PreviewRemoveAtCell(x, y);
        }
        else
        {
            ClearRemovePreview();
        }

        return removed;
    }

    public bool PreviewRemoveAtCell(int x, int y)
    {
        EnsureReferences();
        if (!removeToolSelected)
        {
            return false;
        }

        Vector2Int cell = new Vector2Int(x, y);
        if (TryGetHighestPlacedBuildingAt(cell, out PlacedBuilding building))
        {
            PreviewRemoveTargets(RemoveTarget.ForBuilding(building, cell));
            currentCanPlace = true;
            return true;
        }

        ClearRemovePreview();
        currentCanPlace = false;
        return false;
    }

    public bool TryRemoveSelectedAtCell(int x, int y)
    {
        EnsureReferences();
        if (!removeToolSelected)
        {
            return false;
        }

        bool removed = TryRemove(new Vector2Int(x, y));
        if (removed)
        {
            ClearRemovePreview();
        }

        return removed;
    }

    public bool HasLayerZeroBuildingBlockAtCell(int x, int y)
    {
        return HasLayerZeroBuildingBlockAtCell(new Vector2Int(x, y));
    }

    public bool HasLayerZeroBuildingBlockAtCell(Vector2Int cell)
    {
        return occupiedBlocksByCell.ContainsKey(new Vector3Int(cell.x, FloorLayer, cell.y));
    }

    public bool IsPathBlockedAtCell(int x, int y)
    {
        return IsPathBlockedAtCell(new Vector2Int(x, y));
    }

    public bool IsPathBlockedAtCell(Vector2Int cell)
    {
        return HasLayerZeroBuildingBlockAtCell(cell)
            || (gridBuildSystem != null && gridBuildSystem.IsOccupied(cell));
    }

    public int GetLayerZeroBuildingBlockCount()
    {
        return CountLayerZeroBuildingBlocks();
    }

    public string ClearAllPlacedBuildings()
    {
        EnsureReferences();

        int removedBuildings = placedBuildings.Count;
        int removedLayerZeroBlocks = CountLayerZeroBuildingBlocks();
        for (int i = placedBuildings.Count - 1; i >= 0; i--)
        {
            RemovePlacedBuildingImmediate(placedBuildings[i], false);
        }

        placedBuildings.Clear();
        occupiedBlocksByCell.Clear();
        SaveBuildings();
        NotifyPathfindingProjectionChanged();
        return $"{nameof(BuildPlacementController)} cleared buildings={removedBuildings} layerZeroBlocks={removedLayerZeroBlocks} pathfindingOccupiedCount={(gridBuildSystem != null ? gridBuildSystem.DictionaryCount : -1)}";
    }

    public string GetPlacementDebugState()
    {
        EnsureReferences();

        string buildingId = selectedBuilding != null ? selectedBuilding.BuildingId : "<none>";
        string size = selectedBuilding != null ? selectedBuilding.Size.ToString() : "<none>";
        string center = selectedBuilding != null ? selectedBuilding.CenterOffset.ToString() : "<none>";
        string surface = selectedBuilding != null ? selectedBuilding.BuildSurface.ToString() : "<none>";
        string supportsTop = selectedBuilding != null ? selectedBuilding.SupportsTopPlacement.ToString() : "False";
        string selectedTool = removeToolSelected ? "Remove" : "Build";
        string ghostName = ghostInstance != null ? ghostInstance.name : "<none>";
        string ghostActive = ghostInstance != null ? ghostInstance.activeSelf.ToString() : "False";
        string ghostPosition = ghostInstance != null ? ghostInstance.transform.position.ToString("F3") : "<none>";
        string removeGhostName = removeGhostInstance != null ? removeGhostInstance.name : "<none>";
        string removeGhostActive = removeGhostInstance != null ? removeGhostInstance.activeSelf.ToString() : "False";
        string removeGhostTarget = currentRemovePreviewBuilding != null ? currentRemovePreviewBuilding.BuildingId : "<none>";
        int buildingCount = buildings != null ? buildings.Length : 0;
        int pathfindingOccupiedCount = gridBuildSystem != null ? gridBuildSystem.DictionaryCount : -1;
        int layerZeroBlocks = CountLayerZeroBuildingBlocks();

        return $"{nameof(BuildPlacementController)} buildModeActive={buildModeActive} selectedTool={selectedTool} selectedIndex={selectedIndex} direction={selectedDirection} buildingCount={buildingCount} selectedBuilding={buildingId} surface={surface} supportsTop={supportsTop} size={size} centerOffset={center} ghost={ghostName} ghostActive={ghostActive} ghostPosition={ghostPosition} removeGhost={removeGhostName} removeGhostActive={removeGhostActive} removeGhostTarget={removeGhostTarget} canPlace={currentCanPlace} pathfindingOccupiedCount={pathfindingOccupiedCount} buildingCountPlaced={placedBuildings.Count} occupiedBlockCount={occupiedBlocksByCell.Count} layerZeroBlockCount={layerZeroBlocks} maxBuildLayer={MaxBuildLayer} fadedCount=0";
    }

    public static string RunRemoveToolValidation()
    {
        BuildPlacementController controller = FindFirstObjectByType<BuildPlacementController>();
        if (controller == null)
        {
            return "RemoveToolValidation fail: no BuildPlacementController.";
        }

        controller.EnsureReferences();
        if (controller.gridBuildSystem == null)
        {
            return "RemoveToolValidation fail: no GridBuildSystem.";
        }

        if (controller.buildings == null || controller.buildings.Length == 0)
        {
            return "RemoveToolValidation fail: no building data.";
        }

        if (!controller.TryFindStackableValidationBuilding(out int validationIndex))
        {
            return "RemoveToolValidation fail: no floor-or-top 1x1 building data.";
        }

        BuildingData originalBuilding = controller.selectedBuilding;
        int originalIndex = controller.selectedIndex;
        BuildingFootprint.Dir originalDirection = controller.selectedDirection;
        bool originalRemoveTool = controller.removeToolSelected;
        HashSet<PlacedBuilding> initialBuildings = new HashSet<PlacedBuilding>(controller.placedBuildings);

        int beforeBuildings = controller.placedBuildings.Count;
        int beforeBlocks = controller.occupiedBlocksByCell.Count;
        bool placedBase = false;
        bool placedTop = false;
        bool previewTop = false;
        bool rayHitTop = false;
        bool redPaintTop = false;
        bool noRemoveGhostDuringPreview = false;
        bool dragRemovedTop = false;
        bool sameCellDragBlocked = false;
        bool baseRemainedAfterTopDrag = false;
        bool placedSecondTop = false;
        bool removedBaseCascade = false;
        bool baseGone = false;
        bool topGoneByCascade = false;
        bool previewAfterAllRemoved = true;
        bool noRemoveGhostAfterAllRemoved = false;
        bool linePlaced = false;
        bool linePreview = false;
        bool linePainted = false;
        bool lineRemoved = false;
        bool lineCleared = false;
        Vector2Int cell = default;
        Vector2Int lineStart = default;
        Vector2Int lineEnd = default;
        PlacedBuilding baseBuilding = null;
        PlacedBuilding topBuilding = null;
        PlacedBuilding secondTopBuilding = null;

        try
        {
            controller.SelectBuilding(validationIndex);
            controller.selectedDirection = BuildingFootprint.Dir.Down;

            if (!controller.TryFindValidationCell(out cell))
            {
                return "RemoveToolValidation fail: no free validation cell.";
            }

            BuildingData building = controller.selectedBuilding;
            Vector2Int anchor = controller.CenteredAnchorFromCell(cell.x, cell.y);
            if (controller.TryBuildPlacementQuery(building, anchor, BuildingFootprint.Dir.Down, FloorLayer, false, out PlacementQuery baseQuery)
                && baseQuery.Valid)
            {
                placedBase = controller.TryPlaceResolved(building, baseQuery, out baseBuilding, true, true, false);
            }

            if (controller.TryBuildPlacementQuery(building, anchor, BuildingFootprint.Dir.Down, AutoLayer, false, out PlacementQuery topQuery)
                && topQuery.Valid
                && topQuery.BaseLayer > FloorLayer)
            {
                placedTop = controller.TryPlaceResolved(building, topQuery, out topBuilding, true, true, false);
            }

            Physics.SyncTransforms();
            Vector3 rayOrigin = controller.GetBuildingWorldPosition(building, anchor, BuildingFootprint.Dir.Down)
                + Vector3.up * Mathf.Max(4f, controller.gridBuildSystem.cellSize * 6f);
            rayHitTop = controller.TryGetPlacedBuildingFromRay(new Ray(rayOrigin, Vector3.down), out PlacedBuilding rayBuilding, out Vector2Int rayCell)
                && rayBuilding == topBuilding
                && rayCell == cell;

            controller.SelectRemoveTool();
            bool removeSelected = controller.IsRemoveToolSelected();
            previewTop = controller.PreviewRemoveAtCell(cell.x, cell.y);
            redPaintTop = controller.removePaintOriginalBlocks.Count > 0
                && controller.RemovePreviewUsesHologramMaterial()
                && controller.currentRemovePreviewBuilding == topBuilding;
            noRemoveGhostDuringPreview = controller.removeGhostInstance == null || !controller.removeGhostInstance.activeSelf;

            controller.hasLastRemovalDragCell = false;
            dragRemovedTop = controller.RemoveDragTarget(RemoveTarget.ForBuilding(topBuilding, cell));
            sameCellDragBlocked = !controller.RemoveDragTarget(RemoveTarget.ForBuilding(baseBuilding, cell));
            baseRemainedAfterTopDrag = baseBuilding != null && controller.placedBuildings.Contains(baseBuilding);
            controller.hasLastRemovalDragCell = false;

            if (controller.TryBuildPlacementQuery(building, anchor, BuildingFootprint.Dir.Down, AutoLayer, false, out PlacementQuery secondTopQuery)
                && secondTopQuery.Valid
                && secondTopQuery.BaseLayer > FloorLayer)
            {
                placedSecondTop = controller.TryPlaceResolved(building, secondTopQuery, out secondTopBuilding, true, true, false);
            }

            controller.PreviewRemoveTargets(RemoveTarget.ForBuilding(baseBuilding, cell));
            removedBaseCascade = controller.RemoveResolvedTarget(RemoveTarget.ForBuilding(baseBuilding, cell));
            baseGone = baseBuilding == null || !controller.placedBuildings.Contains(baseBuilding);
            topGoneByCascade = secondTopBuilding == null || !controller.placedBuildings.Contains(secondTopBuilding);
            previewAfterAllRemoved = controller.PreviewRemoveAtCell(cell.x, cell.y);
            noRemoveGhostAfterAllRemoved = controller.removeGhostInstance == null || !controller.removeGhostInstance.activeSelf;

            if (controller.TryFindValidationLineStart(3, out lineStart))
            {
                lineEnd = new Vector2Int(lineStart.x + 2, lineStart.y);
                int placedLineCount = 0;
                for (int i = 0; i < 3; i++)
                {
                    Vector2Int lineCell = new Vector2Int(lineStart.x + i, lineStart.y);
                    Vector2Int lineAnchor = controller.CenteredAnchorFromCell(lineCell.x, lineCell.y);
                    if (controller.TryBuildPlacementQuery(building, lineAnchor, BuildingFootprint.Dir.Down, FloorLayer, false, out PlacementQuery lineQuery)
                        && lineQuery.Valid
                        && controller.TryPlaceResolved(building, lineQuery, out _, true, true, false))
                    {
                        placedLineCount++;
                    }
                }

                linePlaced = placedLineCount == 3;
                controller.UpdateLineRemovalPreview(lineStart, lineEnd);
                linePreview = controller.lineRemovalTargets.Count == 3;
                linePainted = controller.removePaintOriginalBlocks.Count > 0
                    && controller.RemovePreviewUsesHologramMaterial();
                lineRemoved = controller.RemoveLineRemovalTargets();
                lineCleared = !controller.PreviewRemoveAtCell(lineStart.x, lineStart.y)
                    && !controller.PreviewRemoveAtCell(lineStart.x + 1, lineStart.y)
                    && !controller.PreviewRemoveAtCell(lineEnd.x, lineEnd.y);
            }

            int afterBuildings = controller.placedBuildings.Count;
            int afterBlocks = controller.occupiedBlocksByCell.Count;
            bool countsRestored = beforeBuildings == afterBuildings && beforeBlocks == afterBlocks;
            bool pass = removeSelected
                && placedBase
                && placedTop
                && rayHitTop
                && previewTop
                && redPaintTop
                && noRemoveGhostDuringPreview
                && dragRemovedTop
                && sameCellDragBlocked
                && baseRemainedAfterTopDrag
                && placedSecondTop
                && removedBaseCascade
                && baseGone
                && topGoneByCascade
                && !previewAfterAllRemoved
                && noRemoveGhostAfterAllRemoved
                && linePlaced
                && linePreview
                && linePainted
                && lineRemoved
                && lineCleared
                && countsRestored;

            return $"RemoveToolValidation pass={pass} placedBase={placedBase} placedTop={placedTop} rayHitTop={rayHitTop} previewTop={previewTop} redPaintTop={redPaintTop} noRemoveGhostDuringPreview={noRemoveGhostDuringPreview} dragRemovedTop={dragRemovedTop} sameCellDragBlocked={sameCellDragBlocked} baseRemainedAfterTopDrag={baseRemainedAfterTopDrag} placedSecondTop={placedSecondTop} removedBaseCascade={removedBaseCascade} baseGone={baseGone} topGoneByCascade={topGoneByCascade} previewAfterAllRemoved={previewAfterAllRemoved} noRemoveGhostAfterAllRemoved={noRemoveGhostAfterAllRemoved} linePlaced={linePlaced} linePreview={linePreview} linePainted={linePainted} lineRemoved={lineRemoved} lineCleared={lineCleared} beforeBuildings={beforeBuildings} afterBuildings={afterBuildings} beforeBlocks={beforeBlocks} afterBlocks={afterBlocks} cell={cell} lineStart={lineStart} lineEnd={lineEnd}";
        }
        finally
        {
            controller.DestroyRemoveGhost();
            controller.ClearRemovePaintPreview();
            controller.hasLastRemovalDragCell = false;
            controller.removalDragActive = false;
            controller.hasLineRemovalStartCell = false;

            for (int i = controller.placedBuildings.Count - 1; i >= 0; i--)
            {
                PlacedBuilding placed = controller.placedBuildings[i];
                if (placed != null && !initialBuildings.Contains(placed))
                {
                    controller.RemovePlacedBuildingImmediate(placed, false);
                }
            }

            controller.SaveBuildings();
            if (originalRemoveTool)
            {
                controller.SelectRemoveTool();
            }
            else if (originalBuilding != null)
            {
                controller.SelectBuilding(originalBuilding);
            }
            else
            {
                controller.SelectBuilding(originalIndex);
            }

            controller.selectedDirection = originalDirection;
        }
    }

    private void HandleSelectionInput()
    {
        int buildingCount = buildings != null ? buildings.Length : 0;
        int maxKeys = Mathf.Min(buildingCount, 8);
        for (int i = 0; i < maxKeys; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectBuilding(i);
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            SelectRemoveTool();
        }
    }

    private void HandleScrollSelectionInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.001f)
        {
            return;
        }

        int buildingCount = buildings != null ? buildings.Length : 0;
        int totalSlots = buildingCount + 1;
        if (totalSlots <= 0)
        {
            return;
        }

        int currentSlot = removeToolSelected
            ? buildingCount
            : Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, buildingCount - 1));
        int nextSlot = currentSlot + (scroll > 0f ? 1 : -1);
        nextSlot = ((nextSlot % totalSlots) + totalSlots) % totalSlots;

        if (nextSlot == buildingCount)
        {
            SelectRemoveTool();
        }
        else
        {
            SelectBuilding(nextSlot);
        }
    }

    private void HandleRotationInput()
    {
        if (removeToolSelected || !Input.GetKeyDown(KeyCode.R))
        {
            return;
        }

        selectedDirection = NormalizeDirection((int)selectedDirection + 1);
        ClearPostPlacementFadeSuppression();
        UpdateGhostIfVisible();
    }

    private void HandleRemoveToolPreviewAndInput()
    {
        EndLinePlacementDrag();
        SetGhostVisible(false);
        SetLineGhostsVisible(false);
        RestoreAllFaded();

        bool hasTarget = TryGetMouseRemoveTarget(out RemoveTarget target);
        bool pressed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
        bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        if (pressed)
        {
            removalDragActive = true;
            hasLastRemovalDragCell = false;
            hasLineRemovalStartCell = hasTarget;
            lineRemovalStartCell = hasTarget ? target.Cell : default;
        }

        if (removalDragActive && held && hasLineRemovalStartCell)
        {
            Vector2Int currentCell = hasTarget ? target.Cell : lineRemovalStartCell;
            UpdateLineRemovalPreview(lineRemovalStartCell, currentCell);
            currentCanPlace = lineRemovalTargets.Count > 0;
            return;
        }

        if (removalDragActive && !held)
        {
            if (hasLineRemovalStartCell)
            {
                RemoveLineRemovalTargets();
            }

            removalDragActive = false;
            hasLastRemovalDragCell = false;
            hasLineRemovalStartCell = false;
            ClearRemovePreview();
            return;
        }

        if (hasTarget)
        {
            PreviewRemoveTargets(target);
            currentCanPlace = true;
        }
        else
        {
            ClearRemovePreview();
            currentCanPlace = false;
        }
    }

    private void HandlePlacementPreviewAndInput(PlacementQuery placementQuery)
    {
        if (IsSelectedLinePlacement())
        {
            HandleLinePlacementInput(placementQuery);
            return;
        }

        EndLinePlacementDrag();
        SetLineGhostsVisible(false);
        UpdateGhost(placementQuery);

        if (Input.GetMouseButtonDown(0))
        {
            currentCanPlace = placementQuery.Valid;
            if (currentCanPlace)
            {
                TryPlaceResolved(selectedBuilding, placementQuery, out _, true, true, false);
            }
        }
    }

    private void HandleLinePlacementInput(PlacementQuery placementQuery)
    {
        if (Input.GetMouseButtonDown(0))
        {
            linePlacementDragActive = true;
            linePlacementStartCell = placementQuery.AnchorCell;
            linePlacementTargetLayer = placementQuery.BaseLayer;
        }

        if (!linePlacementDragActive)
        {
            SetLineGhostsVisible(false);
            UpdateGhost(placementQuery);
            return;
        }

        UpdateLineGhostPreview(linePlacementStartCell, placementQuery.AnchorCell, linePlacementTargetLayer);

        if (!Input.GetMouseButton(0))
        {
            PlaceLinePreviewCells();
            EndLinePlacementDrag();
            UpdateGhost(placementQuery);
        }
    }

    private void HandleRemovalDragInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            removalDragActive = true;
            hasLastRemovalDragCell = false;
            if (TryGetMouseRawCell(out Vector2Int startCell))
            {
                RemoveDragCell(startCell);
            }
        }

        if (removalDragActive && Input.GetMouseButton(1))
        {
            if (TryGetMouseRawCell(out Vector2Int currentCell))
            {
                RemoveDragCell(currentCell);
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            removalDragActive = false;
            hasLastRemovalDragCell = false;
        }
    }

    private void RemoveDragCell(Vector2Int cell)
    {
        if (TryGetRemoveTargetAtCell(cell, out RemoveTarget target))
        {
            RemoveDragTarget(target);
        }
    }

    private bool RemoveDragTarget(RemoveTarget target)
    {
        if (!target.IsValid)
        {
            return false;
        }

        if (hasLastRemovalDragCell && lastRemovalDragCell == target.Cell)
        {
            return false;
        }

        hasLastRemovalDragCell = true;
        lastRemovalDragCell = target.Cell;
        return RemoveResolvedTarget(target);
    }

    private void UpdateLineRemovalPreview(Vector2Int startCell, Vector2Int currentCell)
    {
        BuildUnitLineCells(startCell, currentCell, linePlacementCells);
        ResolveRemoveTargets(linePlacementCells, lineRemovalTargets);
        PreviewRemoveTargets(lineRemovalTargets);
    }

    private bool RemoveLineRemovalTargets()
    {
        if (lineRemovalTargets.Count == 0)
        {
            return false;
        }

        bool removedAny = false;
        for (int i = 0; i < lineRemovalTargets.Count; i++)
        {
            if (RemoveResolvedTarget(lineRemovalTargets[i]))
            {
                removedAny = true;
            }
        }

        lineRemovalTargets.Clear();
        if (removedAny)
        {
            SaveBuildings();
        }

        return removedAny;
    }

    private void ResolveRemoveTargets(List<Vector2Int> cells, List<RemoveTarget> results)
    {
        results.Clear();
        removeTargetBuildingSet.Clear();
        removeTargetGridSet.Clear();

        for (int i = 0; i < cells.Count; i++)
        {
            if (!TryGetRemoveTargetAtCell(cells[i], out RemoveTarget target))
            {
                continue;
            }

            if (target.Building != null)
            {
                if (removeTargetBuildingSet.Add(target.Building))
                {
                    results.Add(target);
                }

                continue;
            }

            if (target.HasGridCell && removeTargetGridSet.Add(target.Cell))
            {
                results.Add(target);
            }
        }

        removeTargetBuildingSet.Clear();
        removeTargetGridSet.Clear();
    }

    private void BuildUnitLineCells(Vector2Int startCell, Vector2Int currentCell, List<Vector2Int> results)
    {
        results.Clear();

        int dx = currentCell.x - startCell.x;
        int dy = currentCell.y - startCell.y;
        bool horizontal = Mathf.Abs(dx) >= Mathf.Abs(dy);
        int distance = horizontal ? Mathf.Abs(dx) : Mathf.Abs(dy);
        int sign = (horizontal ? dx : dy) >= 0 ? 1 : -1;

        for (int offset = 0; offset <= distance; offset++)
        {
            results.Add(horizontal
                ? new Vector2Int(startCell.x + offset * sign, startCell.y)
                : new Vector2Int(startCell.x, startCell.y + offset * sign));
        }
    }

    private void PreviewRemoveTargets(RemoveTarget target)
    {
        lineRemovalTargets.Clear();
        if (target.IsValid)
        {
            lineRemovalTargets.Add(target);
        }

        PreviewRemoveTargets(lineRemovalTargets);
    }

    private void PreviewRemoveTargets(List<RemoveTarget> targets)
    {
        ClearRemovePaintPreview();
        DestroyRemoveGhost();
        currentRemovePreviewBuilding = null;

        for (int i = 0; i < targets.Count; i++)
        {
            RemoveTarget target = targets[i];
            if (target.Building != null)
            {
                if (currentRemovePreviewBuilding == null)
                {
                    currentRemovePreviewBuilding = target.Building;
                }

                PaintRemoveBuilding(target.Building);
            }
            else if (target.HasGridCell)
            {
                PaintRemoveGridCell(target.Cell);
            }
        }
    }

    private void PaintRemoveBuilding(PlacedBuilding building)
    {
        if (building == null || building.Instance == null)
        {
            return;
        }

        rendererScratch.Clear();
        building.Instance.GetComponentsInChildren(false, rendererScratch);
        for (int i = 0; i < rendererScratch.Count; i++)
        {
            PaintRemoveRenderer(rendererScratch[i]);
        }

        rendererScratch.Clear();
    }

    private void PaintRemoveGridCell(Vector2Int cell)
    {
        if (gridBuildSystem == null || !gridBuildSystem.TryGet(cell, out GridCellData cellData) || cellData == null)
        {
            return;
        }

        GameObject occupant = cellData.Occupant;
        if (occupant == null)
        {
            return;
        }

        rendererScratch.Clear();
        occupant.GetComponentsInChildren(false, rendererScratch);
        for (int i = 0; i < rendererScratch.Count; i++)
        {
            PaintRemoveRenderer(rendererScratch[i]);
        }

        rendererScratch.Clear();
    }

    private void PaintRemoveRenderer(Renderer targetRenderer)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (!removePaintOriginalBlocks.ContainsKey(targetRenderer))
        {
            MaterialPropertyBlock original = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(original);
            Material[] originalMaterials = targetRenderer.sharedMaterials;
            Material[] previewMaterials = CreateRemovePreviewMaterials(originalMaterials);
            removePaintOriginalBlocks[targetRenderer] = new RemovePaintState(originalMaterials, original, previewMaterials);
            targetRenderer.sharedMaterials = previewMaterials;
        }

        removePaintBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(removePaintBlock);
        Color paintColor = new Color(invalidColor.r, invalidColor.g, invalidColor.b, 1f);
        removePaintBlock.SetColor(ColorId, paintColor);
        removePaintBlock.SetColor(BaseColorId, paintColor);
        removePaintBlock.SetColor(EmissionColorId, paintColor);
        targetRenderer.SetPropertyBlock(removePaintBlock);
    }

    private Material[] CreateRemovePreviewMaterials(Material[] originalMaterials)
    {
        EnsureReferences();

        int count = Mathf.Max(1, originalMaterials != null ? originalMaterials.Length : 0);
        Material[] previewMaterials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            Material source = ghostPreviewMaterial != null
                ? ghostPreviewMaterial
                : originalMaterials != null && i < originalMaterials.Length
                    ? originalMaterials[i]
                    : null;
            if (source == null)
            {
                continue;
            }

            Material material = new Material(source);

            material.name = ghostPreviewMaterial != null
                ? $"{ghostPreviewMaterial.name}_Remove_Instance"
                : "RemovePreviewOriginalShader_Instance";

            SetGhostTint(material, invalidColor, false);
            previewMaterials[i] = material;
        }

        return previewMaterials;
    }

    private bool RemovePreviewUsesHologramMaterial()
    {
        EnsureReferences();
        if (ghostPreviewMaterial == null)
        {
            return false;
        }

        foreach (RemovePaintState state in removePaintOriginalBlocks.Values)
        {
            if (state == null || state.PreviewMaterials == null)
            {
                continue;
            }

            for (int i = 0; i < state.PreviewMaterials.Length; i++)
            {
                Material material = state.PreviewMaterials[i];
                if (material != null && material.shader == ghostPreviewMaterial.shader)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ClearRemovePreview()
    {
        ClearRemovePaintPreview();
        SetRemoveGhostVisible(false);
        currentRemovePreviewBuilding = null;
        lineRemovalTargets.Clear();
    }

    private void ClearRemovePaintPreview()
    {
        foreach (KeyValuePair<Renderer, RemovePaintState> entry in removePaintOriginalBlocks)
        {
            Renderer renderer = entry.Key;
            RemovePaintState state = entry.Value;
            if (renderer != null && state != null)
            {
                renderer.sharedMaterials = state.OriginalMaterials;
                renderer.SetPropertyBlock(state.OriginalPropertyBlock);
            }

            if (state != null)
            {
                state.DestroyPreviewMaterials(DestroyUnityObject);
            }
        }

        removePaintOriginalBlocks.Clear();
    }

    private bool TryGetMouseRawCell(out Vector2Int cell)
    {
        cell = default;
        if (activeCamera == null)
        {
            activeCamera = Camera.main;
        }

        if (activeCamera == null || gridBuildSystem == null)
        {
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        Plane gridPlane = new Plane(gridBuildSystem.transform.up, gridBuildSystem.transform.position);
        if (!gridPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        cell = gridBuildSystem.WorldToGrid(ray.GetPoint(distance));
        return true;
    }

    private bool TryGetMouseRemoveTarget(out RemoveTarget target)
    {
        target = default;

        if (activeCamera == null)
        {
            activeCamera = Camera.main;
        }

        if (activeCamera == null || gridBuildSystem == null)
        {
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        if (TryGetPlacedBuildingFromRay(ray, out PlacedBuilding building, out Vector2Int cell))
        {
            target = RemoveTarget.ForBuilding(building, cell);
            return true;
        }

        if (!TryGetGroundRawCell(ray, out cell))
        {
            return false;
        }

        return TryGetRemoveTargetAtCell(cell, out target);
    }

    private bool TryGetGroundRawCell(Ray ray, out Vector2Int cell)
    {
        cell = default;
        Plane gridPlane = new Plane(gridBuildSystem.transform.up, gridBuildSystem.transform.position);
        if (!gridPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        cell = gridBuildSystem.WorldToGrid(ray.GetPoint(distance));
        return true;
    }

    private bool TryGetPlacedBuildingFromRay(Ray ray, out PlacedBuilding building, out Vector2Int cell)
    {
        building = null;
        cell = default;

        RaycastHit[] hits = Physics.RaycastAll(ray, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return TryGetPlacedBuildingFromRendererBoundsRay(ray, out building, out cell);
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !TryGetPlacedBuildingFromCollider(hit.collider, out building))
            {
                continue;
            }

            if (!TryGetClosestOccupiedCell(building, gridBuildSystem.WorldToGridFloat(hit.point), out cell))
            {
                cell = gridBuildSystem.WorldToGrid(hit.point);
            }

            return true;
        }

        return TryGetPlacedBuildingFromRendererBoundsRay(ray, out building, out cell);
    }

    private bool TryGetPlacedBuildingFromRendererBoundsRay(Ray ray, out PlacedBuilding building, out Vector2Int cell)
    {
        building = null;
        cell = default;

        float bestDistance = float.PositiveInfinity;
        PlacedBuilding bestBuilding = null;
        for (int i = 0; i < placedBuildings.Count; i++)
        {
            PlacedBuilding candidate = placedBuildings[i];
            if (candidate == null
                || candidate.Instance == null
                || !TryGetRenderersBounds(candidate.Instance, out Bounds bounds)
                || !bounds.IntersectRay(ray, out float distance)
                || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestBuilding = candidate;
        }

        if (bestBuilding == null)
        {
            return false;
        }

        building = bestBuilding;
        Vector3 hitPoint = ray.GetPoint(bestDistance);
        if (!TryGetClosestOccupiedCell(building, gridBuildSystem.WorldToGridFloat(hitPoint), out cell))
        {
            cell = gridBuildSystem.WorldToGrid(hitPoint);
        }

        return true;
    }

    private bool IsSelectedLinePlacement()
    {
        return selectedBuilding != null && selectedBuilding.DragPlacement == DragPlacement.Line;
    }

    private void SetBuildModeActive(bool active)
    {
        if (buildModeActive == active)
        {
            return;
        }

        buildModeActive = active;
        if (!buildModeActive)
        {
            HideBuildModePreview();
            ClearPlacementInputState();
        }
        else
        {
            ClearPostPlacementFadeSuppression();
        }
    }

    private void HideBuildModePreview()
    {
        RestoreAllFaded();
        SetGhostVisible(false);
        SetLineGhostsVisible(false);
        ClearRemovePreview();
    }

    private void ClearPlacementInputState()
    {
        EndLinePlacementDrag();
        removalDragActive = false;
        hasLastRemovalDragCell = false;
        hasLineRemovalStartCell = false;
        lineRemovalTargets.Clear();
    }

    private void EndLinePlacementDrag()
    {
        linePlacementDragActive = false;
        linePlacementTargetLayer = AutoLayer;
        linePlacementCells.Clear();
        linePlacementQueries.Clear();
    }

    private void EnsureReferences()
    {
        if (gridBuildSystem == null)
        {
            gridBuildSystem = FindFirstObjectByType<GridBuildSystem>();
        }

        if (activeCamera == null)
        {
            activeCamera = Camera.main;
        }

        if (selectedBuilding == null && buildings != null && buildings.Length > 0)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, buildings.Length - 1);
            selectedBuilding = buildings[selectedIndex];
        }

#if UNITY_EDITOR
        if (ghostPreviewMaterial == null || ghostPreviewMaterial.name.StartsWith("GhostPreview"))
        {
            Material hologram = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultGhostMaterialPath);
            if (hologram != null)
            {
                ghostPreviewMaterial = hologram;
            }
        }
#endif
    }

    private bool TryGetMousePlacementQuery(out PlacementQuery query)
    {
        query = default;

        if (activeCamera == null)
        {
            activeCamera = Camera.main;
        }

        if (activeCamera == null || gridBuildSystem == null)
        {
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        if (AllowsTopPlacement(selectedBuilding) && TryGetRoofPlacementQuery(ray, out query))
        {
            return true;
        }

        if (!TryGetGroundAnchorCell(ray, out Vector2Int anchorCell))
        {
            return false;
        }

        if (selectedBuilding != null && selectedBuilding.BuildSurface == BuildSurface.TopOnly)
        {
            int baseLayer = GetRecommendedTopBaseLayer(selectedBuilding, anchorCell, selectedDirection);
            return TryBuildPlacementQuery(selectedBuilding, anchorCell, selectedDirection, baseLayer, false, out query);
        }

        if (!TryBuildPlacementQuery(selectedBuilding, anchorCell, selectedDirection, FloorLayer, false, out query))
        {
            return false;
        }

        if (query.Valid || !AllowsTopPlacement(selectedBuilding))
        {
            return true;
        }

        int topLayer = GetRecommendedTopBaseLayer(selectedBuilding, anchorCell, selectedDirection);
        if (TryBuildPlacementQuery(selectedBuilding, anchorCell, selectedDirection, topLayer, false, out PlacementQuery topQuery)
            && topQuery.Valid)
        {
            query = topQuery;
        }

        return true;
    }

    private bool TryGetGroundAnchorCell(Ray ray, out Vector2Int anchorCell)
    {
        anchorCell = default;
        Plane gridPlane = new Plane(gridBuildSystem.transform.up, gridBuildSystem.transform.position);

        if (!gridPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        anchorCell = CenteredAnchor(gridBuildSystem.WorldToGridFloat(ray.GetPoint(distance)));
        return true;
    }

    private bool TryGetRoofPlacementQuery(Ray ray, out PlacementQuery query)
    {
        query = default;

        RaycastHit[] hits = Physics.RaycastAll(ray, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Vector3 gridUp = gridBuildSystem != null ? gridBuildSystem.transform.up : Vector3.up;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null
                || Vector3.Dot(hit.normal, gridUp) < 0.55f
                || !TryGetPlacedBuildingFromCollider(hit.collider, out _))
            {
                continue;
            }

            Vector2Int hitCell = gridBuildSystem.WorldToGrid(hit.point);
            int baseLayer = TryGetHighestOccupiedLayer(hitCell, out int highestLayer)
                ? highestLayer + 1
                : GetRecommendedTopBaseLayer(selectedBuilding, CenteredAnchor(gridBuildSystem.WorldToGridFloat(hit.point)), selectedDirection);
            baseLayer = NormalizeLayer(baseLayer);

            Vector2Int anchorCell = CenteredAnchor(gridBuildSystem.WorldToGridFloat(hit.point));
            TryBuildPlacementQuery(selectedBuilding, anchorCell, selectedDirection, baseLayer, false, out query);
            return true;
        }

        return false;
    }

    private bool TryGetPlacedBuildingFromCollider(Collider hitCollider, out PlacedBuilding building)
    {
        building = null;
        if (hitCollider == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        for (int i = 0; i < placedBuildings.Count; i++)
        {
            PlacedBuilding candidate = placedBuildings[i];
            if (candidate == null || candidate.Instance == null)
            {
                continue;
            }

            Transform root = candidate.Instance.transform;
            if (hitTransform == root || hitTransform.IsChildOf(root))
            {
                building = candidate;
                return true;
            }
        }

        return false;
    }

    private Vector2Int CenteredAnchor(Vector2 cursorGrid)
    {
        Vector2Int size = GetRotatedFootprintSize(
            selectedBuilding != null ? selectedBuilding.Size : Vector2Int.one,
            selectedDirection);
        return new Vector2Int(
            Mathf.RoundToInt(cursorGrid.x - size.x * 0.5f),
            Mathf.RoundToInt(cursorGrid.y - size.y * 0.5f));
    }

    private Vector2Int CenteredAnchorFromCell(int x, int y)
    {
        return CenteredAnchor(new Vector2(x + 0.5f, y + 0.5f));
    }

    private void UpdateGhost(Vector2Int anchorCell)
    {
        if (TryBuildPlacementQuery(selectedBuilding, anchorCell, selectedDirection, AutoLayer, false, out PlacementQuery query))
        {
            UpdateGhost(query);
        }
    }

    private void UpdateGhost(PlacementQuery query)
    {
        SetRemoveGhostVisible(false);
        if (ghostInstance == null)
        {
            RebuildGhost();
        }

        if (ghostInstance == null)
        {
            return;
        }

        if (lineStretchGhost != null)
        {
            lineStretchGhost.SetActive(false);
        }

        currentGhostAnchorCell = query.AnchorCell;
        currentCanPlace = query.Valid;
        PositionBuildingInstance(
            ghostInstance,
            selectedBuilding,
            query.AnchorCell,
            selectedDirection,
            query.BaseLayer,
            ghostHeightOffset);
        SetGhostTint(currentCanPlace ? validColor : invalidColor, currentCanPlace);
        SetGhostVisible(true);

        if (suppressFadeUntilGhostMoves)
        {
            if (query.AnchorCell == suppressFadeAnchorCell)
            {
                return;
            }

            ClearPostPlacementFadeSuppression();
        }
    }

    private void UpdateRemoveGhost(PlacedBuilding building)
    {
        if (building == null)
        {
            SetRemoveGhostVisible(false);
            currentRemovePreviewBuilding = null;
            return;
        }

        if (removeGhostInstance == null || currentRemovePreviewBuilding != building)
        {
            RebuildRemoveGhost(building);
        }

        if (removeGhostInstance == null)
        {
            return;
        }

        BuildingData data = FindBuildingDataById(building.BuildingId);
        if (data != null)
        {
            PositionBuildingInstance(
                removeGhostInstance,
                data,
                building.AnchorCell,
                building.Direction,
                building.BaseLayer,
                ghostHeightOffset);
        }
        else if (building.Instance != null)
        {
            removeGhostInstance.transform.SetPositionAndRotation(
                building.Instance.transform.position + gridBuildSystem.transform.up * ghostHeightOffset,
                building.Instance.transform.rotation);
            removeGhostInstance.transform.localScale = building.Instance.transform.localScale;
        }

        SetGhostTint(removeGhostMaterialInstances, invalidColor, false);
        SetRemoveGhostVisible(true);
        currentRemovePreviewBuilding = building;
    }

    private void UpdateLineGhostPreview(Vector2Int startCell, Vector2Int currentCell, int targetLayer)
    {
        if (ghostInstance == null)
        {
            RebuildGhost();
        }

        if (ghostInstance == null)
        {
            return;
        }

        BuildLineAnchorCells(startCell, currentCell, linePlacementCells);
        if (linePlacementCells.Count == 0)
        {
            currentCanPlace = false;
            SetGhostVisible(false);
            SetLineGhostsVisible(false);
            RestoreAllFaded();
            return;
        }

        currentGhostAnchorCell = currentCell;
        SetGhostVisible(false);
        SetLineGhostsVisible(false);
        linePlacementQueries.Clear();

        bool allValid = true;
        for (int i = 0; i < linePlacementCells.Count; i++)
        {
            if (!TryBuildPlacementQuery(
                    selectedBuilding,
                    linePlacementCells[i],
                    selectedDirection,
                    targetLayer,
                    false,
                    out PlacementQuery query))
            {
                allValid = false;
                continue;
            }

            linePlacementQueries.Add(query);
            if (!query.Valid)
            {
                allValid = false;
            }
        }

        currentCanPlace = allValid && linePlacementQueries.Count == linePlacementCells.Count;
        Color lineColor = currentCanPlace ? validColor : invalidColor;
        ShowStretchedLineGhost(startCell, currentCell, NormalizeLayer(targetLayer), lineColor, currentCanPlace);
    }

    private void ShowStretchedLineGhost(Vector2Int startCell, Vector2Int currentCell, int baseLayer, Color color, bool valid)
    {
        if (linePlacementCells.Count == 0 || selectedBuilding == null || gridBuildSystem == null)
        {
            return;
        }

        EnsureLineStretchGhost();
        if (lineStretchGhost == null)
        {
            return;
        }

        int dx = currentCell.x - startCell.x;
        int dy = currentCell.y - startCell.y;
        bool horizontal = Mathf.Abs(dx) >= Mathf.Abs(dy);

        Vector2Int footprint = GetRotatedFootprintSize(selectedBuilding.Size, selectedDirection);
        int footAlong = Mathf.Max(1, horizontal ? footprint.x : footprint.y);
        int footPerp = Mathf.Max(1, horizontal ? footprint.y : footprint.x);
        float cell = gridBuildSystem.cellSize;
        int count = linePlacementCells.Count;

        float alongLength = count * footAlong * cell;
        float perpLength = footPerp * cell;
        float height = Mathf.Max(1, GetLocalHeightCells(selectedBuilding)) * cell;

        Vector3 worldFirst = GetBuildingWorldPosition(selectedBuilding, linePlacementCells[0], selectedDirection);
        Vector3 worldLast = GetBuildingWorldPosition(selectedBuilding, linePlacementCells[count - 1], selectedDirection);
        Vector3 lineCenter = (worldFirst + worldLast) * 0.5f;
        float centerY = GetLayerBottomY(baseLayer) + ghostHeightOffset + height * 0.5f;

        Transform t = lineStretchGhost.transform;
        t.rotation = gridBuildSystem.transform.rotation;
        t.position = new Vector3(lineCenter.x, centerY, lineCenter.z);
        t.localScale = horizontal
            ? new Vector3(alongLength, height, perpLength)
            : new Vector3(perpLength, height, alongLength);

        SetGhostTint(lineStretchMaterials, color, valid);
        lineStretchGhost.SetActive(true);
    }

    private void EnsureLineStretchGhost()
    {
        if (lineStretchGhost != null)
        {
            return;
        }

        lineStretchGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lineStretchGhost.name = "GhostLineStretch";
        Collider stretchCollider = lineStretchGhost.GetComponent<Collider>();
        if (stretchCollider != null)
        {
            DestroyUnityObject(stretchCollider);
        }

        lineStretchGhost.transform.SetParent(transform, false);
        lineStretchGhost.SetActive(false);

        lineStretchMaterials.Clear();
        MeshRenderer stretchRenderer = lineStretchGhost.GetComponent<MeshRenderer>();
        if (stretchRenderer != null && ghostPreviewMaterial != null)
        {
            Material materialInstance = new Material(ghostPreviewMaterial) { name = "GhostLineStretch_Instance" };
            stretchRenderer.sharedMaterial = materialInstance;
            lineStretchMaterials.Add(materialInstance);
        }
    }

    private void BuildLineAnchorCells(Vector2Int startCell, Vector2Int currentCell, List<Vector2Int> results)
    {
        results.Clear();

        if (selectedBuilding == null)
        {
            return;
        }

        int dx = currentCell.x - startCell.x;
        int dy = currentCell.y - startCell.y;
        bool horizontal = Mathf.Abs(dx) >= Mathf.Abs(dy);
        int distance = horizontal ? Mathf.Abs(dx) : Mathf.Abs(dy);
        int sign = (horizontal ? dx : dy) >= 0 ? 1 : -1;
        Vector2Int footprintSize = GetRotatedFootprintSize(selectedBuilding.Size, selectedDirection);
        int step = Mathf.Max(1, horizontal ? footprintSize.x : footprintSize.y);

        for (int offset = 0; offset <= distance; offset += step)
        {
            results.Add(horizontal
                ? new Vector2Int(startCell.x + offset * sign, startCell.y)
                : new Vector2Int(startCell.x, startCell.y + offset * sign));
        }
    }

    private void PlaceLinePreviewCells()
    {
        if (selectedBuilding == null || linePlacementQueries.Count == 0 || !currentCanPlace)
        {
            return;
        }

        placedTransactionScratch.Clear();
        for (int i = 0; i < linePlacementQueries.Count; i++)
        {
            if (!linePlacementQueries[i].Valid
                || !TryPlaceResolved(selectedBuilding, linePlacementQueries[i], out PlacedBuilding placed, false, true, false))
            {
                for (int j = placedTransactionScratch.Count - 1; j >= 0; j--)
                {
                    RemovePlacedBuildingImmediate(placedTransactionScratch[j], false);
                }

                SaveBuildings();
                placedTransactionScratch.Clear();
                return;
            }

            placedTransactionScratch.Add(placed);
        }

        SaveBuildings();
        placedTransactionScratch.Clear();
        RestoreAllFaded();
        SetLineGhostsVisible(false);
    }

    private bool CanPlace(BuildingData building, Vector2Int anchorCell, BuildingFootprint.Dir dir)
    {
        return TryBuildPlacementQuery(building, anchorCell, dir, AutoLayer, false, out PlacementQuery query)
            && query.Valid;
    }

    private bool TryFindValidationCell(out Vector2Int cell)
    {
        cell = default;
        if (selectedBuilding == null || gridBuildSystem == null)
        {
            return false;
        }

        int maxX = Mathf.Max(0, gridBuildSystem.gridWidth - 1);
        int maxY = Mathf.Max(0, gridBuildSystem.gridHeight - 1);
        for (int y = maxY; y >= 0; y--)
        {
            for (int x = maxX; x >= 0; x--)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (CanPlace(selectedBuilding, CenteredAnchorFromCell(x, y), selectedDirection))
                {
                    cell = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryFindValidationLineStart(int length, out Vector2Int startCell)
    {
        startCell = default;
        int clampedLength = Mathf.Max(1, length);
        if (selectedBuilding == null || gridBuildSystem == null || gridBuildSystem.gridWidth < clampedLength)
        {
            return false;
        }

        int maxX = Mathf.Max(0, gridBuildSystem.gridWidth - clampedLength);
        int maxY = Mathf.Max(0, gridBuildSystem.gridHeight - 1);
        for (int y = maxY; y >= 0; y--)
        {
            for (int x = maxX; x >= 0; x--)
            {
                bool valid = true;
                for (int i = 0; i < clampedLength; i++)
                {
                    Vector2Int candidate = new Vector2Int(x + i, y);
                    Vector2Int anchor = CenteredAnchorFromCell(candidate.x, candidate.y);
                    if (!TryBuildPlacementQuery(selectedBuilding, anchor, selectedDirection, FloorLayer, false, out PlacementQuery query)
                        || !query.Valid)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                {
                    startCell = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryFindStackableValidationBuilding(out int index)
    {
        index = -1;
        if (buildings == null)
        {
            return false;
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingData building = buildings[i];
            if (building == null
                || building.Prefab == null
                || building.Size != Vector2Int.one
                || !AllowsFloorPlacement(building)
                || !AllowsTopPlacement(building)
                || !building.SupportsTopPlacement)
            {
                continue;
            }

            index = i;
            return true;
        }

        return false;
    }

    private bool TryPlace(BuildingData building, Vector2Int anchorCell, BuildingFootprint.Dir dir)
    {
        if (!TryBuildPlacementQuery(building, anchorCell, dir, AutoLayer, false, out PlacementQuery query))
        {
            return false;
        }

        return TryPlaceResolved(building, query, out _, true, true, false);
    }

    private bool TryPlaceResolved(
        BuildingData building,
        PlacementQuery query,
        out PlacedBuilding placedBuilding,
        bool saveAfter,
        bool validateAgain,
        bool allowExistingLayerZeroGridCells)
    {
        placedBuilding = null;
        if (building == null || !query.Valid)
        {
            return false;
        }

        if (validateAgain && !ValidatePlacementQuery(building, query, allowExistingLayerZeroGridCells))
        {
            return false;
        }

        string buildingId = !string.IsNullOrEmpty(building.BuildingId) ? building.BuildingId : building.name;
        List<Vector2Int> gridCellsPlaced = new List<Vector2Int>();

        for (int i = 0; i < query.Blocks.Count; i++)
        {
            Vector3Int blockCell = query.Blocks[i].Cell;
            if (blockCell.y != FloorLayer)
            {
                continue;
            }

            Vector2Int pathCell = new Vector2Int(blockCell.x, blockCell.z);
            if (gridBuildSystem.TryGet(pathCell, out _))
            {
                continue;
            }

            if (!gridBuildSystem.PlaceAt(pathCell, buildingId))
            {
                RollBackPlacedCells(gridCellsPlaced);
                return false;
            }

            gridCellsPlaced.Add(pathCell);
        }

        GameObject instance = CreatePlacedBuildingInstance(building, query.AnchorCell, query.Direction, query.BaseLayer);
        placedBuilding = new PlacedBuilding(
            nextPlacedBuildingId++,
            query.AnchorCell,
            buildingId,
            query.Direction,
            query.BaseLayer,
            query.Blocks,
            instance,
            building.SupportsTopPlacement);

        for (int i = 0; i < query.Blocks.Count; i++)
        {
            BlockPlacementCell block = query.Blocks[i];
            occupiedBlocksByCell[block.Cell] = new PlacedBlockCell(placedBuilding, block.Block);

            if (block.Cell.y == FloorLayer)
            {
                HideGridCellVisual(new Vector2Int(block.Cell.x, block.Cell.z), instance);
            }
        }

        placedBuildings.Add(placedBuilding);
        if (saveAfter)
        {
            SaveBuildings();
        }

        if (HasLayerZeroBlock(query.Blocks))
        {
            NotifyPathfindingProjectionChanged();
        }

        RestoreAllFaded();
        SuppressFadeUntilGhostMoves(query.AnchorCell);
        return true;
    }

    private bool TryBuildPlacementQuery(
        BuildingData building,
        Vector2Int anchorCell,
        BuildingFootprint.Dir dir,
        int preferredBaseLayer,
        bool allowExistingLayerZeroGridCells,
        out PlacementQuery query)
    {
        query = default;

        if (building == null || gridBuildSystem == null)
        {
            return false;
        }

        if (preferredBaseLayer == AutoLayer)
        {
            if (AllowsFloorPlacement(building))
            {
                PlacementQuery floorQuery = CreatePlacementQuery(building, anchorCell, dir, FloorLayer);
                floorQuery.Valid = ValidatePlacementQuery(building, floorQuery, allowExistingLayerZeroGridCells);
                if (floorQuery.Valid || !AllowsTopPlacement(building))
                {
                    query = floorQuery;
                    return true;
                }
            }

            if (AllowsTopPlacement(building))
            {
                int topLayer = GetRecommendedTopBaseLayer(building, anchorCell, dir);
                query = CreatePlacementQuery(building, anchorCell, dir, topLayer);
                query.Valid = ValidatePlacementQuery(building, query, allowExistingLayerZeroGridCells);
                return true;
            }

            query = CreatePlacementQuery(building, anchorCell, dir, FloorLayer);
            query.Valid = false;
            return true;
        }

        int baseLayer = NormalizeLayer(preferredBaseLayer);
        query = CreatePlacementQuery(building, anchorCell, dir, baseLayer);
        query.Valid = ValidatePlacementQuery(building, query, allowExistingLayerZeroGridCells);
        return true;
    }

    private PlacementQuery CreatePlacementQuery(BuildingData building, Vector2Int anchorCell, BuildingFootprint.Dir dir, int baseLayer)
    {
        List<BuildingFootprint.ResolvedBlockCell> resolvedBlocks =
            BuildingFootprint.GetOccupiedBlockCells(anchorCell, baseLayer, building, dir);
        List<BlockPlacementCell> blocks = new List<BlockPlacementCell>(resolvedBlocks.Count);
        for (int i = 0; i < resolvedBlocks.Count; i++)
        {
            blocks.Add(new BlockPlacementCell(resolvedBlocks[i].Cell, resolvedBlocks[i].Block));
        }

        return new PlacementQuery(anchorCell, dir, baseLayer, false, blocks);
    }

    private bool ValidatePlacementQuery(BuildingData building, PlacementQuery query, bool allowExistingLayerZeroGridCells)
    {
        if (building == null || query.Blocks == null || query.Blocks.Count == 0)
        {
            return false;
        }

        if (!IsPlacementSurfaceAllowed(building, query.BaseLayer))
        {
            return false;
        }

        validationCellsScratch.Clear();
        for (int i = 0; i < query.Blocks.Count; i++)
        {
            BlockPlacementCell block = query.Blocks[i];
            Vector3Int cell = block.Cell;
            if (!IsWithinGrid(new Vector2Int(cell.x, cell.z))
                || cell.y < FloorLayer
                || cell.y > MaxBuildLayer
                || validationCellsScratch.ContainsKey(cell)
                || !IsCellAvailable(cell, allowExistingLayerZeroGridCells))
            {
                validationCellsScratch.Clear();
                return false;
            }

            validationCellsScratch.Add(cell, block);
        }

        for (int i = 0; i < query.Blocks.Count; i++)
        {
            BlockPlacementCell block = query.Blocks[i];
            Vector3Int cell = block.Cell;
            if (cell.y == FloorLayer || !block.RequiresSupport)
            {
                continue;
            }

            Vector3Int belowCell = new Vector3Int(cell.x, cell.y - 1, cell.z);
            if (validationCellsScratch.TryGetValue(belowCell, out BlockPlacementCell ownSupport))
            {
                if (ownSupport.ProvidesSupport)
                {
                    continue;
                }

                validationCellsScratch.Clear();
                return false;
            }

            if (!occupiedBlocksByCell.TryGetValue(belowCell, out PlacedBlockCell externalSupport)
                || !ProvidesExternalSupport(externalSupport))
            {
                validationCellsScratch.Clear();
                return false;
            }
        }

        validationCellsScratch.Clear();
        return true;
    }

    private bool IsPlacementSurfaceAllowed(BuildingData building, int baseLayer)
    {
        if (baseLayer <= FloorLayer)
        {
            return AllowsFloorPlacement(building);
        }

        return AllowsTopPlacement(building);
    }

    private bool IsCellAvailable(Vector3Int cell, bool allowExistingLayerZeroGridCells)
    {
        if (occupiedBlocksByCell.ContainsKey(cell))
        {
            return false;
        }

        if (cell.y == FloorLayer
            && !allowExistingLayerZeroGridCells
            && gridBuildSystem != null
            && gridBuildSystem.TryGet(new Vector2Int(cell.x, cell.z), out _))
        {
            return false;
        }

        return true;
    }

    private int GetRecommendedTopBaseLayer(BuildingData building, Vector2Int anchorCell, BuildingFootprint.Dir dir)
    {
        List<Vector2Int> footprint = BuildingFootprint.GetOccupiedCells(anchorCell, building, dir);
        bool hasAnySupport = false;
        int highest = FloorLayer - 1;
        for (int i = 0; i < footprint.Count; i++)
        {
            if (TryGetHighestOccupiedLayer(footprint[i], out int cellHighest))
            {
                hasAnySupport = true;
                highest = Mathf.Max(highest, cellHighest);
            }
        }

        return hasAnySupport ? NormalizeLayer(highest + 1) : 1;
    }

    private bool TryGetHighestOccupiedLayer(Vector2Int cell, out int highestLayer)
    {
        highestLayer = FloorLayer - 1;
        bool found = false;
        foreach (Vector3Int occupiedCell in occupiedBlocksByCell.Keys)
        {
            if (occupiedCell.x != cell.x || occupiedCell.z != cell.y)
            {
                continue;
            }

            highestLayer = Mathf.Max(highestLayer, occupiedCell.y);
            found = true;
        }

        return found;
    }

    private bool TryRemove(Vector2Int cell)
    {
        return TryGetRemoveTargetAtCell(cell, out RemoveTarget target)
            && RemoveResolvedTarget(target);
    }

    private bool TryGetPlacedBuildingAtCell(Vector2Int cell, out PlacedBuilding building)
    {
        return TryGetHighestPlacedBuildingAt(cell, out building);
    }

    private bool TryGetHighestPlacedBuildingAt(Vector2Int cell, out PlacedBuilding building)
    {
        building = null;
        int highestLayer = FloorLayer - 1;
        foreach (KeyValuePair<Vector3Int, PlacedBlockCell> entry in occupiedBlocksByCell)
        {
            Vector3Int occupiedCell = entry.Key;
            if (occupiedCell.x != cell.x || occupiedCell.z != cell.y || occupiedCell.y < highestLayer)
            {
                continue;
            }

            highestLayer = occupiedCell.y;
            building = entry.Value != null ? entry.Value.Building : null;
        }

        return building != null;
    }

    private bool TryGetRemoveTargetAtCell(Vector2Int cell, out RemoveTarget target)
    {
        target = default;
        if (gridBuildSystem == null)
        {
            return false;
        }

        if (TryGetHighestPlacedBuildingAt(cell, out PlacedBuilding building))
        {
            target = RemoveTarget.ForBuilding(building, cell);
            return true;
        }

        if (gridBuildSystem.TryGet(cell, out _))
        {
            target = RemoveTarget.ForGridCell(cell);
            return true;
        }

        return false;
    }

    private bool TryGetClosestOccupiedCell(PlacedBuilding building, Vector2 gridPoint, out Vector2Int cell)
    {
        cell = default;
        if (building == null || building.OccupiedCells == null || building.OccupiedCells.Count == 0)
        {
            return false;
        }

        float bestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < building.OccupiedCells.Count; i++)
        {
            Vector3Int occupied = building.OccupiedCells[i];
            Vector2Int candidate = new Vector2Int(occupied.x, occupied.z);
            Vector2 center = new Vector2(candidate.x + 0.5f, candidate.y + 0.5f);
            float distance = (center - gridPoint).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            cell = candidate;
            found = true;
        }

        return found;
    }

    private bool RemoveResolvedTarget(RemoveTarget target)
    {
        if (!target.IsValid || gridBuildSystem == null)
        {
            return false;
        }

        if (target.Building != null)
        {
            if (!placedBuildings.Contains(target.Building))
            {
                return false;
            }

            RemovePlacedBuildingCascade(target.Building);
            return true;
        }

        return target.HasGridCell
            && gridBuildSystem.TryGet(target.Cell, out _)
            && gridBuildSystem.RemoveAt(target.Cell);
    }

    private void RemovePlacedBuildingCascade(PlacedBuilding root)
    {
        if (root == null || !placedBuildings.Contains(root))
        {
            return;
        }

        cascadeSetScratch.Clear();
        cascadeScratch.Clear();
        cascadeSetScratch.Add(root);
        cascadeScratch.Add(root);

        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < placedBuildings.Count; i++)
            {
                PlacedBuilding candidate = placedBuildings[i];
                if (candidate == null || cascadeSetScratch.Contains(candidate))
                {
                    continue;
                }

                if (!WillBuildingRemainSupported(candidate, cascadeSetScratch))
                {
                    cascadeSetScratch.Add(candidate);
                    cascadeScratch.Add(candidate);
                    changed = true;
                }
            }
        }
        while (changed);

        cascadeScratch.Sort((a, b) => b.MaxLayer.CompareTo(a.MaxLayer));
        for (int i = 0; i < cascadeScratch.Count; i++)
        {
            RemovePlacedBuildingImmediate(cascadeScratch[i]);
        }

        SaveBuildings();
        cascadeSetScratch.Clear();
        cascadeScratch.Clear();
    }

    private bool WillBuildingRemainSupported(PlacedBuilding building, HashSet<PlacedBuilding> removedBuildings)
    {
        if (building == null)
        {
            return false;
        }

        validationCellsScratch.Clear();
        for (int i = 0; i < building.Blocks.Count; i++)
        {
            validationCellsScratch[building.Blocks[i].Cell] = building.Blocks[i];
        }

        for (int i = 0; i < building.Blocks.Count; i++)
        {
            BlockPlacementCell block = building.Blocks[i];
            Vector3Int cell = block.Cell;
            if (cell.y == FloorLayer || !block.RequiresSupport)
            {
                continue;
            }

            Vector3Int belowCell = new Vector3Int(cell.x, cell.y - 1, cell.z);
            if (validationCellsScratch.TryGetValue(belowCell, out BlockPlacementCell ownSupport))
            {
                if (ownSupport.ProvidesSupport)
                {
                    continue;
                }

                validationCellsScratch.Clear();
                return false;
            }

            if (!occupiedBlocksByCell.TryGetValue(belowCell, out PlacedBlockCell externalSupport)
                || externalSupport.Building == null
                || removedBuildings.Contains(externalSupport.Building)
                || !ProvidesExternalSupport(externalSupport))
            {
                validationCellsScratch.Clear();
                return false;
            }
        }

        validationCellsScratch.Clear();
        return true;
    }

    private void RemovePlacedBuildingImmediate(PlacedBuilding building)
    {
        RemovePlacedBuildingImmediate(building, true);
    }

    private void RemovePlacedBuildingImmediate(PlacedBuilding building, bool notifyPathfinding)
    {
        if (building == null)
        {
            return;
        }

        DropFadeStateForRemovedBuilding(building);
        bool pathfindingChanged = false;

        for (int i = 0; i < building.OccupiedCells.Count; i++)
        {
            Vector3Int cell = building.OccupiedCells[i];
            if (occupiedBlocksByCell.TryGetValue(cell, out PlacedBlockCell placedCell)
                && placedCell != null
                && placedCell.Building == building)
            {
                occupiedBlocksByCell.Remove(cell);
            }

            if (cell.y == FloorLayer)
            {
                pathfindingChanged = true;
                Vector2Int pathCell = new Vector2Int(cell.x, cell.z);
                if (gridBuildSystem != null && gridBuildSystem.TryGet(pathCell, out _))
                {
                    gridBuildSystem.RemoveAt(pathCell);
                }
            }
        }

        if (building.Instance != null)
        {
            DestroyUnityObject(building.Instance);
        }

        placedBuildings.Remove(building);

        if (notifyPathfinding && pathfindingChanged)
        {
            NotifyPathfindingProjectionChanged();
        }
    }

    private bool IsWithinGrid(Vector2Int cell)
    {
        return gridBuildSystem != null
            && cell.x >= 0
            && cell.x < gridBuildSystem.gridWidth
            && cell.y >= 0
            && cell.y < gridBuildSystem.gridHeight;
    }

    private int CountLayerZeroBuildingBlocks()
    {
        int count = 0;
        foreach (Vector3Int cell in occupiedBlocksByCell.Keys)
        {
            if (cell.y == FloorLayer)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasLayerZeroBlock(List<BlockPlacementCell> blocks)
    {
        if (blocks == null)
        {
            return false;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Cell.y == FloorLayer)
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyPathfindingProjectionChanged()
    {
        PathfindingProjectionChanged?.Invoke();
    }

    private GameObject CreatePlacedBuildingInstance(BuildingData building, Vector2Int anchorCell, BuildingFootprint.Dir dir, int baseLayer)
    {
        if (building.Prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(building.Prefab);
        instance.name = $"GridBuilding_L{baseLayer}_{anchorCell.x}_{anchorCell.y}_{building.BuildingId}";
        if (gridBuildSystem.gridRoot != null)
        {
            instance.transform.SetParent(gridBuildSystem.gridRoot, true);
        }

        PositionBuildingInstance(instance, building, anchorCell, dir, baseLayer, 0f);
        return instance;
    }

    private Vector3 GetBuildingWorldPosition(BuildingData building, Vector2Int anchorCell)
    {
        return GetBuildingWorldPosition(building, anchorCell, BuildingFootprint.Dir.Down);
    }

    private Vector3 GetBuildingWorldPosition(BuildingData building, Vector2Int anchorCell, BuildingFootprint.Dir dir)
    {
        if (building == null)
        {
            return gridBuildSystem.GridToWorld(anchorCell);
        }

        return gridBuildSystem.GridToWorld(anchorCell, GetRotatedFootprintSize(building.Size, dir));
    }

    private Quaternion GetBuildingWorldRotation(BuildingFootprint.Dir dir)
    {
        return gridBuildSystem.transform.rotation * Quaternion.Euler(0f, GetDirectionYaw(dir), 0f);
    }

    private void PositionBuildingInstance(
        GameObject instance,
        BuildingData building,
        Vector2Int anchorCell,
        BuildingFootprint.Dir dir,
        int baseLayer,
        float yLift)
    {
        if (instance == null)
        {
            return;
        }

        Vector3 target = GetBuildingWorldPosition(building, anchorCell, dir);
        float bottomY = GetLayerBottomY(baseLayer) + yLift;
        instance.transform.rotation = GetBuildingWorldRotation(dir);
        instance.transform.position = target;

        if (TryGetRenderersBounds(instance, out Bounds bounds))
        {
            Vector3 delta = new Vector3(
                target.x - bounds.center.x,
                bottomY - bounds.min.y,
                target.z - bounds.center.z);
            instance.transform.position += delta;
        }
        else
        {
            instance.transform.position = new Vector3(target.x, bottomY + gridBuildSystem.cellSize * 0.5f, target.z);
        }
    }

    private float GetLayerBottomY(int layer)
    {
        if (gridBuildSystem == null)
        {
            return Mathf.Max(0, layer);
        }

        return gridBuildSystem.transform.position.y + Mathf.Max(0, layer) * gridBuildSystem.cellSize;
    }

    private bool TryGetRenderersBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        if (go == null)
        {
            return false;
        }

        boundsScratch.Clear();
        go.GetComponentsInChildren(false, boundsScratch);
        bool any = false;
        for (int i = 0; i < boundsScratch.Count; i++)
        {
            Renderer r = boundsScratch[i];
            if (r == null)
            {
                continue;
            }

            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        boundsScratch.Clear();
        return any;
    }

    private void HideGridCellVisual(Vector2Int cell, GameObject replacement)
    {
        if (!gridBuildSystem.TryGet(cell, out GridCellData cellData) || cellData == null)
        {
            return;
        }

        if (cellData.Occupant != null && cellData.Occupant != replacement)
        {
            cellData.Occupant.SetActive(false);
        }

        if (replacement != null)
        {
            cellData.Occupant = replacement;
        }
    }

    private GridLevelData LevelData()
    {
        return gridBuildSystem != null ? gridBuildSystem.levelData : null;
    }

    private void SaveBuildings()
    {
        GridLevelData level = LevelData();
        if (level == null)
        {
            return;
        }

        level.buildings ??= new List<GridLevelData.BuildingRecord>();
        level.buildings.Clear();
        for (int i = 0; i < placedBuildings.Count; i++)
        {
            PlacedBuilding b = placedBuildings[i];
            if (b == null)
            {
                continue;
            }

            level.buildings.Add(new GridLevelData.BuildingRecord
            {
                buildingId = b.BuildingId,
                anchorX = b.AnchorCell.x,
                anchorY = b.AnchorCell.y,
                direction = (int)b.Direction,
                layer = b.BaseLayer,
            });
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(level);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(level);
#endif
    }

    private void ReconstructSavedBuildings()
    {
        GridLevelData level = LevelData();
        if (level == null || level.buildings == null)
        {
            return;
        }

        for (int layer = FloorLayer; layer <= MaxBuildLayer; layer++)
        {
            for (int i = 0; i < level.buildings.Count; i++)
            {
                GridLevelData.BuildingRecord record = level.buildings[i];
                if (record != null && NormalizeLayer(record.layer) == layer)
                {
                    RestoreBuilding(record);
                }
            }
        }
    }

    private void RestoreBuilding(GridLevelData.BuildingRecord record)
    {
        BuildingData data = FindBuildingDataById(record.buildingId);
        if (data == null || data.Prefab == null || gridBuildSystem == null)
        {
            return;
        }

        Vector2Int anchorCell = new Vector2Int(record.anchorX, record.anchorY);
        BuildingFootprint.Dir dir = NormalizeDirection(record.direction);
        int baseLayer = NormalizeLayer(record.layer);
        if (!TryBuildPlacementQuery(data, anchorCell, dir, baseLayer, true, out PlacementQuery query) || !query.Valid)
        {
            return;
        }

        TryPlaceResolved(data, query, out _, false, false, true);
    }

    private BuildingData FindBuildingDataById(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId) || buildings == null)
        {
            return null;
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] != null && buildings[i].BuildingId == buildingId)
            {
                return buildings[i];
            }
        }

        return null;
    }

    private static bool AllowsFloorPlacement(BuildingData building)
    {
        return building != null
            && (building.BuildSurface == BuildSurface.FloorOnly
                || building.BuildSurface == BuildSurface.FloorOrTop);
    }

    private static bool AllowsTopPlacement(BuildingData building)
    {
        return building != null
            && (building.BuildSurface == BuildSurface.TopOnly
                || building.BuildSurface == BuildSurface.FloorOrTop);
    }

    private static bool ProvidesExternalSupport(PlacedBlockCell cell)
    {
        return cell != null
            && cell.Building != null
            && cell.Building.SupportsTopPlacement
            && cell.Block != null
            && cell.Block.ProvidesSupport;
    }

    private int GetLocalHeightCells(BuildingData building)
    {
        if (building == null)
        {
            return 1;
        }

        BuildingBlockCell[] cells = building.GetBlockCells();
        int maxLayer = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null)
            {
                maxLayer = Mathf.Max(maxLayer, cells[i].LocalCell.y);
            }
        }

        return maxLayer + 1;
    }

    private void RollBackPlacedCells(List<Vector2Int> placedCells)
    {
        for (int i = 0; i < placedCells.Count; i++)
        {
            if (gridBuildSystem.TryGet(placedCells[i], out _))
            {
                gridBuildSystem.RemoveAt(placedCells[i]);
            }
        }
    }

    private void RebuildGhost()
    {
        if (removeToolSelected)
        {
            DestroyGhost();
            return;
        }

        DestroyGhost();

        if (selectedBuilding == null || selectedBuilding.Prefab == null)
        {
            return;
        }

        ghostInstance = Instantiate(selectedBuilding.Prefab);
        ghostInstance.name = $"Ghost_{selectedBuilding.BuildingId}";

        DisableGhostColliders(ghostInstance);
        CacheGhostRenderers();
        SetGhostVisible(false);
    }

    private void RebuildRemoveGhost(PlacedBuilding building)
    {
        DestroyRemoveGhost();
        if (building == null)
        {
            return;
        }

        BuildingData data = FindBuildingDataById(building.BuildingId);
        GameObject source = data != null && data.Prefab != null
            ? data.Prefab
            : building.Instance;
        if (source == null)
        {
            return;
        }

        removeGhostInstance = Instantiate(source);
        removeGhostInstance.name = $"GhostRemove_{building.InstanceId}_{building.BuildingId}";
        removeGhostInstance.transform.SetParent(transform, false);
        DisableGhostColliders(removeGhostInstance);
        CacheRemoveGhostRenderers();
        currentRemovePreviewBuilding = building;
        SetRemoveGhostVisible(false);
    }

    private void SuppressFadeUntilGhostMoves(Vector2Int anchorCell)
    {
        suppressFadeUntilGhostMoves = true;
        suppressFadeAnchorCell = anchorCell;
    }

    private void ClearPostPlacementFadeSuppression()
    {
        suppressFadeUntilGhostMoves = false;
    }

    private void CacheGhostRenderers()
    {
        ghostRenderers.Clear();
        DestroyGhostMaterialInstances();

        if (ghostInstance == null)
        {
            return;
        }

        ghostInstance.GetComponentsInChildren(true, ghostRenderers);
        for (int i = 0; i < ghostRenderers.Count; i++)
        {
            Renderer ghostRenderer = ghostRenderers[i];
            if (ghostRenderer != null && ghostPreviewMaterial != null)
            {
                Material materialInstance = new Material(ghostPreviewMaterial)
                {
                    name = $"{ghostPreviewMaterial.name}_{selectedBuilding.BuildingId}_Instance"
                };

                ghostRenderer.sharedMaterial = materialInstance;
                ghostMaterialInstances.Add(materialInstance);
            }
        }
    }

    private void CacheRemoveGhostRenderers()
    {
        removeGhostRenderers.Clear();
        DestroyRemoveGhostMaterialInstances();

        if (removeGhostInstance == null)
        {
            return;
        }

        removeGhostInstance.GetComponentsInChildren(true, removeGhostRenderers);
        for (int i = 0; i < removeGhostRenderers.Count; i++)
        {
            Renderer ghostRenderer = removeGhostRenderers[i];
            if (ghostRenderer != null && ghostPreviewMaterial != null)
            {
                Material materialInstance = new Material(ghostPreviewMaterial)
                {
                    name = $"{ghostPreviewMaterial.name}_Remove_Instance"
                };

                ghostRenderer.sharedMaterial = materialInstance;
                removeGhostMaterialInstances.Add(materialInstance);
            }
        }
    }

    private void DisableGhostColliders(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void SetGhostTint(Color color, bool canPlace)
    {
        SetGhostTint(ghostMaterialInstances, color, canPlace);
    }

    private void SetGhostTint(List<Material> materials, Color color, bool canPlace)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            SetGhostTint(materials[i], color, canPlace);
        }
    }

    private void SetGhostTint(Material material, Color color, bool canPlace)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(EmissionColorId))
        {
            material.SetColor(EmissionColorId, color);
        }

        if (material.HasProperty(FillStrengthId))
        {
            material.SetFloat(FillStrengthId, canPlace ? validGhostFill : invalidGhostFill);
        }

        if (material.HasProperty(GlowStrengthId))
        {
            material.SetFloat(GlowStrengthId, canPlace ? validGhostGlow : invalidGhostGlow);
        }
    }

    private void SetGhostVisible(bool visible)
    {
        if (ghostInstance != null && ghostInstance.activeSelf != visible)
        {
            ghostInstance.SetActive(visible);
        }
    }

    private void SetLineGhostsVisible(bool visible)
    {
        if (lineStretchGhost != null)
        {
            lineStretchGhost.SetActive(visible);
        }
    }

    private void SetRemoveGhostVisible(bool visible)
    {
        if (removeGhostInstance != null && removeGhostInstance.activeSelf != visible)
        {
            removeGhostInstance.SetActive(visible);
        }
    }

    private void DestroyGhost()
    {
        RestoreAllFaded();
        DestroyLineGhosts();

        if (ghostInstance == null)
        {
            return;
        }

        DestroyUnityObject(ghostInstance);
        ghostInstance = null;
        ghostRenderers.Clear();
        DestroyGhostMaterialInstances();
    }

    private void DestroyRemoveGhost()
    {
        if (removeGhostInstance != null)
        {
            DestroyUnityObject(removeGhostInstance);
            removeGhostInstance = null;
        }

        removeGhostRenderers.Clear();
        currentRemovePreviewBuilding = null;
        DestroyRemoveGhostMaterialInstances();
    }

    private void DestroyLineGhosts()
    {
        DestroyMaterialList(lineStretchMaterials);
        if (lineStretchGhost != null)
        {
            DestroyUnityObject(lineStretchGhost);
            lineStretchGhost = null;
        }
    }

    private void DestroyGhostMaterialInstances()
    {
        DestroyMaterialList(ghostMaterialInstances);
    }

    private void DestroyRemoveGhostMaterialInstances()
    {
        DestroyMaterialList(removeGhostMaterialInstances);
    }

    private void DestroyMaterialList(List<Material> materials)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material material = materials[i];
            if (material != null)
            {
                DestroyUnityObject(material);
            }
        }

        materials.Clear();
    }

    private void DropFadeStateForRemovedBuilding(PlacedBuilding building)
    {
        _ = building;
    }

    private void RestoreAllFaded()
    {
        _ = fadedAlpha;
    }

    private void ClearAllFadeCaches()
    {
        rendererScratch.Clear();
    }

    private void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private int IndexOf(BuildingData building)
    {
        if (building == null || buildings == null)
        {
            return selectedIndex;
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] == building)
            {
                return i;
            }
        }

        return selectedIndex;
    }

    private void AppendRuntimeBuilding(BuildingData building)
    {
        if (building == null)
        {
            return;
        }

        int oldLength = buildings != null ? buildings.Length : 0;
        Array.Resize(ref buildings, oldLength + 1);
        buildings[oldLength] = building;
    }

    private void UpdateGhostIfVisible()
    {
        if (ghostInstance == null || !ghostInstance.activeSelf)
        {
            return;
        }

        UpdateGhost(currentGhostAnchorCell);
    }

    private static BuildingFootprint.Dir NormalizeDirection(int direction)
    {
        int count = Enum.GetValues(typeof(BuildingFootprint.Dir)).Length;
        int normalized = ((direction % count) + count) % count;
        return (BuildingFootprint.Dir)normalized;
    }

    private static int NormalizeLayer(int layer)
    {
        return Mathf.Clamp(layer, FloorLayer, MaxBuildLayer);
    }

    private static BuildSurface NormalizeBuildSurface(int surface)
    {
        int count = Enum.GetValues(typeof(BuildSurface)).Length;
        int normalized = ((surface % count) + count) % count;
        return (BuildSurface)normalized;
    }

    private static float GetDirectionYaw(BuildingFootprint.Dir dir)
    {
        switch (dir)
        {
            case BuildingFootprint.Dir.Left:
                return -90f;
            case BuildingFootprint.Dir.Up:
                return 180f;
            case BuildingFootprint.Dir.Right:
                return 90f;
            default:
                return 0f;
        }
    }

    private static Vector2Int GetRotatedFootprintSize(Vector2Int size, BuildingFootprint.Dir dir)
    {
        int width = Mathf.Max(1, size.x);
        int height = Mathf.Max(1, size.y);

        switch (dir)
        {
            case BuildingFootprint.Dir.Left:
            case BuildingFootprint.Dir.Right:
                return new Vector2Int(height, width);
            default:
                return new Vector2Int(width, height);
        }
    }

    private static string FormatCells(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return "<empty>";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            builder.Append(cells[i].x);
            builder.Append(',');
            builder.Append(cells[i].y);
        }

        return builder.ToString();
    }

    private struct PlacementQuery
    {
        public readonly Vector2Int AnchorCell;
        public readonly BuildingFootprint.Dir Direction;
        public readonly int BaseLayer;
        public readonly List<BlockPlacementCell> Blocks;
        public bool Valid;

        public PlacementQuery(
            Vector2Int anchorCell,
            BuildingFootprint.Dir direction,
            int baseLayer,
            bool valid,
            List<BlockPlacementCell> blocks)
        {
            AnchorCell = anchorCell;
            Direction = direction;
            BaseLayer = NormalizeLayer(baseLayer);
            Valid = valid;
            Blocks = blocks;
        }
    }

    private readonly struct BlockPlacementCell
    {
        public readonly Vector3Int Cell;
        public readonly BuildingBlockCell Block;

        public bool RequiresSupport => Block == null || Block.RequiresSupport;
        public bool ProvidesSupport => Block != null && Block.ProvidesSupport;

        public BlockPlacementCell(Vector3Int cell, BuildingBlockCell block)
        {
            Cell = cell;
            Block = block;
        }
    }

    private readonly struct RemoveTarget
    {
        public readonly PlacedBuilding Building;
        public readonly Vector2Int Cell;
        public readonly bool HasGridCell;

        public bool IsValid => Building != null || HasGridCell;

        private RemoveTarget(PlacedBuilding building, Vector2Int cell, bool hasGridCell)
        {
            Building = building;
            Cell = cell;
            HasGridCell = hasGridCell;
        }

        public static RemoveTarget ForBuilding(PlacedBuilding building, Vector2Int cell)
        {
            return new RemoveTarget(building, cell, false);
        }

        public static RemoveTarget ForGridCell(Vector2Int cell)
        {
            return new RemoveTarget(null, cell, true);
        }
    }

    private sealed class PlacedBlockCell
    {
        public readonly PlacedBuilding Building;
        public readonly BuildingBlockCell Block;

        public PlacedBlockCell(PlacedBuilding building, BuildingBlockCell block)
        {
            Building = building;
            Block = block;
        }
    }

    private sealed class RemovePaintState
    {
        public readonly Material[] OriginalMaterials;
        public readonly MaterialPropertyBlock OriginalPropertyBlock;
        public readonly Material[] PreviewMaterials;

        public RemovePaintState(
            Material[] originalMaterials,
            MaterialPropertyBlock originalPropertyBlock,
            Material[] previewMaterials)
        {
            OriginalMaterials = originalMaterials;
            OriginalPropertyBlock = originalPropertyBlock;
            PreviewMaterials = previewMaterials;
        }

        public void DestroyPreviewMaterials(Action<UnityEngine.Object> destroy)
        {
            if (PreviewMaterials == null || destroy == null)
            {
                return;
            }

            for (int i = 0; i < PreviewMaterials.Length; i++)
            {
                Material material = PreviewMaterials[i];
                if (material != null
                    && (OriginalMaterials == null || !Array.Exists(OriginalMaterials, original => original == material)))
                {
                    destroy(material);
                }
            }
        }
    }

    private sealed class PlacedBuilding
    {
        public readonly int InstanceId;
        public readonly Vector2Int AnchorCell;
        public readonly string BuildingId;
        public readonly BuildingFootprint.Dir Direction;
        public readonly int BaseLayer;
        public readonly List<BlockPlacementCell> Blocks;
        public readonly List<Vector3Int> OccupiedCells;
        public readonly GameObject Instance;
        public readonly bool SupportsTopPlacement;
        public readonly int MaxLayer;

        public PlacedBuilding(
            int instanceId,
            Vector2Int anchorCell,
            string buildingId,
            BuildingFootprint.Dir direction,
            int baseLayer,
            List<BlockPlacementCell> blocks,
            GameObject instance,
            bool supportsTopPlacement)
        {
            InstanceId = instanceId;
            AnchorCell = anchorCell;
            BuildingId = buildingId;
            Direction = direction;
            BaseLayer = NormalizeLayer(baseLayer);
            Blocks = new List<BlockPlacementCell>(blocks);
            OccupiedCells = new List<Vector3Int>(blocks.Count);
            Instance = instance;
            SupportsTopPlacement = supportsTopPlacement;
            MaxLayer = BaseLayer;

            for (int i = 0; i < blocks.Count; i++)
            {
                OccupiedCells.Add(blocks[i].Cell);
                MaxLayer = Mathf.Max(MaxLayer, blocks[i].Cell.y);
            }
        }
    }
}
