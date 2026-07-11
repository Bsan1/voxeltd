using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the chain-lightning target path and drives a ChainLightning bolt.
///
/// Rules: from a source, jump to the nearest unvisited target within <see cref="acquireRange"/>,
/// then keep hopping to the nearest unvisited target within <see cref="maxJump"/> until
/// <see cref="chainPerSource"/> targets are hit. Then look for the nearest EXTENDER within
/// <see cref="extenderRange"/> of the last hit; if found, jump to it (it becomes the new source)
/// and chain again. Repeats up to <see cref="maxExtenderHops"/> hops. Produces one continuous
/// point list fed into a single ChainLightning.
///
/// Targets / extenders are found by physics layers, so enemies just need a collider on the layer.
/// </summary>
public class ChainLightningCaster : MonoBehaviour
{
    [Header("Bolt to drive")]
    public ChainLightning bolt;

    [Header("Layers")]
    public LayerMask targetMask;
    public LayerMask extenderMask;

    [Header("Chaining")]
    [Tooltip("Search radius for the FIRST target from a source (original source or an extender).")]
    public float acquireRange = 6f;
    [Tooltip("Max distance between consecutive chained targets.")]
    public float maxJump = 4f;
    [Tooltip("How many targets are hit per source before looking for an extender.")]
    public int chainPerSource = 4;

    [Header("Extenders")]
    [Tooltip("Search radius for an extender around the last hit target.")]
    public float extenderRange = 5f;
    [Tooltip("Max number of extender relays in one cast (prevents runaway).")]
    public int maxExtenderHops = 3;

    [Header("Safety / options")]
    public int maxTotalPoints = 40;
    [Tooltip("Use horizontal (XZ) distance only — good for top-down games.")]
    public bool flatDistance = false;
    public int overlapBufferSize = 64;

    /// <summary>Targets hit by the last cast, in chain order (use for damage).</summary>
    public readonly List<Transform> LastHitTargets = new List<Transform>();
    /// <summary>The full ordered world-space point path of the last cast (incl. source + extenders).</summary>
    public readonly List<Vector3> LastPath = new List<Vector3>();

    Collider[] buf;
    readonly HashSet<Transform> visited = new HashSet<Transform>();

    // ---- Public API ----

    /// <summary>Cast from this object's position.</summary>
    [ContextMenu("Cast From Here")]
    public void Cast() => Cast(transform.position);

    /// <summary>Cast from a transform (e.g., the caster's hand).</summary>
    public void Cast(Transform source) { if (source) Cast(source.position); }

    /// <summary>
    /// Build the chain from a world position, drive the bolt, and return the hit targets.
    /// </summary>
    public List<Transform> Cast(Vector3 source)
    {
        var path = BuildPath(source);
        if (bolt && path.Count >= 2)
        {
            bolt.SetPoints(path);
            bolt.Fire();
        }
        return LastHitTargets;
    }

    /// <summary>Compute the path without driving the bolt (e.g., for preview/AI).</summary>
    public List<Vector3> BuildPath(Vector3 source)
    {
        if (buf == null || buf.Length != Mathf.Max(8, overlapBufferSize))
            buf = new Collider[Mathf.Max(8, overlapBufferSize)];

        visited.Clear();
        LastHitTargets.Clear();
        LastPath.Clear();

        LastPath.Add(source);
        Vector3 current = source;
        int hops = 0;

        while (true)
        {
            // chain up to chainPerSource targets from the current source
            int chained = 0;
            while (chained < chainPerSource && LastPath.Count < maxTotalPoints)
            {
                float range = (chained == 0) ? acquireRange : maxJump;
                Transform next = FindNearest(current, range, targetMask);
                if (next == null) break;

                visited.Add(next);
                LastHitTargets.Add(next);
                LastPath.Add(next.position);
                current = next.position;
                chained++;
            }

            if (LastPath.Count >= maxTotalPoints) break;
            if (hops >= maxExtenderHops) break;

            // look for an extender near the last point
            Transform ext = FindNearest(current, extenderRange, extenderMask);
            if (ext == null) break;

            visited.Add(ext);
            LastPath.Add(ext.position); // the relay is part of the bolt
            current = ext.position;
            hops++;
        }

        return LastPath;
    }

    // ---- internals ----

    Transform FindNearest(Vector3 center, float range, LayerMask mask)
    {
        int count = Physics.OverlapSphereNonAlloc(center, range, buf, mask, QueryTriggerInteraction.Collide);
        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var col = buf[i];
            if (!col) continue;
            // dedupe by rigidbody root if present (multi-collider enemies)
            Transform t = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            if (visited.Contains(t)) continue;

            Vector3 d = t.position - center;
            if (flatDistance) d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr <= range * range && sqr < bestSqr) { bestSqr = sqr; best = t; }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, acquireRange);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, extenderRange);

        // preview last path
        if (LastPath != null && LastPath.Count >= 2)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < LastPath.Count - 1; i++)
                Gizmos.DrawLine(LastPath[i], LastPath[i + 1]);
        }
    }
}
