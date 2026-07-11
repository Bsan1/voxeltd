using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Precise, collider-free "is a point inside this puddle" test. It re-implements the exact
/// shape math from PuddleDecal.shadergraph in C#, using the same parameter values pulled from
/// the Decal Projector's material — so the logical shape matches the rendered shape exactly,
/// blocky wobbly edge and all.
///
/// Put this on the same GameObject as the URP Decal Projector that renders the puddle.
/// Call Contains(worldPosition) / IsOn(transform) per enemy.
/// </summary>
[RequireComponent(typeof(DecalProjector))]
[ExecuteAlways]
public class PuddlePresence : MonoBehaviour
{
    [Header("If the detected shape is mirrored vs the visual, toggle these")]
    public bool flipU = false;
    public bool flipV = false;

    [Header("Also require the point to be within the projector's depth box")]
    public bool requireWithinDepth = false;

    [Header("Editor preview (green = counts as 'on puddle')")]
    public bool drawGizmo = true;
    [Range(8, 96)] public int gizmoResolution = 48;

    DecalProjector projector;

    // Cached shape params (kept in sync with the material).
    float radius = 0.42f, edgeNoise = 0.18f, noiseScale = 1f, seed = 0f, blockSize = 0.03f;

    void OnEnable() { projector = GetComponent<DecalProjector>(); CacheParams(); }
    void OnValidate() { projector = GetComponent<DecalProjector>(); CacheParams(); }

    /// <summary>Re-read shape values from the material. Call this if you change them at runtime.</summary>
    public void CacheParams()
    {
        if (projector == null) projector = GetComponent<DecalProjector>();
        var m = projector != null ? projector.material : null;
        if (m == null) return;
        if (m.HasProperty("_Radius"))     radius     = m.GetFloat("_Radius");
        if (m.HasProperty("_EdgeNoise"))  edgeNoise  = m.GetFloat("_EdgeNoise");
        if (m.HasProperty("_NoiseScale")) noiseScale = m.GetFloat("_NoiseScale");
        if (m.HasProperty("_Seed"))       seed       = m.GetFloat("_Seed");
        if (m.HasProperty("_BlockSize"))  blockSize  = m.GetFloat("_BlockSize");
    }

    /// <summary>True if the world position falls inside the puddle's actual (wobbly) shape.</summary>
    public bool Contains(Vector3 worldPos)
    {
        if (projector == null) return false;

        // World -> projector-local, relative to the projector pivot.
        Vector3 local = transform.InverseTransformPoint(worldPos) - projector.pivot;

        Vector2 size = new Vector2(projector.size.x, projector.size.y);
        if (size.x <= 0f || size.y <= 0f) return false;

        if (requireWithinDepth && Mathf.Abs(local.z) > projector.size.z * 0.5f)
            return false;

        // Decal UV: 0..1 across the projector face, centered at 0.5, then scale/bias like the shader sees.
        Vector2 uv = new Vector2(local.x / size.x + 0.5f, local.y / size.y + 0.5f);
        if (flipU) uv.x = 1f - uv.x;
        if (flipV) uv.y = 1f - uv.y;
        uv = uv * projector.uvScale + projector.uvBias;

        return InsidePuddleUV(uv);
    }

    /// <summary>Convenience overload for an enemy Transform.</summary>
    public bool IsOn(Transform t) => t != null && Contains(t.position);

    // Exact port of the shader's shape test (PuddleDecal Custom Function).
    bool InsidePuddleUV(Vector2 uv)
    {
        float bs = Mathf.Max(blockSize, 0.0001f);
        Vector2 puv = new Vector2(Mathf.Floor(uv.x / bs) * bs, Mathf.Floor(uv.y / bs) * bs);
        Vector2 d = new Vector2(puv.x - 0.5f, puv.y - 0.5f);
        float dist = d.magnitude;
        float ang = Mathf.Atan2(d.y, d.x);

        float ns = Mathf.Floor(noiseScale);
        float wob = 0f;
        wob += Mathf.Sin(ang * (2f + ns) + seed * 6.2831f) * 0.6f;
        wob += Mathf.Sin(ang * (4f + ns) - seed * 3.1f) * 0.3f;
        wob += Mathf.Sin(ang * (7f + ns) + seed * 1.7f) * 0.15f;

        float r = radius * (1f + wob * edgeNoise);
        return dist <= r; // matches HLSL step(dist, r)
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmo || projector == null) return;
        CacheParams();
        Vector2 size = new Vector2(projector.size.x, projector.size.y);
        if (size.x <= 0f || size.y <= 0f) return;

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 1f);
        float cell = Mathf.Min(size.x, size.y) / gizmoResolution * 0.8f;
        for (int yi = 0; yi < gizmoResolution; yi++)
        {
            for (int xi = 0; xi < gizmoResolution; xi++)
            {
                float fx = (xi + 0.5f) / gizmoResolution; // 0..1 across the face
                float fy = (yi + 0.5f) / gizmoResolution;
                Vector3 localPt = projector.pivot + new Vector3((fx - 0.5f) * size.x, (fy - 0.5f) * size.y, 0f);
                Vector3 worldPt = transform.TransformPoint(localPt);
                if (Contains(worldPt))
                    Gizmos.DrawCube(worldPt, Vector3.one * cell);
            }
        }
    }
}
