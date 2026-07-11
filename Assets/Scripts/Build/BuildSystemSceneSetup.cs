#if UNITY_EDITOR
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildSystemSceneSetup
{
    private const string LevelDataPath = "Assets/Data/Level_Mock.asset";
    private const string Building1x1Path = "Assets/Data/Building_1x1.asset";
    private const string Building2x2Path = "Assets/Data/Building_2x2.asset";
    private const string Building1x3Path = "Assets/Data/Building_1x3.asset";
    private const string DefaultCellPrefabPath = "Assets/Prefabs/Block_1x1.prefab";
    private const string FloorMaterialPath = "Assets/Materials/FloorGrid.mat";
    private const string FloorBaseMaterialPath = "Assets/Materials/GridFloorWhite.mat";
    private const string GhostMaterialPath = "Assets/Materials/GhostHologram.mat";
    private const string AgentPrefabPath = "Assets/Prefabs/Agent_Swarm.prefab";
    private const string FallbackAgentPrefabPath = "Assets/Prefabs/Enemy_PathAgent.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string MazeRunnerName = "MazeRunner";
    private const string PathAgentsName = "PathAgents";
    private const string PlayerName = "Player";
    private const string PlayerParentName = "Playerparent";
    private const string FloorGridLinesName = "FloorGridLines";

    [MenuItem("Tools/Build System/Setup Grid Test Scene")]
    public static void SetupGridTestSceneFromMenu()
    {
        Debug.Log(SetupGridTestScene());
    }

    [MenuItem("Tools/Build System/Setup Grid + Pathfinding Test Scene")]
    public static void SetupGridAndPathfindingTestSceneFromMenu()
    {
        Debug.Log(SetupGridAndPathfindingTestScene());
    }

    [MenuItem("Tools/Build System/Setup Grid + Pathfinding + Player Test Scene")]
    public static void SetupGridPathfindingAndPlayerTestSceneFromMenu()
    {
        Debug.Log(SetupGridPathfindingAndPlayerTestScene());
    }

    [MenuItem("Tools/Pathfinding/Setup Agent Test Scene/Left Top Right")]
    public static void SetupPathfindingAgentTestSceneFromMenu()
    {
        Debug.Log(SetupPathfindingAgentTestScene());
    }

    [MenuItem("Tools/Player/Setup Ball Player + Follow Camera")]
    public static void SetupBallPlayerWithFollowCameraFromMenu()
    {
        Debug.Log(SetupBallPlayerWithFollowCamera());
    }

    [MenuItem("Tools/Pathfinding/Setup Agent Test Scene/All Sides")]
    public static void SetupPathfindingAgentTestSceneAllSidesFromMenu()
    {
        Debug.Log(SetupPathfindingAgentTestSceneAllSides());
    }

    [MenuItem("Tools/Pathfinding/Setup Agent Test Scene/Left Only")]
    public static void SetupPathfindingAgentTestSceneLeftOnlyFromMenu()
    {
        Debug.Log(SetupPathfindingAgentTestSceneLeftOnly());
    }

    [MenuItem("Tools/Pathfinding/Setup Agent Test Scene/Top Only")]
    public static void SetupPathfindingAgentTestSceneTopOnlyFromMenu()
    {
        Debug.Log(SetupPathfindingAgentTestSceneTopOnly());
    }

    [MenuItem("Tools/Pathfinding/Setup Agent Test Scene/Right Only")]
    public static void SetupPathfindingAgentTestSceneRightOnlyFromMenu()
    {
        Debug.Log(SetupPathfindingAgentTestSceneRightOnly());
    }

    [MenuItem("Tools/Pathfinding/Setup Agent Test Scene/Bottom Only")]
    public static void SetupPathfindingAgentTestSceneBottomOnlyFromMenu()
    {
        Debug.Log(SetupPathfindingAgentTestSceneBottomOnly());
    }

    public static string SetupGridTestScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return "No active scene.";
        }

        int missingRemoved = RemoveMissingScripts(scene);
        int destroyed = DestroyOldSceneObjects(scene);
        int pathfindingComponentsRemoved = RemovePathfindingComponents(scene);

        GridBuildSystem grid = FindOrCreateGrid();

        Camera camera = SetupCamera(scene);
        BuildingData building1x1 = ConfigureBuildingData(Building1x1Path, DragPlacement.Single, BuildSurface.FloorOrTop);
        BuildingData building2x2 = ConfigureBuildingData(Building2x2Path, DragPlacement.Single, BuildSurface.FloorOrTop);
        BuildingData building1x3 = ConfigureBuildingData(Building1x3Path, DragPlacement.Line, BuildSurface.FloorOrTop);

        SetupGrid(grid);
        grid.ClearAll();
        SaveLevelData(grid.levelData);
        string floorName = SetupFloor(grid);

        BuildPlacementController controller = FindOrCreateBuildController(recreate: true);
        SetupBuildController(controller, grid, camera, building1x1, building2x2, building1x3);

        EditorSceneManager.MarkSceneDirty(scene);

        return $"Build test scene setup complete scene={scene.name} destroyed={destroyed} pathComponentsRemoved={pathfindingComponentsRemoved} missingScriptsRemoved={missingRemoved} floor={floorName} levelCells={grid.LevelDataCount} gridOccupied={grid.DictionaryCount} camera={(camera != null ? camera.name : "<none>")} sceneSaved=False";
    }

    public static string SetupGridAndPathfindingTestScene()
    {
        string gridResult = SetupGridTestScene();
        string pathResult = SetupPathfindingAgentTestScene();
        return $"{gridResult}\n{pathResult}";
    }

    public static string SetupGridPathfindingAndPlayerTestScene()
    {
        string setupResult = SetupGridAndPathfindingTestScene();
        string playerResult = SetupBallPlayerWithFollowCamera();
        return $"{setupResult}\n{playerResult}";
    }

    public static string SetupPathfindingAgentTestScene()
    {
        return SetupPathfindingAgentTestScene(new DirectionSelection(true, true, true, false));
    }

    public static string SetupPathfindingAgentTestSceneAllSides()
    {
        return SetupPathfindingAgentTestScene(new DirectionSelection(true, true, true, true));
    }

    public static string SetupPathfindingAgentTestSceneLeftOnly()
    {
        return SetupPathfindingAgentTestScene(new DirectionSelection(true, false, false, false));
    }

    public static string SetupPathfindingAgentTestSceneTopOnly()
    {
        return SetupPathfindingAgentTestScene(new DirectionSelection(false, true, false, false));
    }

    public static string SetupPathfindingAgentTestSceneRightOnly()
    {
        return SetupPathfindingAgentTestScene(new DirectionSelection(false, false, true, false));
    }

    public static string SetupPathfindingAgentTestSceneBottomOnly()
    {
        return SetupPathfindingAgentTestScene(new DirectionSelection(false, false, false, true));
    }

    public static string SetupBallPlayerWithFollowCamera()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return "No active scene.";
        }

        int missingRemoved = RemoveMissingScripts(scene);
        GridBuildSystem grid = FindOrCreateGrid();
        SetupGrid(grid);
        string floorName = SetupFloor(grid);

        GameObject player = SetupPlayer(scene, grid);
        Camera camera = SetupPlayerCamera(scene, player != null ? player.transform : null);

        EditorSceneManager.MarkSceneDirty(scene);

        return $"Ball player setup complete scene={scene.name} player={(player != null ? player.name : "<none>")} camera={(camera != null ? camera.name : "<none>")} floor={floorName} missingScriptsRemoved={missingRemoved} sceneSaved=False";
    }

    private static string SetupPathfindingAgentTestScene(DirectionSelection directions)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return "No active scene.";
        }

        int missingRemoved = RemoveMissingScripts(scene);
        int destroyedPathObjects = DestroyPathfindingSceneObjects(scene);
        int pathfindingComponentsRemoved = RemovePathfindingComponents(scene);

        GridBuildSystem grid = FindOrCreateGrid();
        Camera camera = SetupCamera(scene);
        SetupGrid(grid);
        string floorName = SetupFloor(grid);

        BuildingData building1x1 = ConfigureBuildingData(Building1x1Path, DragPlacement.Single, BuildSurface.FloorOrTop);
        BuildingData building2x2 = ConfigureBuildingData(Building2x2Path, DragPlacement.Single, BuildSurface.FloorOrTop);
        BuildingData building1x3 = ConfigureBuildingData(Building1x3Path, DragPlacement.Line, BuildSurface.FloorOrTop);
        BuildPlacementController controller = FindOrCreateBuildController(recreate: false);
        SetupBuildController(controller, grid, camera, building1x1, building2x2, building1x3);

        GameObject runnerObject = new GameObject(MazeRunnerName);
        runnerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        GridFlowField flowField = runnerObject.AddComponent<GridFlowField>();
        AgentSpatialHash spatialHash = runnerObject.AddComponent<AgentSpatialHash>();
        PathAgentSwarmSpawner spawner = runnerObject.AddComponent<PathAgentSwarmSpawner>();
        MazeBuilder mazeBuilder = runnerObject.AddComponent<MazeBuilder>();

        Transform spawnParent = CreatePathAgentsParent(runnerObject.transform);
        SetupFlowField(flowField, grid, controller);
        SetupSpatialHash(spatialHash);
        SetupSpawner(spawner, grid, flowField, spatialHash, spawnParent, directions);
        SetupMazeBuilder(mazeBuilder, grid, controller, spawner, flowField, directions);

        EditorSceneManager.MarkSceneDirty(scene);

        return $"Pathfinding agent scene setup complete scene={scene.name} destroyedPathObjects={destroyedPathObjects} pathComponentsRemoved={pathfindingComponentsRemoved} missingScriptsRemoved={missingRemoved} floor={floorName} runner={MazeRunnerName} directions={directions} agentPrefab={(ResolveAgentPrefab() != null ? ResolveAgentPrefab().name : "<none>")} sceneSaved=False";
    }

    private static void SetupGrid(GridBuildSystem grid)
    {
        Transform gridRoot = EnsureDedicatedGridRoot(grid);

        SerializedObject so = new SerializedObject(grid);
        SetObject(so, "levelData", AssetDatabase.LoadAssetAtPath<GridLevelData>(LevelDataPath));
        SetObject(so, "defaultCellVisualPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCellPrefabPath));
        SetObject(so, "gridRoot", gridRoot);
        SetObject(so, "floorGridMaterial", ResolveFloorBaseMaterial());
        SetBool(so, "seedMockDataIfEmpty", false);
        SetBool(so, "enableMousePlacement", false);
        SetBool(so, "allowRightClickRemove", false);
        SetFloat(so, "cellSize", 1f);
        SetFloat(so, "floorYOffset", 0f);
        SetInt(so, "gridWidth", 100);
        SetInt(so, "gridHeight", 100);
        so.ApplyModifiedPropertiesWithoutUndo();

        grid.SetupGridVisual();
        EnsureDedicatedGridRoot(grid);
        EditorUtility.SetDirty(grid);
    }

    private static string SetupFloor(GridBuildSystem grid)
    {
        if (grid == null)
        {
            return "<none>";
        }

        grid.SetupGridVisual();
        Transform floor = grid.floorVisual;
        if (floor == null)
        {
            return "<missing>";
        }

        floor.name = "Floor";
        floor.gameObject.SetActive(true);
        floor.gameObject.layer = 0;

        if (grid.gridVisual != null)
        {
            GridLineDriver driver = floor.GetComponent<GridLineDriver>();
            if (driver == null)
            {
                driver = floor.gameObject.AddComponent<GridLineDriver>();
            }

            driver.grid = grid.gridVisual;
            EditorUtility.SetDirty(driver);
        }

        grid.floorGridMaterial = ResolveFloorBaseMaterial();

        if (grid.floorGridMaterial != null && floor.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = grid.floorGridMaterial;
            EditorUtility.SetDirty(renderer);
        }

        Collider collider = floor.GetComponent<Collider>();
        if (collider == null)
        {
            if (floor.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                MeshCollider meshCollider = floor.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                collider = meshCollider;
            }
            else
            {
                collider = floor.gameObject.AddComponent<BoxCollider>();
            }
        }

        collider.enabled = true;
        SetupFloorGridLines(grid, floor);
        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(floor.gameObject);
        EditorUtility.SetDirty(grid);
        return floor.name;
    }

    private static void SetupFloorGridLines(GridBuildSystem grid, Transform floor)
    {
        if (grid == null || floor == null)
        {
            return;
        }

        Transform lines = floor.Find(FloorGridLinesName);
        if (lines == null)
        {
            GameObject lineObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lineObject.name = FloorGridLinesName;
            lineObject.transform.SetParent(floor, false);
            lines = lineObject.transform;
        }

        lines.gameObject.SetActive(true);
        lines.localPosition = new Vector3(0f, 0.001f, 0f);
        lines.localRotation = Quaternion.identity;
        lines.localScale = Vector3.one;

        Collider lineCollider = lines.GetComponent<Collider>();
        if (lineCollider != null)
        {
            Object.DestroyImmediate(lineCollider);
        }

        GridLineDriver driver = lines.GetComponent<GridLineDriver>();
        if (driver == null)
        {
            driver = lines.gameObject.AddComponent<GridLineDriver>();
        }

        driver.grid = grid.gridVisual;

        Material lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        if (lineMaterial != null && lines.TryGetComponent(out Renderer lineRenderer))
        {
            lineRenderer.sharedMaterial = lineMaterial;
            EditorUtility.SetDirty(lineRenderer);
        }

        EditorUtility.SetDirty(driver);
        EditorUtility.SetDirty(lines.gameObject);
    }

    private static void SetupBuildController(
        BuildPlacementController controller,
        GridBuildSystem grid,
        Camera camera,
        BuildingData building1x1,
        BuildingData building2x2,
        BuildingData building1x3)
    {
        SerializedObject so = new SerializedObject(controller);
        SetObject(so, "gridBuildSystem", grid);
        SetObject(so, "activeCamera", camera);
        SetObject(so, "ghostPreviewMaterial", AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath));
        SetBool(so, "disableGridBuildSystemMousePlacement", true);
        SetBool(so, "buildModeActive", true);
        SetInt(so, "selectedIndex", 0);
        SetEnum(so, "selectedDirection", (int)BuildingFootprint.Dir.Down);

        SerializedProperty buildings = so.FindProperty("buildings");
        if (buildings != null)
        {
            buildings.arraySize = 3;
            buildings.GetArrayElementAtIndex(0).objectReferenceValue = building1x1;
            buildings.GetArrayElementAtIndex(1).objectReferenceValue = building2x2;
            buildings.GetArrayElementAtIndex(2).objectReferenceValue = building1x3;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void SetupFlowField(GridFlowField flowField, GridBuildSystem grid, BuildPlacementController controller)
    {
        SerializedObject so = new SerializedObject(flowField);
        SetObject(so, "grid", grid);
        SetObject(so, "buildController", controller);
        SetVector2Int(so, "goalCell", new Vector2Int(50, 50));
        SetInt(so, "goalRadius", 3);
        SetBool(so, "rebuildOnStart", true);
        SetBool(so, "rebuildWhenOccupancyChanges", true);
        SetBool(so, "logRebuilds", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flowField);
    }

    private static void SetupSpatialHash(AgentSpatialHash spatialHash)
    {
        SerializedObject so = new SerializedObject(spatialHash);
        SetFloat(so, "bucketSize", 1.5f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spatialHash);
    }

    private static void SetupSpawner(
        PathAgentSwarmSpawner spawner,
        GridBuildSystem grid,
        GridFlowField flowField,
        AgentSpatialHash spatialHash,
        Transform spawnParent,
        DirectionSelection directions)
    {
        SerializedObject so = new SerializedObject(spawner);
        SetObject(so, "grid", grid);
        SetObject(so, "flowField", flowField);
        SetObject(so, "agentPrefab", ResolveAgentPrefab());
        SetObject(so, "spawnParent", spawnParent);
        SetObject(so, "spatialHash", spatialHash);
        SetInt(so, "defaultSpawnCount", 1000);
        SetBool(so, "clearBeforeSpawn", true);
        SetString(so, "agentNamePrefix", "ValAgent");
        SetBool(so, "spawnOverTime", true);
        SetInt(so, "spawnBatchSize", 25);
        SetFloat(so, "spawnInterval", 0.1f);
        SetBool(so, "spawnLeft", directions.Left);
        SetBool(so, "spawnTop", directions.Top);
        SetBool(so, "spawnRight", directions.Right);
        SetBool(so, "spawnBottom", directions.Bottom);
        SetInt(so, "spawnBandThickness", 2);
        SetSizeClasses(so);
        SetFloat(so, "minSize", 0.1f);
        SetFloat(so, "maxSize", 1.4f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawner);
    }

    private static void SetupMazeBuilder(
        MazeBuilder mazeBuilder,
        GridBuildSystem grid,
        BuildPlacementController controller,
        PathAgentSwarmSpawner spawner,
        GridFlowField flowField,
        DirectionSelection directions)
    {
        SerializedObject so = new SerializedObject(mazeBuilder);
        SetObject(so, "grid", grid);
        SetObject(so, "buildController", controller);
        SetObject(so, "spawner", spawner);
        SetObject(so, "flowField", flowField);
        SetVector2Int(so, "regionMin", new Vector2Int(10, 10));
        SetVector2Int(so, "regionMax", new Vector2Int(90, 90));
        SetVector2Int(so, "goalCell", new Vector2Int(50, 50));
        SetInt(so, "seed", 12345);
        SetFloat(so, "braidFraction", 0.2f);
        SetInt(so, "highwayWidth", 2);
        SetBool(so, "carveLeftHighway", directions.Left);
        SetBool(so, "carveTopHighway", directions.Top);
        SetBool(so, "carveRightHighway", directions.Right);
        SetBool(so, "carveBottomHighway", directions.Bottom);
        SetInt(so, "agentCount", 1000);
        SetBool(so, "runOnStart", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mazeBuilder);
    }

    private static Camera SetupCamera(Scene scene)
    {
        Camera camera = FindOrCreateMainCamera(scene);

        camera.gameObject.name = "Main Camera";
        camera.tag = "MainCamera";
        camera.transform.SetParent(null, true);
        camera.transform.position = new Vector3(-22f, 24f, -22f);
        camera.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 18f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;

        PlayerFollowCamera followCamera = camera.GetComponent<PlayerFollowCamera>();
        if (followCamera != null)
        {
            Object.DestroyImmediate(followCamera);
        }

        StrategyCameraController strategyCamera = camera.GetComponent<StrategyCameraController>();
        if (strategyCamera == null)
        {
            strategyCamera = camera.gameObject.AddComponent<StrategyCameraController>();
        }

        strategyCamera.panSpeed = 12f;
        strategyCamera.enableEdgePan = true;
        strategyCamera.edgePanPixels = 24f;
        strategyCamera.rotateSpeed = 90f;
        strategyCamera.enableMiddleMouseRotate = true;
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(strategyCamera);
        return camera;
    }

    private static Camera SetupPlayerCamera(Scene scene, Transform target)
    {
        Camera camera = FindOrCreateMainCamera(scene);
        camera.gameObject.name = "Main Camera";
        camera.tag = "MainCamera";
        camera.transform.SetParent(null, true);
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;

        StrategyCameraController strategyCamera = camera.GetComponent<StrategyCameraController>();
        if (strategyCamera != null)
        {
            Object.DestroyImmediate(strategyCamera);
        }

        PlayerFollowCamera followCamera = camera.GetComponent<PlayerFollowCamera>();
        if (followCamera == null)
        {
            followCamera = camera.gameObject.AddComponent<PlayerFollowCamera>();
        }

        SerializedObject so = new SerializedObject(followCamera);
        SetObject(so, "target", target);
        SetFloat(so, "pitch", 35f);
        SetFloat(so, "yaw", 45f);
        SetFloat(so, "distance", 20f);
        SetFloat(so, "orthographicSize", 8f);
        SetVector3(so, "lookOffset", Vector3.zero);
        SetFloat(so, "smoothTime", 0.18f);
        so.ApplyModifiedPropertiesWithoutUndo();

        Quaternion rotation = Quaternion.Euler(35f, 45f, 0f);
        camera.transform.rotation = rotation;
        Vector3 aim = target != null ? target.position : Vector3.zero;
        camera.transform.position = aim - (rotation * Vector3.forward) * 20f;

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(followCamera);
        return camera;
    }

    private static Camera FindOrCreateMainCamera(Scene scene)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera != null && camera.gameObject.scene == scene)
        {
            return camera;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static GameObject SetupPlayer(Scene scene, GridBuildSystem grid)
    {
        GameObject oldParent = FindSceneObject(scene, PlayerParentName);
        if (oldParent != null)
        {
            Object.DestroyImmediate(oldParent);
        }

        GameObject existing = FindSceneObject(scene, PlayerName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject player = InstantiatePlayerPrefab();
        player.name = PlayerName;
        player.transform.SetParent(null, true);
        player.transform.rotation = Quaternion.identity;

        Vector2Int spawnCell = new Vector2Int(50, 50);
        Vector3 position = grid != null ? grid.CellToWorld(spawnCell) : Vector3.zero;
        position.y = GetPlayerRestingY(player, grid);
        player.transform.position = position;

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (player.GetComponent<PlayerBall>() == null)
        {
            player.AddComponent<PlayerBall>();
        }

        EditorUtility.SetDirty(player);
        return player;
    }

    private static GameObject InstantiatePlayerPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab != null)
        {
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallback.AddComponent<Rigidbody>();
        fallback.AddComponent<PlayerBall>();
        return fallback;
    }

    private static float GetPlayerRestingY(GameObject player, GridBuildSystem grid)
    {
        float floorY = grid != null ? grid.transform.position.y : 0f;
        SphereCollider sphere = player != null ? player.GetComponent<SphereCollider>() : null;
        if (sphere == null || player == null)
        {
            return floorY + 0.5f;
        }

        Vector3 scale = player.transform.lossyScale;
        float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float centerOffset = sphere.center.y * scale.y;
        return floorY - centerOffset + radius;
    }

    private static BuildingData ConfigureBuildingData(string path, DragPlacement dragPlacement, BuildSurface surface)
    {
        BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (data == null)
        {
            return null;
        }

        SerializedObject so = new SerializedObject(data);
        SetEnum(so, "dragPlacement", (int)dragPlacement);
        SetEnum(so, "buildSurface", (int)surface);
        SetBool(so, "supportsTopPlacement", true);

        Vector2Int[] occupiedCells = data.GetOccupiedCells();
        SerializedProperty blockCells = so.FindProperty("blockCells");
        if (blockCells != null)
        {
            blockCells.arraySize = occupiedCells.Length;
            for (int i = 0; i < occupiedCells.Length; i++)
            {
                SerializedProperty block = blockCells.GetArrayElementAtIndex(i);
                block.FindPropertyRelative("localCell").vector3IntValue =
                    new Vector3Int(occupiedCells[i].x, 0, occupiedCells[i].y);
                block.FindPropertyRelative("requiresSupport").boolValue = true;
                block.FindPropertyRelative("providesSupport").boolValue = true;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        return data;
    }

    private static int DestroyOldSceneObjects(Scene scene)
    {
        int count = 0;
        string[] exactNames =
        {
            "GridFloorWhite",
            MazeRunnerName,
            PlayerParentName,
            PlayerName,
            PathAgentsName,
        };

        for (int i = 0; i < exactNames.Length; i++)
        {
            GameObject target = FindSceneObject(scene, exactNames[i]);
            if (target != null)
            {
                Object.DestroyImmediate(target);
                count++;
            }
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null || go.scene != scene)
            {
                continue;
            }

            if (go.name.StartsWith("GridCell_")
                || go.name.StartsWith("GridBuilding_")
                || go.name.StartsWith("Ghost_")
                || go.name.StartsWith("GhostLine"))
            {
                Object.DestroyImmediate(go);
                count++;
            }
        }

        return count;
    }

    private static int DestroyPathfindingSceneObjects(Scene scene)
    {
        int count = 0;
        GameObject mazeRunner = FindSceneObject(scene, MazeRunnerName);
        if (mazeRunner != null)
        {
            Object.DestroyImmediate(mazeRunner);
            count++;
        }

        GameObject pathAgents = FindSceneObject(scene, PathAgentsName);
        if (pathAgents != null)
        {
            Object.DestroyImmediate(pathAgents);
            count++;
        }

        return count;
    }

    private static int RemovePathfindingComponents(Scene scene)
    {
        int count = 0;
        MonoBehaviour[] components = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = components.Length - 1; i >= 0; i--)
        {
            MonoBehaviour component = components[i];
            if (component == null || component.gameObject.scene != scene)
            {
                continue;
            }

            if (component is MazeBuilder
                || component is GridFlowField
                || component is PathAgentSwarmSpawner
                || component is AgentSpatialHash)
            {
                Object.DestroyImmediate(component);
                count++;
            }
        }

        return count;
    }

    private static GridBuildSystem FindOrCreateGrid()
    {
        GridBuildSystem grid = Object.FindFirstObjectByType<GridBuildSystem>();
        if (grid != null)
        {
            return grid;
        }

        GameObject gridObject = new GameObject("GridBuildSystem");
        return gridObject.AddComponent<GridBuildSystem>();
    }

    private static Transform EnsureDedicatedGridRoot(GridBuildSystem grid)
    {
        if (grid == null)
        {
            return null;
        }

        Transform root = grid.transform.Find("GridRoot");
        if (root == null)
        {
            GameObject rootObject = new GameObject("GridRoot");
            rootObject.transform.SetParent(grid.transform, false);
            root = rootObject.transform;
        }

        root.SetParent(grid.transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        grid.gridRoot = root;
        EditorUtility.SetDirty(root.gameObject);
        return root;
    }

    private static Material ResolveFloorBaseMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FloorBaseMaterialPath);
        return material != null ? material : AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
    }

    private static BuildPlacementController FindOrCreateBuildController(bool recreate)
    {
        BuildPlacementController existingController = Object.FindFirstObjectByType<BuildPlacementController>();
        if (existingController != null && recreate)
        {
            Object.DestroyImmediate(existingController.gameObject);
            existingController = null;
        }

        if (existingController != null)
        {
            return existingController;
        }

        GameObject controllerObject = new GameObject("BuildPlacementController");
        return controllerObject.AddComponent<BuildPlacementController>();
    }

    private static Transform CreatePathAgentsParent(Transform parent)
    {
        GameObject go = new GameObject(PathAgentsName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static PathAgent ResolveAgentPrefab()
    {
        PathAgent prefab = ResolveAgentPrefabAtPath(AgentPrefabPath);
        if (prefab != null)
        {
            return prefab;
        }

        return ResolveAgentPrefabAtPath(FallbackAgentPrefabPath);
    }

    private static PathAgent ResolveAgentPrefabAtPath(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null ? prefab.GetComponent<PathAgent>() : null;
    }

    private static void SetSizeClasses(SerializedObject so)
    {
        SerializedProperty sizeClasses = so.FindProperty("sizeClasses");
        if (sizeClasses == null)
        {
            return;
        }

        sizeClasses.arraySize = 4;
        SetSizeClass(sizeClasses, 0, 0.2f, 58f);
        SetSizeClass(sizeClasses, 1, 0.4f, 39f);
        SetSizeClass(sizeClasses, 2, 0.8f, 2f);
        SetSizeClass(sizeClasses, 3, 1.4f, 1f);
    }

    private static void SetSizeClass(SerializedProperty sizeClasses, int index, float sizeCells, float percent)
    {
        SerializedProperty entry = sizeClasses.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("sizeCells").floatValue = sizeCells;
        entry.FindPropertyRelative("percent").floatValue = percent;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go != null && go.scene == scene && go.name == name)
            {
                return go;
            }
        }

        return null;
    }

    private static int RemoveMissingScripts(Scene scene)
    {
        int removed = 0;
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go != null && go.scene == scene)
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }
        }

        return removed;
    }

    private static void SaveLevelData(GridLevelData levelData)
    {
        if (levelData == null)
        {
            return;
        }

        levelData.cells ??= new System.Collections.Generic.List<GridCellData>();
        levelData.buildings ??= new System.Collections.Generic.List<GridLevelData.BuildingRecord>();
        levelData.cells.Clear();
        levelData.buildings.Clear();
        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssetIfDirty(levelData);
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetVector2Int(SerializedObject so, string propertyName, Vector2Int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2IntValue = value;
        }
    }

    private static void SetVector3(SerializedObject so, string propertyName, Vector3 value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    private static void SetEnum(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    private readonly struct DirectionSelection
    {
        public readonly bool Left;
        public readonly bool Top;
        public readonly bool Right;
        public readonly bool Bottom;

        public DirectionSelection(bool left, bool top, bool right, bool bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public override string ToString()
        {
            return $"left={Left},top={Top},right={Right},bottom={Bottom}";
        }
    }
}
#endif
