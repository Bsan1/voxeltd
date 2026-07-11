using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Pathfinding;

/// <summary>
/// Toggleable overlay that shows which grid cells agents can actually reach (the flow
/// field's reachable area), via a URP Decal projected over the grid. Bakes the GridFlowField
/// into a point-filtered texture (A = reachable, B = distance-to-goal) and feeds the
/// PathOverlay decal shader. Re-bakes automatically when the field rebuilds (walls/towers
/// change the routes). Press the toggle key to show/hide.
///
/// Setup: add to a GameObject that also has a URP Decal Projector with the PathOverlay
/// material assigned; reference the GridFlowField. The projector is auto-sized to the grid.
/// </summary>
public class PathOverlay : MonoBehaviour
{
    [Tooltip("Auto-found at runtime if left empty.")]
    public GridFlowField flowField;
    [Tooltip("Auto-created on this object at runtime if left empty.")]
    public DecalProjector projector;
    [Tooltip("Decal material (PathOverlay.mat). Auto-assigned to the projector if it has none.")]
    public Material overlayMaterial;
    [Tooltip("Agents' spawner. Auto-found; its spawn cells are the trace start points. If empty, open border cells are used.")]
    public PathAgentSwarmSpawner spawner;

    [Header("Restrict to floor")]
    [Tooltip("Set to your floor's Rendering Layer so the overlay paints ONLY the floor, not agents. Requires 'Use Rendering Layers' enabled on the URP Decal feature. Leave as Nothing to affect everything.")]
    public RenderingLayerMask floorRenderingLayers;

    [Header("Toggle")]
    public Key toggleKey = Key.Tab;
    public bool startVisible = false;

    [Header("Projection box")]
    [Tooltip("Thin slab hugging the floor. Keep small so the overlay paints the floor but not agents standing on it.")]
    public float projectionDepth = 0.35f;

    [Header("Fix orientation if mirrored")]
    public bool flipU = false;
    public bool flipV = false;

    [Header("Debug")]
    [Tooltip("Logs reachable/total cells each bake.")]
    public bool debugLog = false;

    static readonly int FlowTexID = Shader.PropertyToID("_FlowTex");

    Texture2D tex;
    Material matInstance;
    bool visible;
    int lastRebuild = -1;

    void Awake()
    {
        EnsureProjector();
        if (!flowField) flowField = FindFirstObjectByType<GridFlowField>();
        if (!spawner) spawner = FindFirstObjectByType<PathAgentSwarmSpawner>();
        if (projector) projector.enabled = false;
        visible = false;
    }

    void EnsureProjector()
    {
        if (!projector) projector = GetComponent<DecalProjector>();
        if (!projector) projector = gameObject.AddComponent<DecalProjector>();
        if (projector.material == null && overlayMaterial != null) projector.material = overlayMaterial;
        if ((uint)floorRenderingLayers != 0u) projector.renderingLayerMask = floorRenderingLayers;
    }

