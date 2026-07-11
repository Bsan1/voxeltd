using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voxel chain lightning. The bolt path is built with recursive midpoint displacement
/// (camera-facing offsets), then VOXELIZED: the path is rasterized into a grid and drawn
/// as a trail of emissive cubes (Minecraft-style), not a line. A bright pulse of voxels
/// travels along the main bolt. Glow comes from HDR emissive cubes + Bloom.
///
/// Points come from the Inspector (Transforms) or code (SetPoints). Cubes are pooled.
/// </summary>
[DisallowMultipleComponent]
public class ChainLightning : MonoBehaviour
{
    [Header("Points (Inspector) or call SetPoints(...)")]
    public List<Transform> points = new List<Transform>();

    [Header("Bolt path (midpoint displacement)")]
    [Range(1, 7)] public int generations = 4;
    [Tooltip("Max sideways displacement at the first generation (world units). Halves each generation.")]
    public float initialOffset = 0.6f;
    [Tooltip("Re-jitter interval (seconds).")]
    public float jitterInterval = 0.05f;

    [Header("Voxels")]
    [Tooltip("Size of each cube / the grid the bolt snaps to (world units).")]
    public float cubeSize = 0.2f;
    [Range(0.5f, 1f)] public float cubeFill = 0.95f;
    [Min(16)] public int maxCubes = 600;

    [Header("Branches / forks")]
    [Range(0f, 1f)] public float branchChance = 0.22f;
    public float branchAngleDeg = 35f;
    [Min(0)] public int maxBranches = 6;
    [Range(0f, 1f)] public float branchBrightness = 0.5f;

    [Header("Color & glow (HDR; needs Bloom)")]
    [ColorUsage(true, true)] public Color coreColor = new Color(3f, 3.2f, 5f, 1f);
    [Range(0f, 1f)] public float baseBrightness = 0.35f;

    [Header("Travel pulse")]
    public float pulseSpeed = 1.5f;
    [Range(0.02f, 0.5f)] public float pulseWidth = 0.15f;
    public bool loopPulse = true;

    [Header("Optional")]
    public Camera viewCamera;
    public Material cubeMaterialOverride;

    [Header("Visual settings (optional ScriptableObject)")]
    [Tooltip("If set, copies its visual values onto this bolt. Leave empty to use the fields above.")]
    public LightningVisualSettings visualSettings;
    [Tooltip("Copy from the asset every frame, so editing it updates the look live.")]
    public bool liveSyncSettings = false;

    // ---- runtime ----
    readonly List<Vector3> srcPts = new List<Vector3>();
    Vector3[] runtimePoints;
    bool useRuntimePoints;

    struct Cell { public Vector3Int c; public float t; public bool branch; }
    readonly List<Cell> cells = new List<Cell>();
    readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();

    readonly List<Transform> pool = new List<Transform>();
    readonly List<MeshRenderer> poolR = new List<MeshRenderer>();
    Mesh cubeMesh;
    Material cubeMat;
    MaterialPropertyBlock mpb;
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    float jitterTimer;
    int seed = 1;
    float pulsePos;

    // ---- Public API ----
    public void SetPoints(IList<Vector3> worldPoints)
    {
        runtimePoints = worldPoints != null ? new List<Vector3>(worldPoints).ToArray() : null;
        useRuntimePoints = runtimePoints != null && runtimePoints.Length >= 2;
    }
    public void SetPoints(IList<Transform> ts)
    {
        if (ts == null) { useRuntimePoints = false; return; }
        var arr = new Vector3[ts.Count];
        for (int i = 0; i < ts.Count; i++) arr[i] = ts[i] ? ts[i].position : Vector3.zero;
        SetPoints(arr);
    }
    public void Fire() => pulsePos = 0f;

    // ---- lifecycle ----
    void OnEnable() { if (visualSettings) visualSettings.ApplyTo(this); EnsureResources(); }

    void Update()
    {
        if (visualSettings && liveSyncSettings) visualSettings.ApplyTo(this);
        if (!GatherPoints()) { HideAll(); return; }

        jitterTimer += Time.deltaTime;
        if (cells.Count == 0 || jitterTimer >= jitterInterval) { jitterTimer = 0f; seed++; }
        Build();

        pulsePos += Time.deltaTime * pulseSpeed;
        pulsePos = loopPulse ? Mathf.Repeat(pulsePos, 1f) : Mathf.Clamp01(pulsePos);

        Render();
    }

    bool GatherPoints()
    {
        srcPts.Clear();
        if (useRuntimePoints && runtimePoints != null) srcPts.AddRange(runtimePoints);
        else foreach (var t in points) if (t) srcPts.Add(t.position);
        return srcPts.Count >= 2;
    }

    Vector3 ViewDir()
    {
        var cam = viewCamera ? viewCamera : Camera.main;
        return cam ? cam.transform.forward : Vector3.forward;
    }

    void Build()
    {
        cells.Clear();
        seen.Clear();
        var rng = new System.Random(seed);
        Vector3 camDir = ViewDir();
        float gs = Mathf.Max(cubeSize, 0.001f);

        // main bolt polyline through all chain points
        var main = new List<Vector3>();
        var branchSeeds = new List<(Vector3 a, Vector3 b)>();
        for (int i = 0; i < srcPts.Count - 1; i++)
        {
            var seg = Displace(srcPts[i], srcPts[i + 1], generations, initialOffset, camDir, rng, true, branchSeeds);
            for (int k = (i == 0 ? 0 : 1); k < seg.Count; k++) main.Add(seg[k]);
        }
        VoxelizePolyline(main, gs, false);

        int made = 0;
        foreach (var bs in branchSeeds)
        {
            if (made >= maxBranches) break;
            var br = Displace(bs.a, bs.b, Mathf.Max(2, generations - 1), initialOffset * 0.5f, camDir, rng, false, null);
            VoxelizePolyline(br, gs, true);
            made++;
        }
    }

