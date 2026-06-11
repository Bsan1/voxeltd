using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding
{
    // Buckets all agents by position each physics step so separation can query
    // only nearby agents (the 3x3 buckets around a point) instead of all N — O(n)
    // total rather than O(n^2). Runs before PathAgent (negative execution order)
    // so the buckets are fresh when agents read them in FixedUpdate.
    [DefaultExecutionOrder(-50)]
    public sealed class AgentSpatialHash : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float bucketSize = 1.5f;

        private readonly List<PathAgent> agents = new List<PathAgent>(1024);
        private readonly HashSet<PathAgent> membership = new HashSet<PathAgent>();
        private readonly Dictionary<long, List<PathAgent>> buckets = new Dictionary<long, List<PathAgent>>();
        private readonly Stack<List<PathAgent>> listPool = new Stack<List<PathAgent>>();

        public void Register(PathAgent agent)
        {
            if (agent != null && membership.Add(agent))
            {
                agents.Add(agent);
            }
        }

        public void Unregister(PathAgent agent)
        {
            if (agent != null && membership.Remove(agent))
            {
                agents.Remove(agent);
            }
        }

        private void FixedUpdate()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (KeyValuePair<long, List<PathAgent>> kv in buckets)
            {
                kv.Value.Clear();
                listPool.Push(kv.Value);
            }

            buckets.Clear();

            for (int i = 0; i < agents.Count; i++)
            {
                PathAgent agent = agents[i];
                if (agent == null)
                {
                    continue;
                }

                long key = KeyOf(agent.Position);
                if (!buckets.TryGetValue(key, out List<PathAgent> list))
                {
                    list = listPool.Count > 0 ? listPool.Pop() : new List<PathAgent>(16);
                    buckets[key] = list;
                }

                list.Add(agent);
            }
        }

        // Fills `result` with agents in the 3x3 buckets around `position`. The
        // caller filters by exact distance. bucketSize must exceed the largest
        // agent interaction distance (sum of two radii) so 3x3 covers it.
        public void GetNeighbors(Vector3 position, List<PathAgent> result)
        {
            GetNeighbors(position, result, int.MaxValue);
        }

        // maxCount caps how many neighbours are gathered, so a dense bucket
        // (a clump of hundreds) costs O(maxCount) per query instead of O(bucket).
        public void GetNeighbors(Vector3 position, List<PathAgent> result, int maxCount)
        {
            if (result == null)
            {
                return;
            }

            int bx = Mathf.FloorToInt(position.x / bucketSize);
            int bz = Mathf.FloorToInt(position.z / bucketSize);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!buckets.TryGetValue(Key(bx + dx, bz + dz), out List<PathAgent> list))
                    {
                        continue;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (result.Count >= maxCount)
                        {
                            return;
                        }

                        result.Add(list[i]);
                    }
                }
            }
        }

        private long KeyOf(Vector3 position)
        {
            return Key(Mathf.FloorToInt(position.x / bucketSize),
                       Mathf.FloorToInt(position.z / bucketSize));
        }

        private static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
