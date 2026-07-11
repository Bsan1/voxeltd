using UnityEngine;

/// <summary>
/// Visual-only settings for the voxel chain lightning (no targeting/mechanics).
/// Create via: Assets > Create > VFX > Lightning Visual Settings.
/// Assign on a ChainLightning to drive its look; reuse one asset across many bolts.
/// </summary>
[CreateAssetMenu(menuName = "VFX/Lightning Visual Settings", fileName = "LightningVisualSettings")]
public class LightningVisualSettings : ScriptableObject
{
    [Header("Bolt path (midpoint displacement)")]
    [Range(1, 7)] public int generations = 4;
    public float initialOffset = 0.6f;
    public float jitterInterval = 0.05f;

    [Header("Voxels")]
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

    /// <summary>Copy these visual values onto a ChainLightning component.</summary>
    public void ApplyTo(ChainLightning cl)
    {
        if (cl == null) return;
        cl.generations = generations;
        cl.initialOffset = initialOffset;
        cl.jitterInterval = jitterInterval;
        cl.cubeSize = cubeSize;
        cl.cubeFill = cubeFill;
        cl.maxCubes = maxCubes;
        cl.branchChance = branchChance;
        cl.branchAngleDeg = branchAngleDeg;
        cl.maxBranches = maxBranches;
        cl.branchBrightness = branchBrightness;
        cl.coreColor = coreColor;
        cl.baseBrightness = baseBrightness;
        cl.pulseSpeed = pulseSpeed;
        cl.pulseWidth = pulseWidth;
        cl.loopPulse = loopPulse;
    }
}
