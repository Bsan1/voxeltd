using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class OpenClawCircularCubeSetup
{
    private const string CubeName = "CircularMovingCube";

    static OpenClawCircularCubeSetup()
    {
        // Intentionally do not auto-configure or save the active scene on domain reload.
        // OpenClaw implementer runs often compile scripts while scene saving is prohibited.
    }

    [MenuItem("OpenClaw/Configure Circular Moving Cube")]
    private static void ConfigureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject cube = GameObject.Find(CubeName);
        if (cube == null)
        {
            GameObject accidentalEmpty = GameObject.Find("New GameObject");
            if (accidentalEmpty != null && accidentalEmpty.GetComponents<Component>().Length == 1)
            {
                Object.DestroyImmediate(accidentalEmpty);
            }

            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = CubeName;
        }

        cube.transform.SetParent(null);
        cube.transform.position = Vector3.zero;
        cube.transform.rotation = Quaternion.identity;
        cube.transform.localScale = Vector3.one;

        if (cube.GetComponent<CircularMovementAroundOrigin>() == null)
        {
            cube.AddComponent<CircularMovementAroundOrigin>();
        }

        Debug.Log("OpenClaw setup complete: CircularMovingCube created at (0, 0, 0) with CircularMovementAroundOrigin. Save the scene manually if this change should persist.");
    }
}
