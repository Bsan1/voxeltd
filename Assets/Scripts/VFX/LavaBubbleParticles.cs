using UnityEngine;

/// <summary>
/// Configures the attached ParticleSystem into a blocky "lava bubble" effect:
/// small square/cube bubbles spawn across a flat area, swell, rise a little, then pop.
/// Drop this on an empty GameObject (a ParticleSystem is added automatically), place it
/// just above a lava surface, and tweak the fields below. Press the "Rebuild Bubbles"
/// context-menu item (gear icon on the component) after changing values in the editor.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
[ExecuteAlways]
public class LavaBubbleParticles : MonoBehaviour
{
    [Header("Spawn area (XZ plane, world units)")]
    public Vector2 areaSize = new Vector2(4f, 4f);
    [Min(0f)] public float emissionRate = 12f;

    [Header("Bubble life & motion")]
    [Min(0.05f)] public float lifetime = 1.6f;
    [Min(0f)] public float riseSpeed = 0.35f;
    [Min(0f)] public float minSize = 0.12f;
    [Min(0f)] public float maxSize = 0.3f;

    [Header("Colors (HDR for glow/bloom)")]
    [ColorUsage(true, true)] public Color bubbleHot = new Color(2.5f, 1.0f, 0.18f, 1f);
    [ColorUsage(true, true)] public Color bubbleCool = new Color(1.4f, 0.35f, 0.05f, 1f);

    [Header("Look")]
    [Tooltip("Optional cube/mesh for true voxel bubbles. Leave empty for flat square billboards.")]
    public Mesh bubbleMesh;
    [Tooltip("Additive = glowing; off = solid blocky bubbles.")]
    public bool additiveGlow = true;

    void Reset() => Configure();
    void Awake() => EnsureMaterial();

    [ContextMenu("Rebuild Bubbles")]
    public void Configure()
    {
        var ps = GetComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;                       // motion comes from velocityOverLifetime
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(bubbleCool, bubbleHot);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 2000;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        // Spawn flat across the surface (thin box on the XZ plane).
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(areaSize.x, 0.001f, areaSize.y);
        shape.rotation = Vector3.zero;
        shape.position = Vector3.zero;

        // Gentle upward drift so bubbles rise off the surface.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        // All three axes must share the same curve mode (TwoConstants here),
        // otherwise Unity throws "Particle Velocity curves must all be in the same mode".
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.5f, riseSpeed);

        // Swell up, hold, then pop (size -> 0 at the end).
        var sizeCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.15f),
            new Keyframe(0.35f, 1.0f),
            new Keyframe(0.75f, 1.0f),
            new Keyframe(1.0f, 0.0f));
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Fade in fast, stay lit, fade out on pop.
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(grad);

        EnsureMaterial();
    }

    void EnsureMaterial()
    {
        var r = GetComponent<ParticleSystemRenderer>();

        if (bubbleMesh != null)
        {
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = bubbleMesh;            // voxel cubes
        }
        else
        {
            r.renderMode = ParticleSystemRenderMode.Billboard; // flat square quads
        }

        if (r.sharedMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "LavaBubble (auto)" };
                // URP Particles/Unlit: make it transparent + additive so bubbles glow.
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additiveGlow ? 2f : 0f); // 2=Additive,0=Alpha
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                r.sharedMaterial = mat;
            }
        }
    }
}
