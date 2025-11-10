// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using UnityEngine;
using System;

/// <summary>
/// Manages progression between levels and adjusts difficulty based on player performance.
/// Integrates with SpireGenerator and MinimaxAI_Clean to scale challenge appropriately.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager inst;

    [Header("Current Level State")]
    [SerializeField, Tooltip("Current level number (0-indexed)")]
    private int currentLevel = 0;

    [SerializeField, Tooltip("Current computed difficulty [0..1]")]
    [Range(0f, 1f)]
    private float currentDifficulty = 0f;

    [Header("Base Difficulty Progression")]
    [Tooltip("Starting difficulty for level 0")]
    [Range(0f, 1f)]
    public float baseDifficultyStart = 0f;

    [Tooltip("Maximum base difficulty cap")]
    [Range(0f, 1f)]
    public float baseDifficultyMax = 1f;

    [Tooltip("How much base difficulty increases per level (before performance modifiers)")]
    [Range(0f, 0.3f)]
    public float baseDifficultyIncrement = 0.1f;

    [Tooltip("Curve controlling difficulty progression (0=linear, >0=exponential growth)")]
    [Range(0f, 2f)]
    public float difficultyGrowthCurve = 1.0f;

    [Header("Performance-Based Adjustments")]
    [Tooltip("Enable dynamic difficulty adjustment based on player performance")]
    public bool enableAdaptiveDifficulty = true;

    [Tooltip("Performance statistics tracker reference")]
    public PerformanceStats performanceStats;

    [Tooltip("How strongly performance affects difficulty (0=none, 1=full weight)")]
    [Range(0f, 1f)]
    public float performanceInfluence = 0.5f;

    [Header("Level Transition")]
    [Tooltip("SpireGenerator to update on level start")]
    public SpireGenerator spireGenerator;

    [Tooltip("MinimaxAI to update on level start")]
    public MinimaxAI minimaxAI;
    public MapController mapController; 

    [Tooltip("Regenerate map when advancing levels")]
    public bool regenerateMapOnAdvance = true;

    [Tooltip("Automatically start next level after match ends")]
    public bool autoProgressLevels = false;

    [Tooltip("Delay before auto-starting next level (seconds)")]
    public float autoProgressDelay = 3f;

    /// <summary>
    /// Initializes the singleton instance and auto-finds required component references if not assigned in the Inspector.
    /// Ensures only one LevelManager persists across scene loads.
    /// </summary>
    private void Awake()
    {
        if (inst == null)
        {
            inst = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto-find references if not assigned
        if (spireGenerator == null) spireGenerator = FindObjectOfType<SpireGenerator>();
    if (minimaxAI == null) minimaxAI = FindObjectOfType<MinimaxAI>();
        if (performanceStats == null) performanceStats = GetComponent<PerformanceStats>();
    }

    private void Start()
    {
        // Initialize first level
        currentDifficulty = CalculateDifficulty(currentLevel);
        ApplyDifficultyToSystems();
    }

    /// <summary>
    /// Calculates the difficulty for a given level, combining base progression and performance modifiers.
    /// </summary>
    /// <param name="level">Level number to calculate for</param>
    /// <returns>Difficulty value [0..1]</returns>
    public float CalculateDifficulty(int level)
    {
        // Base difficulty from level progression
        float levelProgress = level * baseDifficultyIncrement;
        float baseDifficulty = baseDifficultyStart + Mathf.Pow(levelProgress, difficultyGrowthCurve);
        baseDifficulty = Mathf.Clamp01(baseDifficulty);

        // Apply performance-based adjustment if enabled
        if (enableAdaptiveDifficulty && performanceStats != null)
        {
            float performanceModifier = performanceStats.GetPerformanceScore();
            baseDifficulty = Mathf.Lerp(baseDifficulty, baseDifficulty * performanceModifier, performanceInfluence);
        }

        // Clamp to max difficulty
        return Mathf.Min(baseDifficulty, baseDifficultyMax);
    }

    /// <summary>
    /// Advances to the next level and recalculates difficulty.
    /// </summary>
    public void AdvanceLevel()
    {
        currentLevel++;
        currentDifficulty = CalculateDifficulty(currentLevel);

        Debug.Log($"LevelManager: Advanced to Level {currentLevel} with difficulty {currentDifficulty:F2}");

        ApplyDifficultyToSystems();

        // Trigger map regeneration if enabled
        if (regenerateMapOnAdvance && mapController != null)
        {
            mapController.RegenerateMap();
        }

        if (performanceStats != null)
        {
            performanceStats.ResetStats();
        }
    }

    /// <summary>
    /// Restarts the current level without advancing.
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log($"LevelManager: Restarting Level {currentLevel}");

        ApplyDifficultyToSystems();

        if (performanceStats != null)
        {
            performanceStats.ResetStats();
        }
    }

    /// <summary>
    /// Resets progression to level 0.
    /// </summary>
    public void ResetProgression()
    {
        currentLevel = 0;
        currentDifficulty = CalculateDifficulty(currentLevel);

        Debug.Log($"LevelManager: Reset to Level 0");

        ApplyDifficultyToSystems();

        if (performanceStats != null)
        {
            performanceStats.ResetStats();
        }
    }

    /// <summary>
    /// Applies current difficulty settings to SpireGenerator and MinimaxAI.
    /// </summary>
    private void ApplyDifficultyToSystems()
    {
        // Update SpireGenerator difficulty
        if (spireGenerator != null)
        {
            spireGenerator.difficulty = currentDifficulty;
            Debug.Log($"LevelManager: Set SpireGenerator difficulty to {currentDifficulty:F2}");
        }

        // Update AI parameters based on difficulty
        if (minimaxAI != null)
        {
            ApplyDifficultyToAI(currentDifficulty);
        }
    }

    /// <summary>
    /// Scales MinimaxAI parameters based on difficulty level.
    /// </summary>
    private void ApplyDifficultyToAI(float difficulty)
    {
        // Scale search depth (1-5 based on difficulty)
        minimaxAI.AISettings.SearchDepth = Mathf.RoundToInt(Mathf.Lerp(1, 5, difficulty));

        // Scale move consideration breadth
        minimaxAI.AISettings.GlobalMoveCap = Mathf.RoundToInt(Mathf.Lerp(8, 40, difficulty));

        // Enable reinforcement at mid+ difficulty
        minimaxAI.AISettings.AllowReinforce = difficulty > 0.5f;

        // Optionally bias aggression with difficulty
        minimaxAI.AISettings.k_attack = Mathf.RoundToInt(Mathf.Lerp(8, 16, difficulty));
        minimaxAI.AISettings.k_self   = Mathf.RoundToInt(Mathf.Lerp(1, 4, difficulty));

        Debug.Log($"LevelManager: Set Clean AI depth={minimaxAI.AISettings.SearchDepth}, cap={minimaxAI.AISettings.GlobalMoveCap}");
    }

    /// <summary>
    /// Called when a match ends. Handles level progression if enabled.
    /// </summary>
    public void OnMatchEnd(ScoreMgr.GameResult result)
    {
        if (performanceStats != null)
        {
            performanceStats.RecordMatchResult(result);
        }

        if (autoProgressLevels && result != ScoreMgr.GameResult.Draw)
        {
            Invoke(nameof(AdvanceLevel), autoProgressDelay);
        }
    }



    // Public accessors
    public int CurrentLevel => currentLevel;
    public float CurrentDifficulty => currentDifficulty;
}