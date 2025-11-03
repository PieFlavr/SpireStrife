// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks player performance metrics across matches to inform adaptive difficulty.
/// Add/remove stats by modifying the weight fields and calculation logic.
/// </summary>
public class PerformanceStats : MonoBehaviour
{
    [Header("Stat Weights (0 = ignored, 1 = full influence)")]
    [Tooltip("How much win/loss ratio affects difficulty")]
    [Range(0f, 1f)]
    public float winRateWeight = 1f;

    [Tooltip("How much spire control efficiency affects difficulty")]
    [Range(0f, 1f)]
    public float spireEfficiencyWeight = 0.7f;

    [Tooltip("How much unit preservation affects difficulty")]
    [Range(0f, 1f)]
    public float unitPreservationWeight = 0.5f;

    [Tooltip("How much turn efficiency affects difficulty")]
    [Range(0f, 1f)]
    public float turnEfficiencyWeight = 0.4f;

    [Tooltip("How much match completion time affects difficulty")]
    [Range(0f, 1f)]
    public float timeEfficiencyWeight = 0.3f;

    [Header("Performance Thresholds")]
    [Tooltip("Target win rate for balanced difficulty (0.5 = 50%)")]
    [Range(0f, 1f)]
    public float targetWinRate = 0.5f;

    [Tooltip("Number of recent matches to consider for averages")]
    [Range(1, 20)]
    public int rollingWindowSize = 5;

    [Header("Current Stats (Read-Only)")]
    [SerializeField, Tooltip("Total matches played this session")]
    private int totalMatches = 0;

    [SerializeField, Tooltip("Total wins")]
    private int wins = 0;

    [SerializeField, Tooltip("Total losses")]
    private int losses = 0;

    [SerializeField, Tooltip("Total draws")]
    private int draws = 0;

    [SerializeField, Tooltip("Current computed performance score [0..2]")]
    private float performanceScore = 1f;

    // Rolling window for recent match metrics
    private Queue<MatchMetrics> recentMatches = new Queue<MatchMetrics>();

    [System.Serializable]
    private struct MatchMetrics
    {
        public bool playerWon;
        public float spireControlRatio; // Player spires / Total spires at end
        public float unitPreservationRatio; // Remaining units / Starting units
        public int turnsTaken;
        public float matchDuration; // seconds
    }

    // Current match tracking
    private float matchStartTime;
    private int initialPlayerUnits;
    private int initialTotalSpires;

    private void Start()
    {
        BeginMatchTracking();
    }

    /// <summary>
    /// Call at the start of each match to begin tracking.
    /// </summary>
    public void BeginMatchTracking()
    {
        matchStartTime = Time.time;

        if (GameMgr.inst != null)
        {
            initialPlayerUnits = GameMgr.inst.remainingPlayerUnits;
            initialTotalSpires = GameMgr.inst.playerSpires.Count +
                                 GameMgr.inst.aiSpires.Count +
                                 GameMgr.inst.neutralSpires.Count;
        }
    }

    /// <summary>
    /// Records the result of a completed match and updates performance score.
    /// </summary>
    public void RecordMatchResult(ScoreMgr.GameResult result)
    {
        totalMatches++;

        // Update win/loss/draw counts
        if (result == ScoreMgr.GameResult.PlayerWin) wins++;
        else if (result == ScoreMgr.GameResult.AiWin) losses++;
        else if (result == ScoreMgr.GameResult.Draw) draws++;

        // Capture end-of-match metrics
        MatchMetrics metrics = new MatchMetrics
        {
            playerWon = result == ScoreMgr.GameResult.PlayerWin,
            spireControlRatio = CalculateSpireControlRatio(),
            unitPreservationRatio = CalculateUnitPreservationRatio(),
            turnsTaken = TurnManager.inst != null ? TurnManager.inst.turnCount : 0,
            matchDuration = Time.time - matchStartTime
        };

        // Add to rolling window
        recentMatches.Enqueue(metrics);
        if (recentMatches.Count > rollingWindowSize)
        {
            recentMatches.Dequeue();
        }

        // Recalculate performance score
        performanceScore = CalculatePerformanceScore();

        Debug.Log($"PerformanceStats: Match recorded. Score: {performanceScore:F2}, W/L: {wins}/{losses}");
    }

