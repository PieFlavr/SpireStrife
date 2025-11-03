using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

/// <summary>
/// Displays overall game statistics: spire counts, unit totals, turn counter.
/// Attach to a Canvas prefab with TextMeshPro components for each stat.
/// Updates automatically when spire ownership changes or turns advance.
/// </summary>
public class GameScoreboard : MonoBehaviour
{
    [Header("Scoreboard References")]
    [Tooltip("Text component for displaying current turn number")]
    public TextMeshProUGUI turnCounterText;

    [Header("Player Stats")]
    [Tooltip("Text for player-owned spire count")]
    public TextMeshProUGUI playerSpireCountText;
    [Tooltip("Text for player total unit count")]
    public TextMeshProUGUI playerUnitCountText;
    [Tooltip("Text for player garrison (stationed) units")]
    public TextMeshProUGUI playerGarrisonText;
    [Tooltip("Text for player reserve units")]
    public TextMeshProUGUI playerReserveText;

    [Header("AI Stats")]
    [Tooltip("Text for AI-owned spire count")]
    public TextMeshProUGUI aiSpireCountText;
    [Tooltip("Text for AI total unit count")]
    public TextMeshProUGUI aiUnitCountText;
    [Tooltip("Text for AI garrison (stationed) units")]
    public TextMeshProUGUI aiGarrisonText;
    [Tooltip("Text for AI reserve units")]
    public TextMeshProUGUI aiReserveText;

    [Header("Neutral Stats")]
    [Tooltip("Text for neutral spire count")]
    public TextMeshProUGUI neutralSpireCountText;

    [Header("Refresh Settings")]
    [Tooltip("Auto-refresh every N seconds (0 = manual only)")]
    [Min(0f)]
    public float autoRefreshInterval = 0.5f;

    private float timeSinceLastRefresh = 0f;
    private int cachedTurnCount = 0;

    private void OnEnable()
    {
        // Subscribe to spire ownership changes for immediate updates
        SpireConstruct.OwnershipChanged += OnSpireOwnershipChanged;
        RefreshScoreboard();
    }

    private void OnDisable()
    {
        SpireConstruct.OwnershipChanged -= OnSpireOwnershipChanged;
    }

    private void Update()
    {
        if (autoRefreshInterval > 0f)
        {
            timeSinceLastRefresh += Time.deltaTime;
            if (timeSinceLastRefresh >= autoRefreshInterval)
            {
                RefreshScoreboard();
                timeSinceLastRefresh = 0f;
            }
        }
    }

    private void OnSpireOwnershipChanged(SpireConstruct spire, int oldTeam, int newTeam)
    {
        // Immediate refresh when ownership changes
        RefreshScoreboard();
    }

    /// <summary>
    /// Manually refresh all scoreboard statistics.
    /// Call this from external systems or use auto-refresh.
    /// </summary>
    [ContextMenu("Refresh Scoreboard")]
    public void RefreshScoreboard()
    {
        if (TurnManager.inst == null) return;

        int playerTeam = TurnManager.inst.playerTeamId;
        int aiTeam = TurnManager.inst.aiTeamId;
        int neutralTeam = (int)SpireConstruct.OwnerType.Neutral;

        // Get all spires in scene
        var allSpires = FindObjectsOfType<SpireConstruct>();
        var allUnits = FindObjectsOfType<Units>();

        // Count spires by team
        int playerSpires = allSpires.Count(s => s.teamID == playerTeam);
        int aiSpires = allSpires.Count(s => s.teamID == aiTeam);
        int neutralSpires = allSpires.Count(s => s.teamID == neutralTeam);

        // Calculate unit totals by team
        var playerStats = CalculateTeamStats(allSpires, allUnits, playerTeam);
        var aiStats = CalculateTeamStats(allSpires, allUnits, aiTeam);

        // Update turn counter
        int currentTurn = TurnManager.inst != null ? TurnManager.inst.turnCount : 0;
        UpdateText(turnCounterText, $"Turn: {currentTurn}");
        cachedTurnCount = currentTurn;

        // Update player stats
        UpdateText(playerSpireCountText, $"Spires: {playerSpires}");
        UpdateText(playerUnitCountText, $"Total Units: {playerStats.totalUnits}");
        UpdateText(playerGarrisonText, $"Garrison: {playerStats.garrisonUnits}");
        UpdateText(playerReserveText, $"Reserve: {playerStats.reserveUnits}");

        // Update AI stats
        UpdateText(aiSpireCountText, $"Spires: {aiSpires}");
        UpdateText(aiUnitCountText, $"Total Units: {aiStats.totalUnits}");
        UpdateText(aiGarrisonText, $"Garrison: {aiStats.garrisonUnits}");
        UpdateText(aiReserveText, $"Reserve: {aiStats.reserveUnits}");

        // Update neutral stats
        UpdateText(neutralSpireCountText, $"Neutral Spires: {neutralSpires}");
    }

    private struct TeamStats
    {
        public int totalUnits;
        public int garrisonUnits;
        public int reserveUnits;
        public int traversingUnits;
    }

    private TeamStats CalculateTeamStats(SpireConstruct[] spires, Units[] units, int teamID)
    {
        var stats = new TeamStats();

        // Count garrison and reserve from spires
        foreach (var spire in spires)
        {
            if (spire == null || spire.teamID != teamID) continue;
            stats.garrisonUnits += spire.GetTotalGarrisonCount();
            stats.reserveUnits += spire.remainingGarrison;
        }

        // Count traversing units
        foreach (var unit in units)
        {
            if (unit == null || unit.teamID != teamID) continue;
            if (unit.state == Units.UnitState.Traversing)
            {
                stats.traversingUnits += unit.unitCount;
            }
        }

        stats.totalUnits = stats.garrisonUnits + stats.reserveUnits + stats.traversingUnits;
        return stats;
    }

    private void UpdateText(TextMeshProUGUI textComponent, string content)
    {
        if (textComponent != null)
        {
            textComponent.text = content;
        }
    }

    /// <summary>
    /// Returns formatted statistics as a string for debugging or external display.
    /// </summary>
    public string GetStatsString()
    {
        if (TurnManager.inst == null) return "TurnManager not found";

        var allSpires = FindObjectsOfType<SpireConstruct>();
        var allUnits = FindObjectsOfType<Units>();

        int playerTeam = TurnManager.inst.playerTeamId;
        int aiTeam = TurnManager.inst.aiTeamId;

        int playerSpires = allSpires.Count(s => s.teamID == playerTeam);
        int aiSpires = allSpires.Count(s => s.teamID == aiTeam);

        var playerStats = CalculateTeamStats(allSpires, allUnits, playerTeam);
        var aiStats = CalculateTeamStats(allSpires, allUnits, aiTeam);

        return $"Turn {cachedTurnCount}\n" +
               $"Player: {playerSpires} spires, {playerStats.totalUnits} units\n" +
               $"AI: {aiSpires} spires, {aiStats.totalUnits} units";
    }
}