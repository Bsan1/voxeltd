# AI_CONTEXT.md

Project context for the TestNetwork prototype: a grid-building + crowd-pathfinding
sandbox. Test scene: `Assets/Scenes/gridTest.unity`.

## Systems

### Grid build (`Assets/Scripts/Grid/`, `Assets/Scripts/Build/`)
- `GridBuildSystem` — 100x100 cell grid (`cellSize=1`), occupancy is the single
  source of truth. `PlaceAt`/`RemoveAt`/`IsOccupied`/`InBounds`, world<->cell via
  `CellToWorld`/`WorldToCell`, `ClearAll`, and an `OccupancyChanged` event. Cells
  occupy y in [0,1] (placed objects centred at y=0.5), so the floor surface is y=0.
- `BuildPlacementController` — ghost placement, overlap fade, etc.

### Pathfinding swarm (`Assets/Scripts/Pathfinding/`)
A shared flow-field crowd that scales to ~1000 agents, driven on Play by `MazeBuilder`.
- `GridFlowField` — SPFA integration field from a goal AREA (`goalRadius`, every open
  cell in the box seeds cost 0), 8-direction octile flow with corner-cut prevention.
  Rebuilds (coalesced in `LateUpdate`) when occupancy changes; occupancy is the only
  obstacle source (no NavMesh).
- `MazeBuilder` — on Play: clears the grid, builds a recursive-division maze in a
  region, carves the open goal area, punches 2-wide "highway" corridors from each
  spawn side to the goal, then BRAIDS (removes a fraction of walls that join two
  passages) so there is more than one route. Then hands the region to the spawner.
- `PathAgentSwarmSpawner` — spawns agents along 3 maze edges (left/top/right band),
  released over time in batches (interval), with a %-weighted size table
  (`SizeClass[]`). Sizes are editable per-class on the component.
- `PathAgent` — kinematic Rigidbody agent: samples the cached flow dir per cell +
  Perlin noise + separation, then a centre-only wall constraint. Rests its base on
  the floor (centre = groundY + half-height) so every size touches the ground.
- `AgentSpatialHash` — buckets agents so separation is O(n); `GetNeighbors` is capped
  (`maxSeparationNeighbors`, default 20) so dense clumps don't spike to O(n^2).

### Player (`Assets/Scripts/Player/`, `Assets/Scripts/Camera/`)
- `PlayerBall` (prefab: `Assets/Prefabs/Player.prefab`, instance "Player" in gridTest)
  — physics sphere that ROLLS via `AddForce`. Camera-relative WASD, variable jump
  (tap = short hop), heavy mass to resist the crowd, and a fall multiplier so it
  drops faster after the apex without changing jump height. Jump uses VelocityChange
  so height is mass-independent.
- `PlayerFollowCamera` — orthographic, fixed-angle (default 35° pitch, 45° yaw)
  follow camera with SmoothDamp position smoothing. Auto-finds the PlayerBall.
  Attach to a camera (e.g. Main Camera) and disable `StrategyCameraController` on it
  (they both drive the transform).

## Conventions
- Component-based, `[SerializeField]` private fields, `[RequireComponent]` for hard
  deps, references cached in Awake. Built-in engine systems preferred. No
  `[RuntimeInitializeOnLoadMethod]` or other implicit auto-run unless explicitly asked.
- Unity 6 (6000.3.x): use `Rigidbody.linearVelocity` (not `velocity`).
