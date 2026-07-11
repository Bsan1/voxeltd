using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Voxel "charge up" effect for an ability wind-up: cubes converge inward toward a growing,
/// pulsing core with small contained crackle-sparks around it. Intensity ramps over a timed
/// charge, then a bright flash on release. Fires OnReleased (you trigger the actual bolt).
///
/// Usage:
///   var c = obj.GetComponent&lt;VoxelChargeUp&gt;();
///   c.OnReleased += () =&gt; chainLightning.Fire();   // or hook the onReleased UnityEvent
///   c.StartCharge();           // uses chargeDuration
///   // ... or c.StartCharge(0.8f);  c.Cancel();  c.Release();
/// All emissive (HDR) + Bloom for glow. Cubes are pooled.
/// </summary>
[DisallowMultipleComponent]
public class VoxelChargeUp : MonoBehaviour
{
    enum State { Idle, Charging, Flash }

    [Header("Timing")]
    public float chargeDuration = 1.2f;
    public float flashDuration = 0.14f;

    [Header("Voxels")]
    public float cubeSize = 0.12f;
    [Range(0.5f, 1f)] public float cubeFill = 0.95f;

    [Header("Core (growing + pulsing)")]
    public float coreMinSize = 0.18f;
    public float coreMaxSize = 0.55f;
    public float corePulseSpeed = 9f;

    [Header("Converging cubes")]
    [Min(0)] public int convergeCount = 26;
    public float gatherRadius = 1.8f;
    public float convergeSpeed = 0.8f;

    [Header("Crackle sparks (contained)")]
    [Min(0)] public int sparkCount = 12;
    public float sparkRadius = 0.5f;
    public float sparkInterval = 0.05f;

    [Header("Color (HDR; needs Bloom)")]
    [ColorUsage(true, true)] public Color coreColor = new Color(3f, 3.2f, 5f, 1f);
    [ColorUsage(true, true)] public Color sparkColor = new Color(2.2f, 2.6f, 5f, 1f);
    [Range(0f, 1f)] public float startBrightness = 0.35f;
    public float flashBoost = 1.8f;

    [Header("Release")]
    public UnityEvent onReleased;
    public event Action OnReleased;

    [Header("Optional")]
    public Material cubeMaterialOverride;

    public bool IsCharging => state == State.Charging;
    public float Progress { get; private set; }

    // ---- runtime ----
    State state = State.Idle;
    float tCharge, tFlash, sparkTimer;
    Vector3[] convergeDirs;
    Vector3[] sparkOffsets;

    readonly List<Transform> pool = new List<Transform>();
    readonly List<MeshRenderer> poolR = new List<MeshRenderer>();
    int used;
    Mesh cubeMesh;
    Material cubeMat;
    MaterialPropertyBlock mpb;
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    // ---- API ----
    public void StartCharge() => StartCharge(chargeDuration);
    public void StartCharge(float duration)
    {
        chargeDuration = Mathf.Max(0.01f, duration);
        tCharge = 0f; Progress = 0f; state = State.Charging;
        EnsureResources();
    }
    public void Cancel() { state = State.Idle; HideAll(); }
    public void Release()
    {
        if (state == State.Idle) return;
        onReleased?.Invoke();
        OnReleased?.Invoke();
        state = State.Flash; tFlash = 0f;
    }

    // ---- lifecycle ----
    void OnEnable() { EnsureResources(); BuildDirections(); }

    void Update()
    {
        switch (state)
        {
            case State.Idle:
                return;
            case State.Charging:
                tCharge += Time.deltaTime;
                Progress = Mathf.Clamp01(tCharge / chargeDuration);
                RenderCharge(Progress, 1f);
                if (Progress >= 1f) Release();
                break;
            case State.Flash:
                tFlash += Time.deltaTime;
                float f = 1f - Mathf.Clamp01(tFlash / flashDuration);
                RenderCharge(1f, 1f + flashBoost * f);
                if (tFlash >= flashDuration) { state = State.Idle; HideAll(); }
                break;
        }
    }