    /// <summary>
    /// Calculates a unified performance score from weighted stat components.
    /// Score range: 0.5 (struggling) to 1.5 (dominating), centered at 1.0 (balanced).
    /// </summary>
    private float CalculatePerformanceScore()
    {
        if (recentMatches.Count == 0) return 1f; // Neutral default

        float totalWeight = 0f;
        float weightedScore = 0f;

        // Component 1: Win Rate
        if (winRateWeight > 0f)
        {
            float winRate = (float)wins / Mathf.Max(1, totalMatches);
            // Scale: 0.5 at target, 1.5 if perfect, 0.5 if zero
            float winRateScore = 0.5f + (winRate / targetWinRate);
            weightedScore += winRateScore * winRateWeight;
            totalWeight += winRateWeight;
        }

        // Component 2: Spire Efficiency (from recent matches)
        if (spireEfficiencyWeight > 0f)
        {
            float avgSpireRatio = GetAverageMetric(m => m.spireControlRatio);
            // Scale: 1.0 at 50% control, 1.5 at 100%, 0.5 at 0%
            float spireScore = 0.5f + avgSpireRatio;
            weightedScore += spireScore * spireEfficiencyWeight;
            totalWeight += spireEfficiencyWeight;
        }

        // Component 3: Unit Preservation (from recent matches)
        if (unitPreservationWeight > 0f)
        {
            float avgPreservation = GetAverageMetric(m => m.unitPreservationRatio);
            // Scale: 1.0 at 50%, 1.5 at 100%, 0.5 at 0%
            float preservationScore = 0.5f + avgPreservation;
            weightedScore += preservationScore * unitPreservationWeight;
            totalWeight += unitPreservationWeight;
        }

        // Component 4: Turn Efficiency (lower turns = higher score)
        if (turnEfficiencyWeight > 0f)
        {
            float avgTurns = GetAverageMetric(m => (float)m.turnsTaken);
            // Normalize assuming 50 turns is "average", fewer is better
            float turnScore = Mathf.Clamp(1.5f - (avgTurns / 50f), 0.5f, 1.5f);
            weightedScore += turnScore * turnEfficiencyWeight;
            totalWeight += turnEfficiencyWeight;
        }

        // Component 5: Time Efficiency (faster wins = higher score)
        if (timeEfficiencyWeight > 0f)
        {
            float avgDuration = GetAverageMetric(m => m.matchDuration);
            // Normalize assuming 300s (5min) is "average"
            float timeScore = Mathf.Clamp(1.5f - (avgDuration / 300f), 0.5f, 1.5f);
            weightedScore += timeScore * timeEfficiencyWeight;
            totalWeight += timeEfficiencyWeight;
        }

        // Compute final weighted average
        float finalScore = totalWeight > 0f ? weightedScore / totalWeight : 1f;

        // Clamp to reasonable range [0.5..1.5]
        return Mathf.Clamp(finalScore, 0.5f, 1.5f);
    }

    /// <summary>
    /// Helper to compute average of a metric across recent matches.
    /// </summary>
    private float GetAverageMetric(System.Func<MatchMetrics, float> selector)
    {
        if (recentMatches.Count == 0) return 0f;

        float sum = 0f;
        foreach (var match in recentMatches)
        {
            sum += selector(match);
        }
        return sum / recentMatches.Count;
    }

    /// <summary>
    /// Calculates the ratio of player-controlled spires to total spires at match end.
    /// </summary>
    /// <returns>Ratio from 0.0 (no spires) to 1.0 (all spires controlled by player).</returns>
    private float CalculateSpireControlRatio()
    {
        if (GameMgr.inst == null) return 0f;

        int totalSpires = GameMgr.inst.playerSpires.Count +
                          GameMgr.inst.aiSpires.Count +
                          GameMgr.inst.neutralSpires.Count;

        if (totalSpires == 0) return 0f;

        return (float)GameMgr.inst.playerSpires.Count / totalSpires;
    }
    
    /// <summary>
    /// Calculates the ratio of remaining player units to initial player units at match end.
    /// </summary>
    /// <returns>Ratio from 0.0 (no units remaining) to 1.0 (all units preserved).</returns>
    private float CalculateUnitPreservationRatio()
    {
        if (GameMgr.inst == null || initialPlayerUnits == 0) return 0f;

        return (float)GameMgr.inst.remainingPlayerUnits / initialPlayerUnits;
    }

    /// <summary>
    /// Resets stats for a new level/session.
    /// </summary>
    public void ResetStats()
    {
        BeginMatchTracking();
    }

    /// <summary>
    /// Gets the current performance score for difficulty calculation.
    /// Returns: 0.5 (struggling) to 1.5 (dominating), centered at 1.0.
    /// </summary>
    public float GetPerformanceScore()
    {
        return performanceScore;
    }

    // Public read-only accessors
    public int TotalMatches => totalMatches;
    public int Wins => wins;
    public int Losses => losses;
    public int Draws => draws;
    public float WinRate => totalMatches > 0 ? (float)wins / totalMatches : 0f;
}