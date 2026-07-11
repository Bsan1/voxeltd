using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BuildModeHudInstaller
{
    private const string ScenePath = "Assets/Scenes/gridTest.unity";
    private const string PrefabPath = "Assets/Prefabs/UI/BuildModeHud.prefab";

    [MenuItem("Tools/TestNetwork/UI/Rebuild Build Mode HUD Prefab")]
    public static void RebuildPrefabMenu()
    {
        RebuildPrefab();
    }

    [MenuItem("Tools/TestNetwork/UI/Install Build Mode HUD In gridTest")]
    public static void InstallInGridTestMenu()
    {
        RebuildPrefabAndInstallInGridTest();
    }

    public static void RebuildPrefabAndInstallInGridTest()
    {
        GameObject prefab = RebuildPrefab();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BuildPlacementController controller = Object.FindFirstObjectByType<BuildPlacementController>();

        BuildModeHud hud = Object.FindFirstObjectByType<BuildModeHud>();
        if (hud == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "BuildModeHud";
            hud = instance.GetComponent<BuildModeHud>();
        }

        hud.CreateDefaultLayout();
        RectTransform sceneRoot = hud.GetComponent<RectTransform>();
        NormalizeRootRect(sceneRoot);
        if (sceneRoot != null && PrefabUtility.IsPartOfPrefabInstance(sceneRoot))
        {
            PrefabUtility.ApplyObjectOverride(sceneRoot, PrefabPath, InteractionMode.AutomatedAction);
        }

        hud.SetAutoFindController(true, false);
        hud.SetPlacementController(controller, false);
        hud.RefreshNow();

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static GameObject RebuildPrefab()
    {
        EnsureDirectory(PrefabPath);

        GameObject root = new GameObject(
            "BuildModeHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(BuildModeHud));

        try
        {
            BuildModeHud hud = root.GetComponent<BuildModeHud>();
            hud.SetAutoFindController(false, false);
            hud.SetPlacementController(null, false);
            hud.CreateDefaultLayout();
            NormalizeRootRect(root.GetComponent<RectTransform>());
            hud.SetPlacementController(null, false);
            hud.SetAutoFindController(true, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.ImportAsset(PrefabPath);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsureDirectory(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }
    }

    private static void NormalizeRootRect(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        root.localScale = Vector3.one;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
    }
}
