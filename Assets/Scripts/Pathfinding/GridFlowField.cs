using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding
{
    public sealed class GridFlowField : MonoBehaviour
    {
        // 8-directional so agents move diagonally (no cardinal "L" staircasing),
        // with octile costs (10 cardinal / 14 diagonal) for natural distances.
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
        };
        private const int CardinalCost = 10;
        private const int DiagonalCost = 14;

        [Header("References")]
        [SerializeField] private GridBuildSystem grid;

        [Header("Goal")]
        [SerializeField] private Vector2Int goalCell;
        [Tooltip("Goal is a (2r+1)x(2r+1) area of open cells around goalCell. 0 = single cell.")]
        [SerializeField, Min(0)] private int goalRadius = 3;
        [SerializeField] private bool rebuildOnStart = true;
        [SerializeField] private bool rebuildWhenOccupancyChanges = true;
        [SerializeField] private bool logRebuilds = true;

        private int[] integration;
        private Vector2Int[] flow;
        private readonly Queue<int> queue = new Queue<int>();
        private int cachedWidth;
        private int cachedHeight;
        private bool hasValidGoal;

        public Vector2Int GoalCell => goalCell;
        public int GoalRadius => goalRadius;
        public bool HasValidGoal => hasValidGoal;
        public int RebuildCount { get; private set; }
        public GridBuildSystem Grid => grid;
        public Vector2Int ActiveGoal => activeGoal;

        private bool dirty;
        private Vector2Int activeGoal;

        // BFS step-distance from this cell to the goal (the integration field).
        // Used for continuous gradient steering. False if out of bounds or
        // unreachable (occupied / walled off).
        public bool TryGetCost(Vector2Int cell, out float cost)
        {
            cost = 0f;
            if (!hasValidGoal || integration == null
                || cell.x < 0 || cell.x >= cachedWidth
                || cell.y < 0 || cell.y >= cachedHeight)
            {
                return false;
            }

            int v = integration[cell.y * cachedWidth + cell.x];
            if (v == int.MaxValue)
            {
                return false;
            }

            cost = v;
            return true;
        }

        private void Awake()
        {
            ResolveGrid();
            EnsureCapacity();
        }

        private void OnEnable()
        {
            ResolveGrid();
            if (grid != null)
            {
                grid.OccupancyChanged += HandleOccupancyChanged;
            }
        }

        private void Start()
        {
            if (rebuildOnStart)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            if (grid != null)
            {
                grid.OccupancyChanged -= HandleOccupancyChanged;
            }
        }

        public void SetGoal(Vector2Int goal)
        {
            goalCell = goal;
            Rebuild();
        }

        public void SetGoal(int x, int y)
        {
            SetGoal(new Vector2Int(x, y));
        }

        public void SetGoalCell(int x, int y)
        {
            SetGoal(new Vector2Int(x, y));
        }

        public void Rebuild()
        {
            ResolveGrid();
            EnsureCapacity();
            ClearFields();

            // A shared flow field keeps occupancy in GridBuildSystem as the
            // source of truth. It avoids a separate NavMesh/obstacle-carving
            // state and lets many agents sample one cached direction map.
            activeGoal = ResolveGoal();
            hasValidGoal = grid != null
                && grid.InBounds(activeGoal)
                && !grid.IsOccupied(activeGoal);

            if (!hasValidGoal)
            {
                return;
            }

            // Seed EVERY open cell in the goal region at cost 0 (a "bigger goal").
            // The BFS then drains the whole area; agents head for whichever goal
            // cell is cheapest, so a wide goal accepts the crowd without a pile-up
            // on one cell.
            queue.Clear();
            for (int dz = -goalRadius; dz <= goalRadius; dz++)
            {
                for (int dx = -goalRadius; dx <= goalRadius; dx++)
                {
                    Vector2Int c = new Vector2Int(activeGoal.x + dx, activeGoal.y + dz);
                    if (!CanTraverse(c))
                    {
                        continue;
                    }

                    int idx = ToIndex(c);
                    integration[idx] = 0;
                    flow[idx] = Vector2Int.zero;
                    queue.Enqueue(idx);
                }
            }

            // SPFA relaxation (non-uniform octile costs): a node may be re-queued
            // when a cheaper path is found; costs only decrease, so it terminates.
            while (queue.Count > 0)
            {
                int currentIndex = queue.Dequeue();
                Vector2Int current = FromIndex(currentIndex);
                int currentCost = integration[currentIndex];

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int dir = Directions[i];
                    Vector2Int neighbor = current + dir;
                    if (!CanTraverse(neighbor))
                    {
                        continue;
                    }

                    bool diagonal = dir.x != 0 && dir.y != 0;
                    if (diagonal && !CanDiagonal(current, dir))
                    {
                        continue;   // don't cut through a wall corner
                    }

                    int nextCost = currentCost + (diagonal ? DiagonalCost : CardinalCost);
                    int neighborIndex = ToIndex(neighbor);
                    if (integration[neighborIndex] <= nextCost)
                    {
                        continue;
                    }

                    integration[neighborIndex] = nextCost;
                    queue.Enqueue(neighborIndex);
                }
            }

            BuildFlowDirections();
            RebuildCount++;

            if (logRebuilds)
            {
                Debug.Log($"{nameof(GridFlowField)} rebuilt goal={goalCell} cells={integration.Length} reachable={GetReachableCellCount()} rebuilds={RebuildCount}", this);
            }
        }

        public Vector2Int NextCell(Vector2Int from)
        {
            if (!CanUseCell(from))
            {
                return from;
            }

            Vector2Int step = flow[ToIndex(from)];
            if (step == Vector2Int.zero)
            {
                return from;
            }

            Vector2Int next = from + step;
            return CanUseCell(next) ? next : from;
        }

        public Vector3 SampleDirection(Vector3 worldPosition)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            Vector2Int cell = grid.WorldToCell(worldPosition);
            Vector2Int next = NextCell(cell);
            if (next == cell)
            {
                return Vector3.zero;
            }

            Vector3 target = grid.CellToWorld(next);
            Vector3 direction = target - worldPosition;
            direction.y = 0f;
            float sqrMagnitude = direction.sqrMagnitude;
            return sqrMagnitude > 0.0001f ? direction / Mathf.Sqrt(sqrMagnitude) : Vector3.zero;
        }

        public bool HasPath(Vector2Int from)
        {
            return CanUseCell(from) && integration[ToIndex(from)] != int.MaxValue;
        }

        public bool HasPathFromCell(int x, int y)
        {
            return HasPath(new Vector2Int(x, y));
        }

        public string GetDebugSummary()
        {
            return $"{nameof(GridFlowField)} goal={goalCell} valid={hasValidGoal} size={cachedWidth}x{cachedHeight} rebuilds={RebuildCount} reachable={(integration != null ? GetReachableCellCount() : 0)}";
        }

        private void HandleOccupancyChanged()
        {
            // Coalesce: a multi-cell building fires OccupancyChanged once per cell.
            // Flag dirty and rebuild ONCE in LateUpdate instead of N times.
            if (rebuildWhenOccupancyChanges)
            {
                dirty = true;
            }
        }

        private void LateUpdate()
        {
            if (dirty)
            {
                dirty = false;
                Rebuild();
            }
        }

        // If the goal cell is out of bounds or occupied (e.g. a building was placed
        // on it), use the nearest free cell so agents don't all freeze.
        private Vector2Int ResolveGoal()
        {
            if (grid == null)
            {
                return goalCell;
            }

            if (grid.InBounds(goalCell) && !grid.IsOccupied(goalCell))
            {
                return goalCell;
            }

            int maxR = Mathf.Max(cachedWidth, cachedHeight);
            for (int r = 1; r <= maxR; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                        {
                            continue;   // ring perimeter only
                        }

                        Vector2Int c = new Vector2Int(goalCell.x + dx, goalCell.y + dz);
                        if (grid.InBounds(c) && !grid.IsOccupied(c))
                        {
                            return c;
                        }
                    }
                }
            }

            return goalCell;
        }

        private void ResolveGrid()
        {
            if (grid == null)
            {
                grid = FindFirstObjectByType<GridBuildSystem>();
            }
        }

        private void EnsureCapacity()
        {
            if (grid == null)
            {
                return;
            }

            int width = Mathf.Max(1, grid.gridWidth);
            int height = Mathf.Max(1, grid.gridHeight);
            int count = width * height;
            if (integration != null && cachedWidth == width && cachedHeight == height)
            {
                return;
            }

            cachedWidth = width;
            cachedHeight = height;
            integration = new int[count];
            flow = new Vector2Int[count];
        }

        private void ClearFields()
        {
            hasValidGoal = false;

            if (integration == null || flow == null)
            {
                return;
            }

            for (int i = 0; i < integration.Length; i++)
            {
                integration[i] = int.MaxValue;
                flow[i] = Vector2Int.zero;
            }
        }

        private void BuildFlowDirections()
        {
            for (int y = 0; y < cachedHeight; y++)
            {
                for (int x = 0; x < cachedWidth; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    int index = ToIndex(cell);
                    // cost 0 == a goal-region cell (nothing seeds 0 except the goal),
                    // unreachable == int.MaxValue: both have no onward step.
                    if (integration[index] == int.MaxValue || integration[index] == 0)
                    {
                        flow[index] = Vector2Int.zero;
                        continue;
                    }

                    int bestScore = int.MaxValue;
                    Vector2Int bestDirection = Vector2Int.zero;

                    for (int i = 0; i < Directions.Length; i++)
                    {
                        Vector2Int dir = Directions[i];
                        Vector2Int neighbor = cell + dir;
                        if (!CanUseCell(neighbor) || integration[ToIndex(neighbor)] == int.MaxValue)
                        {
                            continue;
                        }

                        bool diagonal = dir.x != 0 && dir.y != 0;
                        if (diagonal && !CanDiagonal(cell, dir))
                        {
                            continue;
                        }

                        // Step toward the neighbour that minimises total cost to goal.
                        int score = integration[ToIndex(neighbor)] + (diagonal ? DiagonalCost : CardinalCost);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestDirection = dir;
                        }
                    }

                    flow[index] = bestDirection;
                }
            }
        }

        private bool CanTraverse(Vector2Int cell)
        {
            return grid != null
                && grid.InBounds(cell)
                && !grid.IsOccupied(cell);
        }

        // A diagonal step is only allowed if both shared cardinal cells are open,
        // so agents never clip through a wall corner.
        private bool CanDiagonal(Vector2Int from, Vector2Int dir)
        {
            return CanTraverse(from + new Vector2Int(dir.x, 0))
                && CanTraverse(from + new Vector2Int(0, dir.y));
        }

        private bool CanUseCell(Vector2Int cell)
        {
            return hasValidGoal
                && cell.x >= 0
                && cell.x < cachedWidth
                && cell.y >= 0
                && cell.y < cachedHeight
                && integration != null;
        }

        private int ToIndex(Vector2Int cell)
        {
            return cell.y * cachedWidth + cell.x;
        }

        private Vector2Int FromIndex(int index)
        {
            return new Vector2Int(index % cachedWidth, index / cachedWidth);
        }

        private int GetReachableCellCount()
        {
            int count = 0;
            for (int i = 0; i < integration.Length; i++)
            {
                if (integration[i] != int.MaxValue)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
