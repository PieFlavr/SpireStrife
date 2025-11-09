using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple overlay component that shows whose turn it is and the current phase.
/// Attach to a Canvas object; assign either a legacy Text or a TMP_Text field.
/// </summary>
public class TurnIndicatorUI : MonoBehaviour
{
    [Header("Optional UI References")]
    public Text uiText;              // For UnityEngine.UI
    public TMP_Text tmpText;         // For TextMeshPro

    [Header("Formatting")]
    [Tooltip("Prefix shown before the numeric turn count.")]
    public string turnLabel = "Turn";
    [Tooltip("Format string for display. {0}=TurnLabel, {1}=TurnCount, {2}=Side, {3}=Phase")]
    public string format = "{0} {1}: {2} - {3}";

    private TurnManager.Phase lastPhase = TurnManager.Phase.Init;
    private int lastTurn = -1;
    private string lastDisplay = string.Empty;

    void Update()
    {
        var tm = TurnManager.inst;
        if (tm == null)
        {
            SetText("No TurnManager");
            return;
        }

        // Only recompute when something changed
        if (tm.CurrentPhase != lastPhase || tm.turnCount != lastTurn)
        {
            lastPhase = tm.CurrentPhase;
            lastTurn = tm.turnCount;
            lastDisplay = BuildDisplay(tm);
            SetText(lastDisplay);
        }
    }

    private string BuildDisplay(TurnManager tm)
    {
        if (tm.CurrentPhase == TurnManager.Phase.GameOver)
        {
            return $"{turnLabel} {tm.turnCount}: Game Over";
        }

        string side = SideName(tm.CurrentPhase, tm);
        string phase = PhaseName(tm.CurrentPhase);
        return string.Format(format, turnLabel, tm.turnCount, side, phase);
    }

    private string SideName(TurnManager.Phase phase, TurnManager tm)
    {
        switch (phase)
        {
            case TurnManager.Phase.PlayerPlanning:
            case TurnManager.Phase.PlayerResolving:
                return "Player";
            case TurnManager.Phase.AiPlanning:
            case TurnManager.Phase.AiResolving:
                return "AI";
            case TurnManager.Phase.Init:
                return "Init";
            default:
                return "Unknown";
        }
    }

    private string PhaseName(TurnManager.Phase phase)
    {
        switch (phase)
        {
            case TurnManager.Phase.PlayerPlanning: return "Planning";
            case TurnManager.Phase.PlayerResolving: return "Resolving";
            case TurnManager.Phase.AiPlanning: return "Planning";
            case TurnManager.Phase.AiResolving: return "Resolving";
            case TurnManager.Phase.Init: return "Waiting";
            case TurnManager.Phase.GameOver: return "Finished";
            default: return phase.ToString();
        }
    }

    private void SetText(string value)
    {
        if (uiText != null) uiText.text = value;
        if (tmpText != null) tmpText.text = value;
    }
}
