using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding
{
    // Pathfinding test driver. On Play: clears the grid, generates a
    // recursive-division maze in a region, carves an open goal AREA, punches a
    // 2-wide "highway" from each spawn side to the goal, then braids the maze
    // (removes some walls to create loops) so there is MORE THAN ONE route to the
    // goal. Finally spawns the agent swarm. Runs after GridBuildSystem (high
    // execution order) so its ClearAll wipes any leftover/loaded data first.
    [DefaultExecutionOrder(100)]
    public sealed class MazeBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridBuildSystem grid;
        [SerializeField] private PathAgentSwarmSpawner spawner;
        [SerializeField] private GridFlowField flowField;

        [Header("Maze region (grid cells)")]
        [SerializeField] private Vector2Int regionMin = new Vector2Int(10, 10);
        [SerializeField] private Vector2Int regionMax = new Vector2Int(90, 90);
        [SerializeField] private Vector2Int goalCell = new Vector2Int(50, 50);
        [SerializeField] private int seed = 12345;

        [Header("Maze shape")]
        [Tooltip("Fraction of removable interior walls knocked out to create LOOPS (extra routes to goal). 0 = perfect maze (single route).")]
        [SerializeField, Range(0f, 1f)] private float braidFraction = 0.2f;
        [Tooltip("Width (in cells) of the highway corridors carved from each spawn side to the goal. 2 = the requested 2-wide routes.")]
        [SerializeField, Min(1)] private int highwayWidth = 2;
        [SerializeField] private bool carveLeftHighway = true;
        [SerializeField] private bool carveTopHighway = true;
        [SerializeField] private bool carveRightHighway = true;
        [SerializeField] private bool carveBottomHighway = false;

        [Header("Agents")]
        [SerializeField] private int agentCount = 1000;
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunTest();
            }
        }

        // Public so it can also be re-run from the bridge / a button.
        public string RunTest()
        {
            ResolveRefs();
            if (grid == null)
            {
                return "MazeBuilder: no GridBuildSystem.";
            }

            System.Random rng = new System.Random(seed);

            grid.ClearAll();
            int walls = BuildMaze();
            CarveGoalArea();
            CarveHighways();
            int braided = Braid(rng, braidFraction);

            if (grid.IsOccupied(goalCell))
            {
                grid.RemoveAt(goalCell);
            }

            if (flowField != null)
            {
                flowField.SetGoal(goalCell);
            }

            string spawnResult = "no spawner";
            if (spawner != null)
            {
                spawner.SetSpawnRegion(regionMin, regionMax);
                spawnResult = spawner.SpawnAgentCount(agentCount);
            }

            return $"MazeBuilder: walls={walls} braided={braided} goal={goalCell} r={(flowField != null ? flowField.GoalRadius : 0)} | {spawnResult}";
        }

        private void ResolveRefs()
        {
            if (grid == null) grid = GetComponent<GridBuildSystem>();
            if (grid == null) grid = FindFirstObjectByType<GridBuildSystem>();
            if (spawner == null) spawner = GetComponent<PathAgentSwarmSpawner>();
            if (spawner == null) spawner = FindFirstObjectByType<PathAgentSwarmSpawner>();
            if (flowField == null) flowField = GetComponent<GridFlowField>();
            if (flowField == null) flowField = FindFirstObjectByType<GridFlowField>();
        }

        private int BuildMaze()
        {
            List<Vector2Int> walls = new List<Vector2Int>();
            System.Random rng = new System.Random(seed);
            Divide(regionMin.x, regionMin.y, regionMax.x, regionMax.y, rng, walls);

            int placed = 0;
            for (int i = 0; i < walls.Count; i++)
            {
                Vector2Int w = walls[i];
                if (w == goalCell || grid.IsOccupied(w))
                {
                    continue;
                }

                if (grid.PlaceAt(w.x, w.y))
                {
                    placed++;
                }
            }

            return placed;
        }

        // Recursive division: split the region with a wall line that has a single
        // gap, recurse on each half. Guarantees a fully-connected passage space.
        private void Divide(int x1, int y1, int x2, int y2, System.Random rng, List<Vector2Int> walls)
        {
            int w = x2 - x1 + 1;
            int h = y2 - y1 + 1;
            if (w < 5 || h < 5)
            {
                return;
            }

            bool horizontal = h > w || (h == w && rng.Next(2) == 0);
            if (horizontal)
            {
                int wy = y1 + 1 + rng.Next(h - 2);
                int gap = x1 + rng.Next(w);
                for (int x = x1; x <= x2; x++)
                {
                    if (x != gap) walls.Add(new Vector2Int(x, wy));
                }

                Divide(x1, y1, x2, wy - 1, rng, walls);
                Divide(x1, wy + 1, x2, y2, rng, walls);
            }
            else
            {
                int wx = x1 + 1 + rng.Next(w - 2);
                int gap = y1 + rng.Next(h);
                for (int y = y1; y <= y2; y++)
                {
                    if (y != gap) walls.Add(new Vector2Int(wx, y));
                }

                Divide(x1, y1, wx - 1, y2, rng, walls);
                Divide(wx + 1, y1, x2, y2, rng, walls);
            }
        }

        // Clear an open square of free cells around the goal so the goal is BIG.
        // Radius is owned by the flow field (single source of truth) so the seeded
        // goal area and the carved open area always match.
        private void CarveGoalArea()
        {
            int r = flowField != null ? flowField.GoalRadius : 0;
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    Vector2Int c = new Vector2Int(goalCell.x + dx, goalCell.y + dz);
                    if (grid.InBounds(c) && grid.IsOccupied(c))
                    {
                        grid.RemoveAt(c);
                    }
                }
            }
        }

        // Punch a wide, guaranteed-open corridor from the midpoint of each enabled
        // side to the goal (horizontal leg then vertical leg). These are the
        // 2-wide routes, and being independent of the maze they add extra routes.
        private void CarveHighways()
        {
            int midY = (regionMin.y + regionMax.y) / 2;
            int midX = (regionMin.x + regionMax.x) / 2;

            if (carveLeftHighway) CarveCorridor(new Vector2Int(regionMin.x, midY), goalCell);
            if (carveRightHighway) CarveCorridor(new Vector2Int(regionMax.x, midY), goalCell);
            if (carveTopHighway) CarveCorridor(new Vector2Int(midX, regionMax.y), goalCell);
            if (carveBottomHighway) CarveCorridor(new Vector2Int(midX, regionMin.y), goalCell);
        }

        private void CarveCorridor(Vector2Int from, Vector2Int to)
        {
            int x0 = Mathf.Min(from.x, to.x);
            int x1 = Mathf.Max(from.x, to.x);
            for (int x = x0; x <= x1; x++)
            {
                ClearBand(x, from.y, horizontal: true);
            }

            int y0 = Mathf.Min(from.y, to.y);
            int y1 = Mathf.Max(from.y, to.y);
            for (int y = y0; y <= y1; y++)
            {
                ClearBand(to.x, y, horizontal: false);
            }
        }

        // Clears `highwayWidth` cells perpendicular to the corridor's travel axis.
        private void ClearBand(int x, int y, bool horizontal)
        {
            for (int k = 0; k < highwayWidth; k++)
            {
                Vector2Int c = horizontal ? new Vector2Int(x, y + k) : new Vector2Int(x + k, y);
                if (grid.InBounds(c) && grid.IsOccupied(c))
                {
                    grid.RemoveAt(c);
                }
            }
        }

        // Braiding: remove a fraction of walls that have open passages on BOTH
        // opposite sides. Each removal joins two corridors into a loop, so the maze
        // gains alternative routes to the goal (no longer a single-path tree).
        private int Braid(System.Random rng, float fraction)
        {
            if (fraction <= 0f)
            {
                return 0;
            }

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int y = regionMin.y + 1; y < regionMax.y; y++)
            {
                for (int x = regionMin.x + 1; x < regionMax.x; x++)
                {
                    Vector2Int c = new Vector2Int(x, y);
                    if (!grid.IsOccupied(c))
                    {
                        continue;
                    }

                    bool horizontalGap = !grid.IsOccupied(new Vector2Int(x - 1, y)) && !grid.IsOccupied(new Vector2Int(x + 1, y));
                    bool verticalGap = !grid.IsOccupied(new Vector2Int(x, y - 1)) && !grid.IsOccupied(new Vector2Int(x, y + 1));
                    if (horizontalGap || verticalGap)
                    {
                        candidates.Add(c);
                    }
                }
            }

            // Fisher-Yates shuffle, then remove the first `count`.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int removeCount = Mathf.RoundToInt(candidates.Count * fraction);
            int removed = 0;
            for (int i = 0; i < removeCount && i < candidates.Count; i++)
            {
                if (grid.RemoveAt(candidates[i]))
                {
                    removed++;
                }
            }

            return removed;
        }
    }
}
