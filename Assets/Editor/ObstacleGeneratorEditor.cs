#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ObstacleGenerator))]
public class ObstacleGeneratorEditor : Editor
{
    void OnSceneGUI()
    {
        // Target may become null during undo/redo or recompilation
        var gen = target as ObstacleGenerator;
        if (gen == null) return;

        Event e = Event.current;
        if (e == null) return;

        // Shift + Left Click -> paint obstacle
        if (e.shift && e.type == EventType.MouseDown && e.button == 0)
        {
            Ray worldRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            Undo.IncrementCurrentGroup();
            if (gen != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Paint Obstacle Cell");
                gen.PaintObstacleAtRay(worldRay);
            }

            // Scene repaint and event used
            e.Use();
            SceneView.RepaintAll();
        }
    }

    public override void OnInspectorGUI()
    {
        // Ensure serialized representation is valid before drawing
        if (serializedObject == null || serializedObject.targetObject == null)
            return;

        serializedObject.UpdateIfRequiredOrScript();

        // Draw all properties except the script reference using the safer iterator
        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
        {
            DrawPropertiesExcluding(serializedObject, "m_Script");
        }

        // Apply any property modifications before running actions that mutate the scene
        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Create Random Obstacles"))
                {
                    var gen = target as ObstacleGenerator;
                    if (gen != null)
                    {
                        Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Create Random Obstacles");
                        gen.CreateObstacles();
                        EditorUtility.SetDirty(gen);
                    }
                }
                if (GUILayout.Button("Clear Obstacles"))
                {
                    var gen = target as ObstacleGenerator;
                    if (gen != null)
                    {
                        Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Clear Obstacles");
                        gen.ClearObstacles();
                        EditorUtility.SetDirty(gen);
                    }
                }
            }
        }

        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Save -> JSON File"))
                {
                    var gen = target as ObstacleGenerator;
                    if (gen != null)
                    {
                        gen.SaveCurrentObstaclesToJson();
                        EditorUtility.SetDirty(gen);
                    }
                }
                if (GUILayout.Button("Save As Custom JSON File..."))
                {
                    var gen = target as ObstacleGenerator;
                    if (gen != null)
                    {
                        gen.SaveCurrentObstaclesToCustomJson();
                        EditorUtility.SetDirty(gen);
                    }
                }
            }
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Load <- JSON File"))
            {
                var gen = target as ObstacleGenerator;
                if (gen != null)
                {
                    gen.LoadObstaclesFromJson();
                    EditorUtility.SetDirty(gen);
                }
            }
        }

        EditorGUILayout.HelpBox("Scene paint: Shift + Left Click to add an obstacle on the clicked HexCell.\nUse 'Clear Obstacles' to remove all.", MessageType.Info);
    }
}
#endif