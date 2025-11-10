#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ObstacleGenerator))]
public class ObstacleGeneratorEditor : Editor
{
    void OnSceneGUI()
    {
        var gen = (ObstacleGenerator)target;
        if (gen == null) return;

        Event e = Event.current;
        if (e == null) return;

        // Shift + Left Click -> paint obstacle
        if (e.shift && e.type == EventType.MouseDown && e.button == 0)
        {
            Ray worldRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            Undo.IncrementCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Paint Obstacle Cell");

            gen.PaintObstacleAtRay(worldRay);

            // Scene repaint and event used
            e.Use();
            SceneView.RepaintAll();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Random Obstacles"))
            {
                var gen = (ObstacleGenerator)target;
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Create Random Obstacles");
                gen.CreateObstacles();
                EditorUtility.SetDirty(gen);
            }
            if (GUILayout.Button("Clear Obstacles"))
            {
                var gen = (ObstacleGenerator)target;
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Clear Obstacles");
                gen.ClearObstacles();
                EditorUtility.SetDirty(gen);
            }
        }

        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save -> JSON File"))
            {
                ((ObstacleGenerator)target).SaveCurrentObstaclesToJson();
                EditorUtility.SetDirty(target);
            }
            if (GUILayout.Button("Save As Custom JSON File..."))
            {
                ((ObstacleGenerator)target).SaveCurrentObstaclesToCustomJson();
                EditorUtility.SetDirty(target);
            }
        }

        // if (GUILayout.Button("Apply Assigned Layout"))
        // {
        //     ((ObstacleGenerator)target).ApplyAssignedLayout();
        //     EditorUtility.SetDirty(target);
        // }

        if (GUILayout.Button("Load <- JSON File"))
        {
            ((ObstacleGenerator)target).LoadObstaclesFromJson();
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.HelpBox("Scene paint: Shift + Left Click to add an obstacle on the clicked HexCell.\nUse 'Clear Obstacles' to remove all.", MessageType.Info);
    }
}
#endif