    void BuildDirections()
    {
        // stable, evenly distributed directions (Fibonacci sphere) so converging streams don't jitter
        convergeDirs = new Vector3[Mathf.Max(1, convergeCount)];
        float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < convergeDirs.Length; i++)
        {
            float y = 1f - (i + 0.5f) / convergeDirs.Length * 2f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = ga * i;
            convergeDirs[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }
        sparkOffsets = new Vector3[Mathf.Max(1, sparkCount)];
        ReseedSparks();
    }

    void ReseedSparks()
    {
        if (sparkOffsets == null) return;
        for (int i = 0; i < sparkOffsets.Length; i++)
            sparkOffsets[i] = UnityEngine.Random.insideUnitSphere * sparkRadius;
    }

    void RenderCharge(float progress, float intensityMul)
    {
        EnsureResources();
        float gs = Mathf.Max(cubeSize, 0.001f);
        Vector3 center = transform.position;
        float ramp = Mathf.Lerp(startBrightness, 1f, progress * progress) * intensityMul;
        used = 0;

        // --- growing pulsing core (center + 6 axis blocks) ---
        float coreSize = Mathf.Lerp(coreMinSize, coreMaxSize, progress);
        float pulse = 1f + 0.18f * Mathf.Sin(Time.time * corePulseSpeed);
        float coreScale = coreSize * pulse;
        AddCube(center, coreScale, coreColor, ramp);
        Vector3[] ax = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        float armDist = coreSize * 0.7f;
        float armScale = coreSize * 0.55f * pulse;
        for (int i = 0; i < 6; i++)
            AddCube(center + ax[i] * armDist, armScale, coreColor, ramp * 0.85f);

        // --- converging cubes (stream inward) ---
        int nConv = Mathf.CeilToInt(convergeCount * Mathf.Clamp01(0.25f + progress));
        float radius = gatherRadius * (1f - 0.35f * progress);
        for (int i = 0; i < nConv && i < convergeDirs.Length; i++)
        {
            float u = Mathf.Repeat(Time.time * convergeSpeed + i * 0.1379f, 1f); // 0 outer -> 1 center
            float dist = radius * (1f - u);
            Vector3 pos = SnapToGrid(center + convergeDirs[i] * dist, gs);
            float b = ramp * (0.35f + 0.65f * u);   // brighter as it nears the core
            AddCube(pos, gs * cubeFill, coreColor, b);
        }

        // --- contained crackle sparks (re-seeded on interval for a stable flicker) ---
        sparkTimer += Time.deltaTime;
        if (sparkTimer >= sparkInterval) { sparkTimer = 0f; ReseedSparks(); }
        int nSpark = Mathf.CeilToInt(sparkCount * Mathf.Clamp01(progress * 1.2f));
        for (int i = 0; i < nSpark && i < sparkOffsets.Length; i++)
        {
            Vector3 pos = SnapToGrid(center + sparkOffsets[i] * (0.5f + 0.5f * progress), gs);
            AddCube(pos, gs * cubeFill, sparkColor, ramp * 0.9f);
        }

        // disable any leftover pooled cubes
        for (int i = used; i < pool.Count; i++)
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
    }

    Vector3 SnapToGrid(Vector3 p, float gs)
        => new Vector3(Mathf.Round(p.x / gs) * gs, Mathf.Round(p.y / gs) * gs, Mathf.Round(p.z / gs) * gs);

    void AddCube(Vector3 pos, float scale, Color hdr, float brightness)
    {
        EnsurePool(used + 1);
        var t = pool[used];
        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        t.position = pos;
        t.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        var col = new Color(hdr.r * brightness, hdr.g * brightness, hdr.b * brightness, 1f);
        poolR[used].GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorID, col);
        poolR[used].SetPropertyBlock(mpb);
        used++;
    }

    // ---- resources ----
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
                cubeMat = new Material(sh) { name = "ChargeVoxel (auto)" };
                cubeMat.enableInstancing = true;
                if (cubeMat.HasProperty(BaseColorID)) cubeMat.SetColor(BaseColorID, coreColor);
            }
        }
    }

    void EnsurePool(int n)
    {
        while (pool.Count < n)
        {
            var go = new GameObject("_Charge" + pool.Count);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
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
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        used = 0;
    }

    [ContextMenu("Test Charge")]
    void TestCharge() { if (Application.isPlaying) StartCharge(); }
}
