using UnityEngine;

/// <summary>
/// Feeds the cell size of a Unity <see cref="Grid"/> component into the
/// GridLines material so the rendered line spacing always matches the grid.
/// No manual "grid scale" field: change the Grid's Cell Size and the lines
/// rescale automatically (in edit mode too, thanks to [ExecuteAlways]).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class GridLineDriver : MonoBehaviour
{
    [Tooltip("Unity Grid component that defines the cell size. " +
             "If left empty, the first Grid found in the scene is used.")]
    public Grid grid;

    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
    private static readonly int GridOriginId = Shader.PropertyToID("_GridOrigin");

    private void OnEnable()
    {
        Apply();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

    private void OnValidate() => Apply();
    private void Update() => Apply();

#if UNITY_EDITOR
    // Keeps the line spacing in sync while editing (the Grid's Cell Size can
    // change without notifying this component, and Update() does not tick
    // reliably outside Play mode).
    private void EditorTick()
    {
        if (!Application.isPlaying)
        {
            Apply();
        }
    }
#endif

    private void Apply()
    {
        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
        }
        if (grid == null)
        {
            return;
        }

        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }
        if (cachedRenderer == null)
        {
            return;
        }

        // Square cells: take the x extent of the Grid's cell size.
        float cell = Mathf.Max(0.0001f, grid.cellSize.x);
        Vector3 origin = grid.transform.position;

        propertyBlock ??= new MaterialPropertyBlock();
        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(CellSizeId, cell);
        propertyBlock.SetVector(GridOriginId, new Vector4(origin.x, origin.y, origin.z, 0f));
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