    void Start() { if (startVisible) Show(); }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) Toggle();
        if (visible && flowField && flowField.RebuildCount != lastRebuild) Rebake();
    }

    public void Toggle() { if (visible) Hide(); else Show(); }

    public void Show()
    {
        EnsureProjector();
        if (!flowField) flowField = FindFirstObjectByType<GridFlowField>();
        if (!spawner) spawner = FindFirstObjectByType<PathAgentSwarmSpawner>();
        if (flowField == null || flowField.Grid == null || projector == null) return;
        EnsureMaterialInstance();
        FitProjectorToGrid();
        Rebake();
        projector.enabled = true;
        visible = true;
    }

    public void Hide()
    {
        if (projector) projector.enabled = false;
        visible = false;
    }

    void EnsureMaterialInstance()
    {
        // Instance the material so writing _FlowTex doesn't modify the shared asset.
        if (matInstance == null && projector.material != null)
        {
            matInstance = new Material(projector.material) { name = projector.material.name + " (overlay)" };
            projector.material = matInstance;
        }
    }

    void FitProjectorToGrid()
    {
        var grid = flowField.Grid;
        int w = grid.gridWidth, h = grid.gridHeight;
        float cs = grid.cellSize;

        Vector3 c0 = grid.CellToWorld(new Vector2Int(0, 0));
        Vector3 c1 = grid.CellToWorld(new Vector2Int(w - 1, h - 1));
        Vector3 center = (c0 + c1) * 0.5f;

        // Hug the actual floor plane so the thin box only contains the floor surface
        // (agents' tops sit above it -> they don't get painted).
        float floorY = grid.floorVisual ? grid.floorVisual.position.y : center.y;

        var t = projector.transform;
        t.rotation = Quaternion.Euler(90f, 0f, 0f);            // face straight down
        t.position = new Vector3(center.x, floorY, center.z);

        projector.pivot = Vector3.zero;
        projector.size = new Vector3(w * cs, h * cs, projectionDepth);
    }

    void Rebake()
    {
        lastRebuild = flowField.RebuildCount;
        var grid = flowField.Grid;
        int w = grid.gridWidth, h = grid.gridHeight;

        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex) Destroy(tex);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FlowOverlay" };
        }

        // Trace the routes agents actually take: greedy-follow the flow from every
        // spawn cell to the goal, marking each visited cell + its step direction.
        var onPath = new bool[w * h];
        var dirX = new sbyte[w * h];
        var dirY = new sbyte[w * h];
        int guardMax = w * h;
        foreach (var spawn in GetTraceStartCells(w, h))
        {
            var cell = spawn;
            for (int guard = 0; guard < guardMax; guard++)
            {
                if (!flowField.HasPath(cell)) break;
                int i = cell.y * w + cell.x;
                Vector2Int step = flowField.NextCell(cell) - cell;
                if (!onPath[i]) { onPath[i] = true; dirX[i] = (sbyte)step.x; dirY[i] = (sbyte)step.y; }
                if (step == Vector2Int.zero) break;   // reached the goal
                cell += step;
            }
        }

        // write pixels: A = on a route, RG = travel direction (for the arrows)
        int pathCount = 0;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool on = onPath[i];
                if (on) pathCount++;
                int ddx = dirX[i], ddy = dirY[i];
                if (flipU) ddx = -ddx;
                if (flipV) ddy = -ddy;
                byte r = (byte)Mathf.RoundToInt((ddx * 0.5f + 0.5f) * 255f);
                byte g = (byte)Mathf.RoundToInt((ddy * 0.5f + 0.5f) * 255f);
                byte a = (byte)(on ? 255 : 0);
                int tx = flipU ? (w - 1 - x) : x;
                int ty = flipV ? (h - 1 - y) : y;
                px[ty * w + tx] = new Color32(r, g, 0, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);

        var mat = projector.material;
        if (mat) mat.SetTexture(FlowTexID, tex);

        if (debugLog)
            Debug.Log($"[PathOverlay] baked {w}x{h}, route cells {pathCount}/{w * h}", this);
    }

    // Where to start tracing routes: the spawner's actual spawn cells if available,
    // otherwise every open border cell that can reach the goal.
    List<Vector2Int> GetTraceStartCells(int w, int h)
    {
        if (spawner != null)
        {
            var s = spawner.GetSpawnCells();
            if (s != null && s.Count > 0) return s;
        }
        var list = new List<Vector2Int>();
        for (int x = 0; x < w; x++) { TryAddStart(list, x, 0); TryAddStart(list, x, h - 1); }
        for (int y = 0; y < h; y++) { TryAddStart(list, 0, y); TryAddStart(list, w - 1, y); }
        return list;
    }

    void TryAddStart(List<Vector2Int> list, int x, int y)
    {
        var c = new Vector2Int(x, y);
        if (flowField.HasPath(c)) list.Add(c);
    }
}
