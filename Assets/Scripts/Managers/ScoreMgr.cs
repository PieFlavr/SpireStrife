// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Linq;
using UnityEngine;

public class ScoreMgr : MonoBehaviour
{
	public static ScoreMgr inst;

	public enum GameResult { None, PlayerWin, AiWin, Draw }
	public GameResult result = GameResult.None;

	public bool isFinalized => result != GameResult.None;

	[Header("Debug Snapshot")]
	public int lastPlayerUnits;
	public int lastAiUnits;
	public int lastPlayerSpires;
	public int lastAiSpires;

	// Prevent premature finalize at scene start
	[HideInInspector]
	public bool startedPlayObserved = false;

	void Awake()
	{
		inst = this;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void EnsureInstance()
	{
		if (FindObjectOfType<ScoreMgr>() == null)
		{
			var go = new GameObject("ScoreMgr");
			go.AddComponent<ScoreMgr>();
			DontDestroyOnLoad(go);
			Debug.Log("ScoreMgr auto-created at runtime.");
		}
	}

	public void ResetResult()
	{
		result = GameResult.None;
		lastPlayerUnits = lastAiUnits = lastPlayerSpires = lastAiSpires = 0;
		startedPlayObserved = false;
	}

	// Check end condition:
	// - "Finished units" uses GameMgr.remainingPlayerUnits / remainingAiUnits (reserves)
	// - If one side finishes units and the other already has MORE spires, that other side wins immediately
	// - Otherwise, keep running until both finish units; then compare spires for win/draw
	public void CheckAndFinalizeIfDone()
	{
		if (isFinalized) return;

		// Defer until systems are initialized
		if (TurnManager.inst == null || TurnManager.inst.CurrentPhase == TurnManager.Phase.Init)
			return;
		if (GameMgr.inst == null)
			return; // we rely on GameMgr fields per user request
		int knownSpires = GameMgr.inst.playerSpires.Count + GameMgr.inst.aiSpires.Count + GameMgr.inst.neutralSpires.Count;
		if (knownSpires == 0)
			return; // spires not discovered yet

		// Unit counts are already updated by UpdateUnitCounts() in Update()
		// Only allow finalize after we've seen any reserve > 0 at least once
		if (!startedPlayObserved)
		{
			if (lastPlayerUnits > 0 && lastAiUnits > 0)
				startedPlayObserved = true;
			else
				return; // both zero and never saw >0 yet: don't finalize at frame 0
		}

		// 1. One-sided finish immediate-win rule
		if (lastPlayerUnits == 0 && lastAiUnits > 0)
		{
			if (lastAiSpires > lastPlayerSpires)
			{
				result = GameResult.AiWin;
				Debug.Log("Result: AI wins (player finished units and AI already has more spires)");
				NotifyMatchEnd();
                return;
			}
			// else: keep running until AI also finishes units
		}
		// 2. One-sided Player win
		else if (lastAiUnits == 0 && lastPlayerUnits > 0)
		{
			if (lastPlayerSpires > lastAiSpires)
			{
				result = GameResult.PlayerWin;
				Debug.Log("Result: Player wins (AI finished units and Player already has more spires)");
                NotifyMatchEnd();
                return;
			}
			// else: keep running until Player also finishes units
		}

		// 3. Both finished -> final comparison including draw
		if (lastPlayerUnits == 0 && lastAiUnits == 0)
		{
			if (lastPlayerSpires > lastAiSpires)
			{
				result = GameResult.PlayerWin;
				Debug.Log("Result: Player wins (more spires when both sides finished units)");
			}
			else if (lastAiSpires > lastPlayerSpires)
			{
				result = GameResult.AiWin;
				Debug.Log("Result: AI wins (more spires when both sides finished units)");
			}
			else
			{
				result = GameResult.Draw;
				Debug.Log("Result: Draw (equal spires when both sides finished units)");
			}
            NotifyMatchEnd();
        }
	}

    private void NotifyMatchEnd()
    {
        // Notify TurnManager to end the game immediately
        if (TurnManager.inst != null)
        {
            TurnManager.inst.EndGame();
        }
        
        // Notify LevelManager of match end
        if (LevelManager.inst != null)
        {
            LevelManager.inst.OnMatchEnd(result);
        }
    }
    
    void Update()
    {
		// Poll-based check is acceptable here since finalization happens once per match
        
		if (!isFinalized)
        {
            // Update unit counts for TurnManager to use for skip logic
            UpdateUnitCounts();
			CheckAndFinalizeIfDone();
		}
	}

    /// <summary>
    /// Update unit and spire counts for external systems (like TurnManager skip logic)
    /// </summary>
    private void UpdateUnitCounts()
    {
        if (GameMgr.inst == null) return;
        
        // Update unit counts from GameMgr reserves
        lastPlayerUnits = Mathf.Max(0, GameMgr.inst.remainingPlayerUnits);
        lastAiUnits = Mathf.Max(0, GameMgr.inst.remainingAiUnits);
        
        // Update spire counts
        lastPlayerSpires = GameMgr.inst.playerSpires.Count;
        lastAiSpires = GameMgr.inst.aiSpires.Count;
    }

	private int TeamPlayer()
	{
		return TurnManager.inst != null ? TurnManager.inst.playerTeamId : 0;
	}

	private int TeamAi()
	{
		return TurnManager.inst != null ? TurnManager.inst.aiTeamId : 1;
	}

	private int CountActiveUnits(int team)
	{
		var all = Object.FindObjectsOfType<Units>();
		int total = 0;
		foreach (var u in all)
		{
			if (u != null && u.teamID == team && u.unitCount > 0 && u.state != Units.UnitState.Destroyed)
			{
				total += u.unitCount;
			}
		}
		return total;
	}

	private int CountSpires(int team)
	{
		if (GameMgr.inst != null)
		{
			if (team == TeamPlayer()) return GameMgr.inst.playerSpires.Count;
			if (team == TeamAi()) return GameMgr.inst.aiSpires.Count;
		}
		return Object.FindObjectsOfType<SpireConstruct>().Count(s => s != null && s.teamID == team);
	}
}