    void VoxelizePolyline(List<Vector3> poly, float gs, bool branch)
    {
        if (poly.Count < 2) return;
        float total = 0f;
        for (int i = 0; i < poly.Count - 1; i++) total += Vector3.Distance(poly[i], poly[i + 1]);
        if (total < 1e-5f) total = 1f;

        float acc = 0f;
        for (int i = 0; i < poly.Count - 1; i++)
        {
            Vector3 a = poly[i], b = poly[i + 1];
            float segLen = Vector3.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / (gs * 0.5f)));
            for (int s = 0; s <= steps; s++)
            {
                float f = s / (float)steps;
                Vector3 p = Vector3.Lerp(a, b, f);
                var cell = new Vector3Int(
                    Mathf.RoundToInt(p.x / gs), Mathf.RoundToInt(p.y / gs), Mathf.RoundToInt(p.z / gs));
                if (seen.Add(cell))
                {
                    float t = branch ? -1f : (acc + segLen * f) / total;
                    cells.Add(new Cell { c = cell, t = t, branch = branch });
                    if (cells.Count >= maxCubes) return;
                }
            }
            acc += segLen;
        }
    }

    List<Vector3> Displace(Vector3 a, Vector3 b, int gens, float offset,
                           Vector3 camDir, System.Random rng, bool allowBranch,
                           List<(Vector3, Vector3)> branchOut)
    {
        var segs = new List<(Vector3 s, Vector3 e)> { (a, b) };
        float off = offset;
        for (int g = 0; g < gens; g++)
        {
            var next = new List<(Vector3, Vector3)>(segs.Count * 2);
            foreach (var (s, e) in segs)
            {
                Vector3 dir = e - s;
                float len = dir.magnitude;
                if (len < 1e-5f) { next.Add((s, e)); continue; }
                dir /= len;
                Vector3 perp = Vector3.Cross(camDir, dir);
                if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(Vector3.up, dir);
                perp.Normalize();
                float d = ((float)rng.NextDouble() * 2f - 1f) * off;
                Vector3 mid = (s + e) * 0.5f + perp * d;
                next.Add((s, mid));
                next.Add((mid, e));
                if (allowBranch && branchOut != null && rng.NextDouble() < branchChance)
                {
                    float ang = ((float)rng.NextDouble() * 2f - 1f) * branchAngleDeg;
                    Vector3 bdir = Quaternion.AngleAxis(ang, camDir) * (e - mid) * 0.7f;
                    branchOut.Add((mid, mid + bdir));
                }
            }
            segs = next;
            off *= 0.5f;
        }
        var pts = new List<Vector3>(segs.Count + 1) { segs[0].s };
        foreach (var (_, e) in segs) pts.Add(e);
        return pts;
    }

    // ---- rendering ----
    void EnsureResources()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        if (cubeMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
        }
        if (cubeMat == null)
        {
            if (cubeMaterialOverride) cubeMat = cubeMaterialOverride;
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (!sh) sh = Shader.Find("Unlit/Color");
                cubeMat = new Material(sh) { name = "LightningVoxel (auto)" };
                cubeMat.enableInstancing = true;
                if (cubeMat.HasProperty(BaseColorID)) cubeMat.SetColor(BaseColorID, coreColor);
            }
        }
    }

    void Render()
    {
        EnsureResources();
        float gs = Mathf.Max(cubeSize, 0.001f);
        Vector3 scale = Vector3.one * (gs * cubeFill);

        EnsurePool(cells.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < cells.Count;
            if (pool[i].gameObject.activeSelf != on) pool[i].gameObject.SetActive(on);
            if (!on) continue;

            var cell = cells[i];
            pool[i].position = (Vector3)cell.c * gs;
            pool[i].localScale = scale;

            float b = cell.branch
                ? branchBrightness * (0.6f + 0.4f * Mathf.PerlinNoise(i * 3.7f, Time.time * 9f))
                : PulseBrightness(cell.t);

            Color col = new Color(coreColor.r * b, coreColor.g * b, coreColor.b * b, 1f);
            poolR[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorID, col);
            poolR[i].SetPropertyBlock(mpb);
        }
    }

    float PulseBrightness(float t)
    {
        float dist = Mathf.Abs(t - pulsePos);
        if (loopPulse) dist = Mathf.Min(dist, 1f - dist);
        float bump = Mathf.Max(0f, 1f - dist / pulseWidth);
        return baseBrightness + (1f - baseBrightness) * bump;
    }

    void EnsurePool(int n)
    {
        while (pool.Count < n)
        {
            var go = new GameObject("_Voxel" + pool.Count);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = cubeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cubeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            pool.Add(go.transform);
            poolR.Add(mr);
        }
    }

    void HideAll()
    {
        foreach (var t in pool) if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
    }

    [ContextMenu("Rebuild Now")]
    void RebuildNow() { EnsureResources(); if (GatherPoints()) { seed++; Build(); Render(); } }
}
