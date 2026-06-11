using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Pathfinding
{
    public sealed class PathAgentSwarmSpawner : MonoBehaviour
    {
        // One agent-size class and the share of the swarm that uses it. Percents
        // are relative weights (they need not sum to 100), so the mix is editable
        // straight from the Inspector.
        [System.Serializable]
        public struct SizeClass
        {
            [Min(0.05f)] public float sizeCells;
            [Range(0f, 100f)] public float percent;
        }

        [Header("References")]
        [SerializeField] private GridBuildSystem grid;
        [SerializeField] private GridFlowField flowField;
        [SerializeField] private PathAgent agentPrefab;
        [SerializeField] private Transform spawnParent;
        [SerializeField] private AgentSpatialHash spatialHash;

        [Header("Spawn")]
        [SerializeField, Min(1)] private int defaultSpawnCount = 1000;
        [SerializeField] private bool clearBeforeSpawn = true;
        [SerializeField] private string agentNamePrefix = "ValAgent";

        [Header("Spawn timing (interval release)")]
        [SerializeField] private bool spawnOverTime = true;
        [Tooltip("How many agents are released each interval.")]
        [SerializeField, Min(1)] private int spawnBatchSize = 25;
        [Tooltip("Seconds between batches.")]
        [SerializeField, Min(0f)] private float spawnInterval = 0.1f;

        [Header("Spawn sides (maze edges)")]
        [SerializeField] private bool spawnLeft = true;
        [SerializeField] private bool spawnTop = true;
        [SerializeField] private bool spawnRight = true;
        [SerializeField] private bool spawnBottom = false;
        [Tooltip("Thickness (in cells) of the spawn band along each enabled side.")]
        [SerializeField, Min(1)] private int spawnBandThickness = 2;

        [Header("Agent size distribution (% by size)")]
        [Tooltip("Each agent picks a size class by weighted random over these percents. Leave empty to use min/max range.")]
        [SerializeField]
        private SizeClass[] sizeClasses =
        {
            new SizeClass { sizeCells = 0.4f, percent = 50f },
            new SizeClass { sizeCells = 0.7f, percent = 30f },
            new SizeClass { sizeCells = 1.0f, percent = 20f },
        };
        [Header("Agent size fallback (cell units, used if no size classes)")]
        [SerializeField, Min(0.05f)] private float minSize = 0.2f;
        [SerializeField, Min(0.05f)] private float maxSize = 1.4f;

        private readonly List<PathAgent> spawnedAgents = new List<PathAgent>(1000);
        private Vector2Int spawnRegionMin;
        private Vector2Int spawnRegionMax;
        private bool hasSpawnRegion;
        private Coroutine spawnRoutine;

        public int SpawnedCount => spawnedAgents.Count;
        public float LastSpawnMilliseconds { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        // MazeBuilder hands us the maze rectangle so we spawn along its edges.
        public void SetSpawnRegion(Vector2Int min, Vector2Int max)
        {
            spawnRegionMin = min;
            spawnRegionMax = max;
            hasSpawnRegion = true;
        }

        public string SpawnDefaultAgents()
        {
            return SpawnAgentCount(defaultSpawnCount);
        }

        public string SpawnAgentCount(int count)
        {
            ResolveReferences();

            if (grid == null || flowField == null)
            {
                return $"{nameof(PathAgentSwarmSpawner)} missing grid or flow field.";
            }

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            if (clearBeforeSpawn)
            {
                ClearSpawnedAgents();
            }

            flowField.Rebuild();
            PathAgent.SetSharedFlowField(flowField);
            PathAgent.SetSharedHash(spatialHash);
            EnsureSpawnParent();

            List<Vector2Int> cells = BuildSpawnCells();
            if (cells.Count == 0)
            {
                return $"{nameof(PathAgentSwarmSpawner)} no reachable spawn cells. {flowField.GetDebugSummary()}";
            }

            int requested = Mathf.Max(0, count);

            if (spawnOverTime && Application.isPlaying)
            {
                spawnRoutine = StartCoroutine(SpawnOverTime(requested, cells));
                return $"{nameof(PathAgentSwarmSpawner)} releasing {requested} over time (batch={spawnBatchSize}/{spawnInterval}s) from {cells.Count} cells. {flowField.GetDebugSummary()}";
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int spawned = SpawnBatch(requested, cells, 0);
            stopwatch.Stop();
            LastSpawnMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;

            return $"{nameof(PathAgentSwarmSpawner)} spawned={spawned}/{requested} total={spawnedAgents.Count} ms={LastSpawnMilliseconds:0.###} {flowField.GetDebugSummary()}";
        }

        private IEnumerator SpawnOverTime(int count, List<Vector2Int> cells)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int cursor = 0;
            int spawned = 0;
            while (spawned < count)
            {
                int thisBatch = Mathf.Min(spawnBatchSize, count - spawned);
                cursor = SpawnBatchAt(thisBatch, cells, cursor, out int placed);
                spawned += placed;
                if (placed == 0)
                {
                    break;
                }

                yield return new WaitForSeconds(spawnInterval);
            }

            stopwatch.Stop();
            LastSpawnMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
            spawnRoutine = null;
        }

        // Spawn up to `count` agents starting at `cells[cursor]`, cycling the list.
        private int SpawnBatch(int count, List<Vector2Int> cells, int cursor)
        {
            SpawnBatchAt(count, cells, cursor, out int placed);
            return placed;
        }

        private int SpawnBatchAt(int count, List<Vector2Int> cells, int cursor, out int placed)
        {
            placed = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = cells[cursor % cells.Count];
                cursor++;

                PathAgent agent = CreateAgent(cell);
                if (agent == null)
                {
                    break;
                }

                spawnedAgents.Add(agent);
                placed++;
            }

            return cursor;
        }

        public string ClearSpawnedAgents()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            int cleared = 0;
            for (int i = spawnedAgents.Count - 1; i >= 0; i--)
            {
                PathAgent agent = spawnedAgents[i];
                if (agent != null)
                {
                    DestroyAgent(agent.gameObject);
                    cleared++;
                }
            }

            spawnedAgents.Clear();
            return $"{nameof(PathAgentSwarmSpawner)} cleared={cleared}";
        }

        public string GetDebugSummary()
        {
            return $"{nameof(PathAgentSwarmSpawner)} spawned={spawnedAgents.Count} lastMs={LastSpawnMilliseconds:0.###}";
        }

        private void ResolveReferences()
        {
            if (grid == null)
            {
                grid = GetComponent<GridBuildSystem>();
            }

            if (grid == null)
            {
                grid = FindFirstObjectByType<GridBuildSystem>();
            }

            if (flowField == null)
            {
                flowField = GetComponent<GridFlowField>();
            }

            if (flowField == null)
            {
                flowField = FindFirstObjectByType<GridFlowField>();
            }

            if (spatialHash == null)
            {
                spatialHash = GetComponent<AgentSpatialHash>();
            }

            if (spatialHash == null)
            {
                spatialHash = FindFirstObjectByType<AgentSpatialHash>();
            }

            if (spatialHash == null)
            {
                // Auto-create so separation works without manual scene setup.
                spatialHash = (grid != null ? grid.gameObject : gameObject)
                    .AddComponent<AgentSpatialHash>();
            }
        }

        private void EnsureSpawnParent()
        {
            if (spawnParent != null)
            {
                return;
            }

            Transform existing = transform.Find("PathAgents");
            if (existing != null)
            {
                spawnParent = existing;
                return;
            }

            GameObject parent = new GameObject("PathAgents");
            parent.transform.SetParent(transform, false);
            spawnParent = parent.transform;
        }

        // Candidate spawn cells: the inner band along each enabled maze side,
        // filtered to open cells that can actually reach the goal. Shuffled so a
        // batch spreads across all three sides instead of filling one corner.
        private List<Vector2Int> BuildSpawnCells()
        {
            HashSet<Vector2Int> set = new HashSet<Vector2Int>();

            if (hasSpawnRegion)
            {
                int t = Mathf.Max(1, spawnBandThickness);
                if (spawnLeft) AddBand(set, spawnRegionMin.x, spawnRegionMin.x + t - 1, spawnRegionMin.y, spawnRegionMax.y);
                if (spawnRight) AddBand(set, spawnRegionMax.x - t + 1, spawnRegionMax.x, spawnRegionMin.y, spawnRegionMax.y);
                if (spawnTop) AddBand(set, spawnRegionMin.x, spawnRegionMax.x, spawnRegionMax.y - t + 1, spawnRegionMax.y);
                if (spawnBottom) AddBand(set, spawnRegionMin.x, spawnRegionMax.x, spawnRegionMin.y, spawnRegionMin.y + t - 1);
            }
            else
            {
                // Fallback: whole grid (no region supplied).
                AddBand(set, 0, grid.gridWidth - 1, 0, grid.gridHeight - 1);
            }

            List<Vector2Int> cells = new List<Vector2Int>(set.Count);
            foreach (Vector2Int c in set)
            {
                if (!grid.IsOccupied(c) && flowField.HasPath(c))
                {
                    cells.Add(c);
                }
            }

            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }

            return cells;
        }

        private void AddBand(HashSet<Vector2Int> set, int xMin, int xMax, int yMin, int yMax)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2Int c = new Vector2Int(x, y);
                    if (grid.InBounds(c))
                    {
                        set.Add(c);
                    }
                }
            }
        }

        private PathAgent CreateAgent(Vector2Int cell)
        {
            Vector3 position = grid.CellToWorld(cell);

            PathAgent agent;
            if (agentPrefab != null)
            {
                agent = Instantiate(agentPrefab, position, Quaternion.identity, spawnParent);
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.transform.SetParent(spawnParent, false);
                fallback.transform.position = position;
                agent = fallback.AddComponent<PathAgent>();
            }

            agent.name = $"{agentNamePrefix}_{spawnedAgents.Count:0000}";
            agent.SetFlowField(flowField);
            float cellSize = grid != null ? grid.cellSize : 1f;
            agent.SetSize(PickSize(), cellSize);
            return agent;
        }

        // Weighted pick over the size classes (percent = relative weight). Falls
        // back to the min/max range if no classes are configured.
        private float PickSize()
        {
            if (sizeClasses == null || sizeClasses.Length == 0)
            {
                return Random.Range(minSize, maxSize);
            }

            float total = 0f;
            for (int i = 0; i < sizeClasses.Length; i++)
            {
                total += Mathf.Max(0f, sizeClasses[i].percent);
            }

            if (total <= 0f)
            {
                return Random.Range(minSize, maxSize);
            }

            float r = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < sizeClasses.Length; i++)
            {
                acc += Mathf.Max(0f, sizeClasses[i].percent);
                if (r <= acc)
                {
                    return Mathf.Max(0.05f, sizeClasses[i].sizeCells);
                }
            }

            return Mathf.Max(0.05f, sizeClasses[sizeClasses.Length - 1].sizeCells);
        }

        private static void DestroyAgent(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
