using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridSystem))]
public class GridSystemEditor : Editor
{
    private const string MarkerNamePrefix = "GridMarker";

    private SerializedProperty widthProperty;
    private SerializedProperty depthProperty;
    private SerializedProperty cellSizeProperty;
    private SerializedProperty levelHeightProperty;
    private SerializedProperty levelsProperty;

    private bool showDimensions = true;
    private bool showLevels = true;
    private bool showDebug = true;

    private int debugX;
    private int debugLevel;
    private int debugZ;
    private string debugLabel = "Test marker";

    private void OnEnable()
    {
        widthProperty = serializedObject.FindProperty(nameof(GridSystem.width));
        depthProperty = serializedObject.FindProperty(nameof(GridSystem.depth));
        cellSizeProperty = serializedObject.FindProperty(nameof(GridSystem.cellSize));
        levelHeightProperty = serializedObject.FindProperty(nameof(GridSystem.levelHeight));
        levelsProperty = serializedObject.FindProperty(nameof(GridSystem.levels));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GridSystem grid = (GridSystem)target;

        DrawDimensions(grid);
        EditorGUILayout.Space(4f);
        DrawLevels(grid);
        EditorGUILayout.Space(4f);
        DrawDebug(grid);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDimensions(GridSystem grid)
    {
        showDimensions = EditorGUILayout.Foldout(showDimensions, "Dimensions", true);
        if (!showDimensions)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(widthProperty);
        EditorGUILayout.PropertyField(depthProperty);
        EditorGUILayout.PropertyField(cellSizeProperty);
        EditorGUILayout.PropertyField(levelHeightProperty);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(grid, "Resize Grid System");
            grid.EnsureLevelsInitialized();
            EditorUtility.SetDirty(grid);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawLevels(GridSystem grid)
    {
        showLevels = EditorGUILayout.Foldout(showLevels, "Levels", true);
        if (!showLevels)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Level Count", grid.LevelCount.ToString());
        EditorGUILayout.PropertyField(levelsProperty, new GUIContent("Serialized Levels"), false);

        for (int i = 0; i < grid.LevelCount; i++)
        {
            EditorGUILayout.LabelField($"Level {i}", $"Y {i * grid.levelHeight:0.###}, occupied {grid.GetOccupiedCount(i)}");
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Level"))
        {
            AddLevel(grid);
        }

        using (new EditorGUI.DisabledScope(grid.LevelCount == 0))
        {
            if (GUILayout.Button("Remove Top Level"))
            {
                RemoveTopLevel(grid);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel--;
    }

    private void DrawDebug(GridSystem grid)
    {
        showDebug = EditorGUILayout.Foldout(showDebug, "Debug", true);
        if (!showDebug)
        {
            return;
        }

        EditorGUI.indentLevel++;
        debugX = EditorGUILayout.IntField("X", debugX);
        debugLevel = EditorGUILayout.IntField("Level", debugLevel);
        debugZ = EditorGUILayout.IntField("Z", debugZ);
        debugLabel = EditorGUILayout.TextField("Label", debugLabel);

        GridCoord coord = new GridCoord(debugX, debugLevel, debugZ);
        EditorGUILayout.LabelField("Cell World Center", grid.CellToWorld(coord).ToString("F3"));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Place Test Marker"))
        {
            PlaceTestMarker(grid, coord);
        }

        if (GUILayout.Button("Clear Cell"))
        {
            ClearCell(grid, coord);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear All"))
        {
            ClearAll(grid);
        }

        EditorGUILayout.Space(3f);
        for (int i = 0; i < grid.LevelCount; i++)
        {
            EditorGUILayout.LabelField($"Level {i} Occupied", grid.GetOccupiedCount(i).ToString());
        }

        EditorGUI.indentLevel--;
    }

    private void AddLevel(GridSystem grid)
    {
        Undo.RecordObject(grid, "Add Grid Level");
        grid.AddLevel();
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private void RemoveTopLevel(GridSystem grid)
    {
        List<GameObject> occupants = GetOccupantsOnLevel(grid, grid.LevelCount - 1);

        Undo.RecordObject(grid, "Remove Grid Level");
        for (int i = 0; i < occupants.Count; i++)
        {
            Undo.DestroyObjectImmediate(occupants[i]);
        }

        grid.RemoveTopLevel();
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private void PlaceTestMarker(GridSystem grid, GridCoord coord)
    {
        grid.EnsureLevelsInitialized();
        if (!grid.IsValid(coord))
        {
            Debug.LogWarning($"{nameof(GridSystem)} debug placement rejected invalid coord {coord}.", grid);
            return;
        }

        if (grid.TryGet(coord, out GridCellData existing) && existing != null && existing.IsOccupied)
        {
            Debug.LogWarning($"{nameof(GridSystem)} debug placement rejected occupied coord {coord}.", grid);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Grid Test Marker");
        Undo.RecordObject(grid, "Place Grid Test Marker");

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(marker, "Create Grid Test Marker");
        marker.name = $"{MarkerNamePrefix}_{coord.x}_{coord.level}_{coord.z}";
        marker.transform.SetParent(grid.transform, true);
        marker.transform.position = grid.CellToWorld(coord);
        marker.transform.rotation = grid.transform.rotation;
        marker.transform.localScale = Vector3.one * Mathf.Max(0.1f, grid.cellSize * 0.35f);

        GridCellData data = new GridCellData(string.IsNullOrWhiteSpace(debugLabel) ? "Test marker" : debugLabel, marker);
        if (!grid.TrySet(coord, data))
        {
            Undo.DestroyObjectImmediate(marker);
            Undo.CollapseUndoOperations(group);
            return;
        }

        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
        Undo.CollapseUndoOperations(group);
    }

    private void ClearCell(GridSystem grid, GridCoord coord)
    {
        grid.EnsureLevelsInitialized();
        if (!grid.IsValid(coord))
        {
            Debug.LogWarning($"{nameof(GridSystem)} debug clear rejected invalid coord {coord}.", grid);
            return;
        }

        Undo.RecordObject(grid, "Clear Grid Cell");
        if (grid.TryGet(coord, out GridCellData data) && data != null && data.Occupant != null)
        {
            Undo.DestroyObjectImmediate(data.Occupant);
        }

        grid.Clear(coord);
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private void ClearAll(GridSystem grid)
    {
        grid.EnsureLevelsInitialized();
        List<GameObject> occupants = GetAllOccupants(grid);

        Undo.RecordObject(grid, "Clear Grid");
        for (int i = 0; i < occupants.Count; i++)
        {
            Undo.DestroyObjectImmediate(occupants[i]);
        }

        grid.ClearAll();
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private List<GameObject> GetOccupantsOnLevel(GridSystem grid, int levelIndex)
    {
        List<GameObject> occupants = new List<GameObject>();
        foreach (GridCoord coord in grid.GetOccupiedCoords())
        {
            if (coord.level != levelIndex)
            {
                continue;
            }

            if (grid.TryGet(coord, out GridCellData data) && data?.Occupant != null)
            {
                occupants.Add(data.Occupant);
            }
        }

        return occupants;
    }

    private List<GameObject> GetAllOccupants(GridSystem grid)
    {
        List<GameObject> occupants = new List<GameObject>();
        foreach (GridCoord coord in grid.GetOccupiedCoords())
        {
            if (grid.TryGet(coord, out GridCellData data) && data?.Occupant != null)
            {
                occupants.Add(data.Occupant);
            }
        }

        return occupants;
    }
}
