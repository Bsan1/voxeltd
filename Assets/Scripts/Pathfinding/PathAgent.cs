using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding
{
    // One swarm agent. Follows the shared 8-directional flow field toward the goal
    // (diagonal moves -> no cardinal "L"; the field never traps, so every agent
    // reaches the goal). Per-agent variety: a goal-ring offset (fanned approach) +
    // mild curving noise. Moves as a kinematic Rigidbody; its CENTER never enters an
    // occupied cell.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PathAgent : MonoBehaviour
    {
        private static GridFlowField sharedFlowField;
        private static AgentSpatialHash sharedHash;

        [Header("Path")]
        [SerializeField] private GridFlowField flowField;
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField] private bool rotateTowardMovement = true;
        [SerializeField, Min(1f)] private float rotationSpeed = 540f;
        [SerializeField, Min(0.1f)] private float arriveRadius = 1.5f;
        [Tooltip("World Y of the floor surface. Each agent rests its base here regardless of size.")]
        [SerializeField] private float groundY = 0f;

        [Header("Variety")]
        [SerializeField] private float noiseScale = 0.12f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.35f;
        [SerializeField] private float maxNoiseTurnDegrees = 35f;
        [Tooltip("Each agent aims at a random point within this radius of the goal (world units).")]
        [SerializeField] private float goalRingRadius = 1.5f;

        [Header("Separation")]
        [SerializeField] private float separationStrength = 1.5f;
        [Tooltip("Max neighbours an agent reacts to per step. Bounds the per-agent cost so dense clumps don't spike to O(n^2).")]
        [SerializeField, Min(1)] private int maxSeparationNeighbors = 20;

        private Rigidbody body;
        private Transform cachedTransform;
        private GridBuildSystem grid;
        private float sizeCells = 1f;
        private float radius = 0.5f;
        private float gridY;
        private bool hasGridY;
        private Vector3 goalRingOffset;
        private float noisePhase;
        private Vector2Int currentCell;
        private bool hasCell;
        private Vector3 cachedFlowDir;
        private int lastRebuildCount = -1;
        private readonly List<PathAgent> neighborScratch = new List<PathAgent>(32);

        public float Radius => radius;
        public Vector3 Position => cachedTransform != null ? cachedTransform.position : transform.position;

        public static void SetSharedFlowField(GridFlowField field) => sharedFlowField = field;
        public static void SetSharedHash(AgentSpatialHash hash) => sharedHash = hash;

        private void Awake()
        {
            cachedTransform = transform;
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (flowField == null)
            {
                flowField = sharedFlowField != null ? sharedFlowField : FindFirstObjectByType<GridFlowField>();
            }

            grid = flowField != null ? flowField.Grid : FindFirstObjectByType<GridBuildSystem>();

            // Per-agent variety seed (stable per instance).
            System.Random rng = new System.Random(GetInstanceID());
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r = goalRingRadius * (float)rng.NextDouble();
            goalRingOffset = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            noisePhase = (float)(rng.NextDouble() * 1000.0);
        }

        private void OnEnable()
        {
            if (sharedHash == null)
            {
                sharedHash = FindFirstObjectByType<AgentSpatialHash>();
            }

            sharedHash?.Register(this);
        }

        private void OnDisable()
        {
            sharedHash?.Unregister(this);
        }

        public void SetSize(float sizeInCells, float cellSize)
        {
            sizeCells = Mathf.Max(0.01f, sizeInCells);
            radius = 0.5f * sizeCells * cellSize;

            Transform t = cachedTransform != null ? cachedTransform : transform;
            t.localScale = Vector3.one * (sizeCells * cellSize);

            // Rest the body ON the floor: its centre sits half a (scaled) height
            // above the ground, so every size touches the floor instead of a small
            // one floating or a large one sinking. Bounds reflect the new scale.
            Renderer rend = GetComponentInChildren<Renderer>();
            float halfHeight = rend != null ? rend.bounds.extents.y : sizeCells * cellSize;
            Vector3 p = t.position;
            p.y = groundY + halfHeight;
            t.position = p;
            gridY = p.y;
            hasGridY = true;
        }

        public void SetFlowField(GridFlowField field)
        {
            flowField = field;
            if (field != null)
            {
                grid = field.Grid;
            }
        }

        private void FixedUpdate()
        {
            if (flowField == null || grid == null || body == null)
            {
                return;
            }

            Vector3 pos = cachedTransform.position;
            if (!hasGridY)
            {
                gridY = pos.y;
                hasGridY = true;
            }

            Vector3 goalWorld = grid.CellToWorld(flowField.ActiveGoal) + goalRingOffset;
            Vector3 toGoal = goalWorld - pos;
            toGoal.y = 0f;
            float goalDist = toGoal.magnitude;
            bool atGoal = goalDist <= arriveRadius;

            Vector3 move = Vector3.zero;
            if (!atGoal)
            {
                // Time-sliced field sample: re-read on cell change OR a field rebuild
                // (so an agent stopped by a new wall reroutes when the wall clears).
                int rc = flowField.RebuildCount;
                Vector2Int cell = grid.WorldToCell(pos);
                if (!hasCell || cell != currentCell || rc != lastRebuildCount)
                {
                    currentCell = cell;
                    hasCell = true;
                    lastRebuildCount = rc;
                    cachedFlowDir = flowField.SampleDirection(pos);
                }

                Vector3 dir = cachedFlowDir;
                if (dir.sqrMagnitude < 1e-6f)
                {
                    // No field step (at/near the goal cell) -> head to the offset goal.
                    dir = goalDist > 1e-4f ? toGoal / goalDist : Vector3.zero;
                }

                dir = ApplyNoise(dir, pos);
                move += dir * moveSpeed;
            }

            move += Separation(pos) * separationStrength;

            if (move.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector3 step = move * Time.fixedDeltaTime;
            Vector3 candidate = new Vector3(pos.x + step.x, gridY, pos.z + step.z);
            candidate = KeepOutOfBuildings(pos, candidate);

            body.MovePosition(candidate);

            if (rotateTowardMovement)
            {
                Vector3 face = candidate - pos;
                face.y = 0f;
                if (face.sqrMagnitude > 1e-6f)
                {
                    Quaternion target = Quaternion.LookRotation(face.normalized, Vector3.up);
                    cachedTransform.rotation = Quaternion.RotateTowards(
                        cachedTransform.rotation, target, rotationSpeed * Time.fixedDeltaTime);
                }
            }
        }

        // Mild per-agent curving on the final direction (variety, not enough to trap).
        private Vector3 ApplyNoise(Vector3 dir, Vector3 pos)
        {
            if (noiseStrength <= 0f || dir.sqrMagnitude < 1e-6f)
            {
                return dir.sqrMagnitude < 1e-6f ? dir : dir.normalized;
            }

            float n = Mathf.PerlinNoise(pos.x * noiseScale + noisePhase, pos.z * noiseScale + noisePhase);
            float angle = (n - 0.5f) * 2f * maxNoiseTurnDegrees * noiseStrength;
            return (Quaternion.Euler(0f, angle, 0f) * dir).normalized;
        }

        private Vector3 Separation(Vector3 pos)
        {
            if (sharedHash == null)
            {
                return Vector3.zero;
            }

            neighborScratch.Clear();
            sharedHash.GetNeighbors(pos, neighborScratch, maxSeparationNeighbors);

            Vector3 push = Vector3.zero;
            for (int i = 0; i < neighborScratch.Count; i++)
            {
                PathAgent other = neighborScratch[i];
                if (other == null || other == this)
                {
                    continue;
                }

                Vector3 d = pos - other.Position;
                d.y = 0f;
                float minDist = radius + other.Radius;
                float sqr = d.sqrMagnitude;
                // sqrMagnitude compare avoids a sqrt for every non-overlapping
                // neighbour; only overlapping pairs pay for the sqrt.
                if (sqr > 1e-8f && sqr < minDist * minDist)
                {
                    float dist = Mathf.Sqrt(sqr);
                    push += d / dist * (minDist - dist);
                }
                else if (sqr <= 1e-8f)
                {
                    push += new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
                }
            }

            return push;
        }

        // Center-only constraint: never let the agent's centre land in an occupied
        // cell. If blocked, slide along one axis (organic wall-follow).
        private Vector3 KeepOutOfBuildings(Vector3 from, Vector3 candidate)
        {
            if (grid == null || !IsPathBlocked(grid.WorldToCell(candidate)))
            {
                return candidate;
            }

            Vector3 xOnly = new Vector3(candidate.x, gridY, from.z);
            if (!IsPathBlocked(grid.WorldToCell(xOnly)))
            {
                return xOnly;
            }

            Vector3 zOnly = new Vector3(from.x, gridY, candidate.z);
            if (!IsPathBlocked(grid.WorldToCell(zOnly)))
            {
                return zOnly;
            }

            return new Vector3(from.x, gridY, from.z);
        }

        private bool IsPathBlocked(Vector2Int cell)
        {
            return flowField != null ? flowField.IsBlocked(cell) : grid != null && grid.IsOccupied(cell);
        }
    }
}
