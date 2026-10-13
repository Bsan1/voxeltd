# Voxel TD

> **Work in progress.** This is a Unity prototype for a voxel tower-defense game. The core systems are running, but the game around them is not built yet. There are no fighting towers, enemy health, waves, or win and loss conditions.

## What works so far

- **Grid building.** The build grid is 100 × 100, with occupancy kept as the single source of truth. Build mode has a ghost preview and supports placing and removing buildings. Buildings can cover several cells, and their data is stored in ScriptableObjects. A strategy camera moves around the map.
- **Swarm pathfinding.** Enemies follow a flow field calculated from the goal area. They can move in eight directions without cutting through wall corners, and the field is rebuilt whenever a wall or tower changes the available routes. A spatial hash keeps the crowd separated at low cost, allowing around 1,000 agents of different sizes to move at once. A recursive-division maze generator creates a test map, then adds extra openings so there is more than one route. The swarm is released from three sides in waves.
- **Player.** The player is a heavy physics ball controlled with camera-relative WASD. It has a variable-height jump and can push through the crowd. A fixed-angle follow camera tracks it.
- **Voxel effects.**
  - Chain lightning made from glowing cubes. It jumps between targets and continues through extender points.
  - A charge-up effect in which cubes gather into a pulsing core before release.
  - Puddles with blocky edges and an exact test for whether an enemy is standing inside the rendered shape.
  - Lava bubbles and an overlay showing which cells enemies can reach.

## Running it

Open the project in Unity 6000.3.11f1 using URP. The main test scene is `Assets/Scenes/gridTest.unity`. Press Play to build a maze and release the swarm. The other scenes contain tests for individual mechanics and effects.

## How it was made

Development follows an AI-assisted loop. A planner writes a short plan and checklist, an implementer uses Codex through OpenClaw to change the code, and the result is validated in the Unity Editor before it is accepted. `AGENTS.md` and `Docs/` describe the workflow.

## Author

Barış Şan — [sanbaris381@gmail.com](mailto:sanbaris381@gmail.com)