using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager inst;
    public int playerTeamId = 0;
    public int aiTeamId = 1;
    public int turnCount = 0;

    public enum Phase { Init, PlayerPlanning, PlayerResolving, AiPlanning, AiResolving, GameOver }
    public Phase CurrentPhase { get; private set; } = Phase.Init;
    public bool PlayerInputEnabled => CurrentPhase == Phase.PlayerPlanning;

    private readonly List<Units> queuedPlayer = new List<Units>();
    private readonly List<Units> queuedAi = new List<Units>();
    private bool isResolving = false;

    void Awake() { inst = this; }

    void Start()
    {
        if (CurrentPhase == Phase.Init)
        {
            StartGame();
        }
    }

    public void QueueUnits(Units u)
    {
        if (u == null) return;
        if (u.teamID == playerTeamId)
        {
            if (!queuedPlayer.Contains(u)) queuedPlayer.Add(u);
        }
        else
        {
            if (!queuedAi.Contains(u)) queuedAi.Add(u);
        }
    }

    // Initialize and start the first player turn
    public void StartGame()
    {
        if (CurrentPhase != Phase.Init) return;
        StartPlayerPlanning();
    }

    // Call this from an "End Turn" button to end player planning and resolve player moves
    public void EndPlayerTurn()
    {
        if (CurrentPhase != Phase.PlayerPlanning || isResolving) return;
        StartCoroutine(ResolvePlayerThenAi());
    }

    private void 
    StartPlayerPlanning()
    {
        turnCount++;
        // If player has no units and AI has less-than-or-equal spires, skip player's turn
        if (ShouldSkipPlayerTurn())
        {
            // Run AI turns back-to-back until it either finishes its units or gains a spire lead
            StartCoroutine(RunAiAutoLoop());
            return;
        }

        CurrentPhase = Phase.PlayerPlanning;
        // PlayerInputEnabled becomes true via property
    }

    private System.Collections.IEnumerator ResolvePlayerThenAi()
{
    isResolving = true;
    Debug.Log($"[TurnManager] === ResolvePlayerThenAi START ===");
    turnCount++;
    CurrentPhase = Phase.PlayerResolving;
    Debug.Log($"[TurnManager] Phase: PlayerResolving, queued player moves: {queuedPlayer.Count}");

    ResolveConflicts(queuedPlayer);
    yield return ResolveTeamAsync(queuedPlayer);

    Debug.Log($"[TurnManager] Phase: AiPlanning");
    CurrentPhase = Phase.AiPlanning;

    var ai = FindObjectOfType<MinimaxAI>();
    if (ai != null)
    {
        Debug.Log($"[TurnManager] Calling AI.PlanAndQueueAIMoves, AI.IsBusy={ai.IsBusy}");
        ai.PlanAndQueueAIMoves();
        
        int guard = 0;
        while (ai.IsBusy && guard < 300)
        {
            guard++;
            yield return null;
        }
        
        if (guard >= 300)
        {
            Debug.LogError($"[TurnManager] AI planning timeout! IsBusy still true after 300 frames");
        }
        else
        {
            Debug.Log($"[TurnManager] AI planning completed after {guard} frames");
        }
    }

    Debug.Log($"[TurnManager] Phase: AiResolving, queued AI moves: {queuedAi.Count}");
    CurrentPhase = Phase.AiResolving;

    ResolveConflicts(queuedAi);
    yield return ResolveTeamAsync(queuedAi);

    // NEW: Add debug logging for regeneration
    Debug.Log($"[TurnManager] Regenerating reserves for {FindObjectsOfType<SpireConstruct>().Length} spires");
    
    foreach (var spire in FindObjectsOfType<SpireConstruct>())
    {
        spire.ResetTurn();
    }

    isResolving = false;
    Debug.Log($"[TurnManager] === ResolvePlayerThenAi END ===");
    StartPlayerPlanning();
}

    private void ResolveConflicts(List<Units> queued)
    {
        for (int i = 0; i < queued.Count; i++)
        {
            for (int j = i + 1; j < queued.Count; j++)
            {
                Units a = queued[i];
                Units b = queued[j];
                if (a == null || b == null) continue;
                if (a.HasPathConflict(b))
                {
                    a.ResolveCombat(b);
                    if (a.unitCount <= 0) a.DestroyUnitGroup();
                    if (b.unitCount <= 0) b.DestroyUnitGroup();
                }
            }
        }
        // Remove destroyed or null units
        queued.RemoveAll(u => u == null || u.unitCount <= 0 || u.state == Units.UnitState.Destroyed);
    }

    private System.Collections.IEnumerator ResolveTeamAsync(List<Units> queued)
    {
        // Execute surviving movements (animated) and wait for completion of all
        int pending = 0;

        foreach (var u in queued.ToArray())
        {
            if (u != null && u.unitCount > 0 && u.state == Units.UnitState.Traversing)
            {
                pending++;
                u.ExecuteMovementAnimatedWithCallback(() => { pending = Mathf.Max(0, pending - 1); });
            }
        }

        // Wait until all complete
        while (pending > 0) yield return null;

        // Clear queue
        queued.Clear();
    }

    // =========================
    // Auto-skip loop when player has no units
    // =========================
    // TurnManager.cs
    private bool ShouldSkipPlayerTurn()
    {
        if (turnCount <= 1) return false; // Never skip first turn
        
        int playerUnits = CountActiveUnits(playerTeamId);
        if (playerUnits > 0) return false;
        
        int aiSpires = CountSpires(aiTeamId);
        int playerSpires = CountSpires(playerTeamId);
        return aiSpires <= playerSpires;
    }

    private System.Collections.IEnumerator RunAiAutoLoop()
    {
        if (isResolving)
    {
        Debug.LogError("[TurnManager] RunAiAutoLoop called while already resolving!");
        yield break;
    }
    
    isResolving = true;
    Debug.Log("[TurnManager] === ENTERING AI AUTO-LOOP ===");

    bool stop = false;
    int loopCount = 0;
    int maxLoops = 100; // Safety limit
    
    while (!stop && loopCount < maxLoops)
    {
        loopCount++;
        Debug.Log($"[TurnManager] AI auto-loop iteration {loopCount}");
        
        CurrentPhase = Phase.AiPlanning;

        var ai = FindObjectOfType<MinimaxAI>();
        if (ai != null)
        {
            ai.PlanAndQueueAIMoves();
            int guard = 0;
            while (ai.IsBusy && guard < 300)
            {
                guard++;
                yield return null;
            }
            
            if (guard >= 300)
            {
                Debug.LogError("[TurnManager] AI auto-loop: AI planning timeout!");
                stop = true;
                break;
            }
        }

        CurrentPhase = Phase.AiResolving;
        ResolveConflicts(queuedAi);
        yield return ResolveTeamAsync(queuedAi);

        foreach (var spire in FindObjectsOfType<SpireConstruct>())
            spire.ResetTurn();

        int aiUnits = CountActiveUnits(aiTeamId);
        int playerUnits = CountActiveUnits(playerTeamId);
        int aiSpires = CountSpires(aiTeamId);
        int playerSpires = CountSpires(playerTeamId);
        
        Debug.Log($"[TurnManager] AI auto-loop: aiUnits={aiUnits}, aiSpires={aiSpires}, playerSpires={playerSpires}");

        // Stop conditions
        if (aiUnits == 0 || aiSpires > playerSpires)
        {
            Debug.Log($"[TurnManager] AI auto-loop stopping: aiUnits={aiUnits}, aiSpires={aiSpires}, playerSpires={playerSpires}");
            stop = true;
        }

        if (aiUnits == 0 && playerUnits == 0)
        {
            Debug.Log("[TurnManager] AI auto-loop stopping: both sides out of units");
            stop = true;
        }

        yield return null;
    }

    if (loopCount >= maxLoops)
    {
        Debug.LogError($"[TurnManager] AI auto-loop hit safety limit of {maxLoops} iterations!");
    }

    isResolving = false;
    Debug.Log("[TurnManager] === EXITING AI AUTO-LOOP ===");
    StartPlayerPlanning();
    }

    private int CountActiveUnits(int team)
    {
        var all = FindObjectsOfType<Units>();
        int count = 0;
        foreach (var u in all)
        {
            if (u != null && u.teamID == team && u.unitCount > 0 && u.state != Units.UnitState.Destroyed)
            {
                count += u.unitCount;
            }
        }
        return count;
    }

    private int CountSpires(int team)
    {
        // Prefer GameMgr lists if available
        if (GameMgr.inst != null)
        {
            if (team == playerTeamId) return GameMgr.inst.playerSpires.Count;
            if (team == aiTeamId) return GameMgr.inst.aiSpires.Count;
        }
        return FindObjectsOfType<SpireConstruct>().Count(s => s != null && s.teamID == team);
    }

    public void EndGame()
    {
        CurrentPhase = Phase.GameOver;
    }
}
