using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Demo driver for ChainLightningCaster: sets the target/extender layer masks by name,
/// then casts on Start, on a key, and/or on an auto-loop so you can watch the chain build
/// (source -> ~4 targets -> nearest extender -> ~4 more).
/// </summary>
public class ChainCastDemo : MonoBehaviour
{
    public ChainLightningCaster caster;
    [Tooltip("Where the chain starts. Defaults to this object.")]
    public Transform source;

    [Header("Layers (set on the caster at runtime)")]
    public string targetLayer = "LightningTarget";
    public string extenderLayer = "LightningExtender";

    [Header("Trigger")]
    public Key key = Key.Space;
    public bool autoLoop = true;
    public float interval = 2.5f;

    float t;

    void Start()
    {
        if (caster)
        {
            caster.targetMask = LayerMask.GetMask(targetLayer);
            caster.extenderMask = LayerMask.GetMask(extenderLayer);
        }
        Cast();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame) Cast();
        if (autoLoop)
        {
            t += Time.deltaTime;
            if (t >= interval) { t = 0f; Cast(); }
        }
    }

    [ContextMenu("Cast")]
    public void Cast()
    {
        if (caster == null) return;
        Vector3 s = source ? source.position : transform.position;
        caster.Cast(s);
    }
}
