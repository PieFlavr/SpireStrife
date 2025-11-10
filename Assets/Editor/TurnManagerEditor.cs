using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TurnManager))]
public class TurnManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tm = (TurnManager)target;
        if (tm == null)
        {
            EditorGUILayout.HelpBox("TurnManager reference lost.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live status.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Current Phase", tm.CurrentPhase.ToString());
        EditorGUILayout.LabelField("Turn Count", tm.turnCount.ToString());
        EditorGUILayout.LabelField("Player Input Enabled", tm.PlayerInputEnabled.ToString());

        // Live counts
        int playerUnits = SafeCountUnits(tm.playerTeamId);
        int aiUnits = SafeCountUnits(tm.aiTeamId);
        int playerSpires = SafeCountSpires(tm.playerTeamId);
        int aiSpires = SafeCountSpires(tm.aiTeamId);

        EditorGUILayout.LabelField("Player Units", playerUnits.ToString());
        EditorGUILayout.LabelField("AI Units", aiUnits.ToString());
        EditorGUILayout.LabelField("Player Spires", playerSpires.ToString());
        EditorGUILayout.LabelField("AI Spires", aiSpires.ToString());

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Game"))
        {
            if (tm.CurrentPhase == TurnManager.Phase.Init)
            {
                tm.StartGame();
            }
        }
        if (GUILayout.Button("End Player Turn"))
        {
            tm.EndPlayerTurn();
        }
        if (GUILayout.Button("Force Game Over"))
        {
            tm.EndGame();
        }
        EditorGUILayout.EndHorizontal();

        // Continuous repaint to keep values fresh
        if (Event.current.type == EventType.Repaint)
        {
            Repaint();
        }
    }

    private int SafeCountUnits(int teamId)
    {
        var all = GameObject.FindObjectsOfType<Units>();
        int total = 0;
        foreach (var u in all)
        {
            if (u != null && u.teamID == teamId && u.unitCount > 0 && u.state != Units.UnitState.Destroyed)
                total += u.unitCount;
        }
        return total;
    }

    private int SafeCountSpires(int teamId)
    {
        var all = GameObject.FindObjectsOfType<SpireConstruct>();
        int total = 0;
        foreach (var s in all)
        {
            if (s != null && s.teamID == teamId) total++;
        }
        return total;
    }
}
