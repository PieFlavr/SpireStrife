// Author: Lucas Pinto
// Original Date: 2025-01-28
// Description: Central event hub for game-wide notifications. Decouples game logic from UI and other systems.

using System;

/// <summary>
/// Central event hub for game-wide notifications.
/// Decouples game logic from UI and other systems using the Observer pattern.
/// All events follow a publish-subscribe model where systems can listen without tight coupling.
/// </summary>
public static class GameEvents
{
    /// <summary>
    /// Fired when a match ends with a final result.
    /// Subscribe to this event to respond to game completion.
    /// </summary>
    /// <remarks>
    /// Listeners: LevelManager (performance tracking), EndScreen (display result), MatchSummaryUI (statistics)
    /// </remarks>
    public static event Action<ScoreMgr.GameResult> OnMatchEnded;

    /// <summary>
    /// Fired when a new level begins after map regeneration.
    /// Subscribe to this event to update UI elements or initialize level-specific logic.
    /// </summary>
    /// <param name="levelNumber">The newly started level number (0-indexed)</param>
    /// <param name="difficulty">The difficulty setting for this level [0..1]</param>
    /// <remarks>
    /// Listeners: Level display UI, difficulty indicator panels, analytics systems
    /// </remarks>
    public static event Action<int, float> OnLevelStarted;

    /// <summary>
    /// Fired when difficulty changes during gameplay (e.g., adaptive difficulty adjustment).
 /// Subscribe to this event to update difficulty displays or adjust game parameters.
    /// </summary>
    /// <param name="newDifficulty">The new difficulty value [0..1]</param>
    /// <remarks>
    /// Listeners: Debug UI panels, difficulty visualization, telemetry systems
    /// </remarks>
    public static event Action<float> OnDifficultyChanged;

    /// <summary>
    /// Invokes the match end event to notify all subscribers.
    /// Call this when a match concludes with a final result.
    /// </summary>
    /// <param name="result">The final match outcome (PlayerWin, AiWin, or Draw)</param>
    /// <remarks>
    /// Called by: ScoreMgr.NotifyMatchEnd() when game end conditions are met
    /// </remarks>
    public static void MatchEnded(ScoreMgr.GameResult result)
    {
        OnMatchEnded?.Invoke(result);
    }

    /// <summary>
    /// Invokes the level start event to notify all subscribers.
    /// Call this when a new level begins (typically after map regeneration).
    /// </summary>
    /// <param name="levelNumber">The level number that just started (0-indexed)</param>
    /// <param name="difficulty">The difficulty setting for the new level [0..1]</param>
    /// <remarks>
    /// Called by: LevelManager.AdvanceLevel() after difficulty calculation and map regeneration
    /// </remarks>
    public static void LevelStarted(int levelNumber, float difficulty)
{
        OnLevelStarted?.Invoke(levelNumber, difficulty);
    }

    /// <summary>
    /// Invokes the difficulty change event to notify all subscribers.
    /// Call this when game difficulty is adjusted outside of normal level progression.
    /// </summary>
    /// <param name="newDifficulty">The updated difficulty value [0..1]</param>
    /// <remarks>
    /// Called by: LevelManager or difficulty adjustment systems when settings change
    /// </remarks>
    public static void DifficultyChanged(float newDifficulty)
  {
        OnDifficultyChanged?.Invoke(newDifficulty);
    }
}
