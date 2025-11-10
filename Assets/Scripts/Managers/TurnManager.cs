using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager inst;
    public int playerTeamId = 0;
    public int aiTeamId = 1;
    public int turnCount = 0;
    [Tooltip("If true, game auto-starts on Start(). If false, waits in Init phase until StartGame() is called.")]
    public bool AutoStart = true;
    private bool playerHadInteractiveTurn = false;
    private bool gameOverChecked = false;
    private bool initialSpiresEstablished = false;

    public enum Phase { Init, PlayerPlanning, PlayerResolving, AiPlanning, AiResolving, GameOver }
    public Phase CurrentPhase { get; private set; } = Phase.Init;
    public bool PlayerInputEnabled => CurrentPhase == Phase.PlayerPlanning;

    private readonly List<Units> queuedPlayer = new List<Units>();
    private readonly List<Units> queuedAi = new List<Units>();
    private bool isResolving = false;

    void Awake() { inst = this; }

    void Start()
    {
        if (CurrentPhase == Phase.Init && AutoStart)
        {
            var gen = FindObjectOfType<SpireGenerator>();
            if (gen != null && gen.IsGenerating) return;
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

    public void StartGame()
    {
        if (CurrentPhase != Phase.Init) return;
        if (!HaveAnySpires())
        {
            StartCoroutine(WaitForInitialSpiresThenStart());
            return;
        }
        initialSpiresEstablished = true;
        StartPlayerPlanning();
    }

    public void EndPlayerTurn()
    {
        if (CurrentPhase != Phase.PlayerPlanning || isResolving) return;
        StartCoroutine(ResolvePlayerThenAi());
    }

    private void StartPlayerPlanning()
    {
        turnCount++;
        var ai = FindObjectOfType<MinimaxAI>();
        if (ai != null) ai.ResetForNewTurn();
        if (initialSpiresEstablished && CheckAndSetGameOver()) return;
        if (!playerHadInteractiveTurn)
        {
            CurrentPhase = Phase.PlayerPlanning;
            playerHadInteractiveTurn = true;
            return;
        }
        if (ShouldSkipTeamTurn(playerTeamId))
        {
            StartCoroutine(RunSingleAiTurnThenReturn());
            return;
        }
        CurrentPhase = Phase.PlayerPlanning;
    }

    private System.Collections.IEnumerator ResolvePlayerThenAi()
    {
        isResolving = true;
        turnCount++;
        CurrentPhase = Phase.PlayerResolving;
        ResolveConflicts(queuedPlayer);
        yield return ResolveTeamAsync(queuedPlayer, playerTeamId);
        if (ShouldSkipTeamTurn(aiTeamId))
        {
            Debug.Log("[TurnManager] AI has no units/reserves, skipping AI turn");
            foreach (var spire in FindObjectsOfType<SpireConstruct>()) spire.ResetTurn();
            isResolving = false;
            if (!CheckAndSetGameOver()) StartPlayerPlanning();
            yield break;
        }
        CurrentPhase = Phase.AiPlanning;
        var ai = FindObjectOfType<MinimaxAI>();
        if (ai != null)
        {
            ai.PlanAndQueueAIMoves();
            int guard = 0;
            while (ai.IsBusy && guard < 300) { guard++; yield return null; }
        }
        CurrentPhase = Phase.AiResolving;
        ResolveConflicts(queuedAi);
        yield return ResolveTeamAsync(queuedAi, aiTeamId);
        foreach (var spire in FindObjectsOfType<SpireConstruct>()) spire.ResetTurn();
        isResolving = false;
        if (!CheckAndSetGameOver()) StartPlayerPlanning();
    }

    private void ResolveConflicts(List<Units> queued)
    {
        for (int i = 0; i < queued.Count; i++)
        {
            for (int j = i + 1; j < queued.Count; j++)
            {
                Units a = queued[i]; Units b = queued[j];
                if (a == null || b == null) continue;
                if (a.HasPathConflict(b))
                {
                    a.ResolveCombat(b);
                    if (a.unitCount <= 0) a.DestroyUnitGroup();
                    if (b.unitCount <= 0) b.DestroyUnitGroup();
                }
            }
        }
        queued.RemoveAll(u => u == null || u.unitCount <= 0 || u.state == Units.UnitState.Destroyed);
    }

    private System.Collections.IEnumerator ResolveTeamAsync(List<Units> queued, int? teamIdOverride = null)
    {
        int pending = 0;
        foreach (var u in queued.ToArray())
        {
            if (u != null && u.unitCount > 0 && u.state == Units.UnitState.Traversing)
            {
                pending++;
                u.ExecuteMovementAnimatedWithCallback(() => { pending = Mathf.Max(0, pending - 1); });
            }
        }
        while (pending > 0) yield return null;
        int teamId = teamIdOverride ?? InferTeamFromQueue(queued);
        if (teamId >= 0)
        {
            int guard = 0; int guardMax = 1200;
            while (AnyTraversingUnits(teamId) && guard < guardMax)
            {
                guard++; yield return null;
            }
        }
        queued.Clear();
    }

    private int InferTeamFromQueue(List<Units> queued)
    {
        foreach (var u in queued) { if (u != null) return u.teamID; }
        return -1;
    }

    private bool AnyTraversingUnits(int teamId)
    {
        var all = FindObjectsOfType<Units>();
        foreach (var u in all)
        {
            if (u != null && u.teamID == teamId && u.state == Units.UnitState.Traversing) return true;
        }
        return false;
    }

    // =========================
    // Auto-skip loop when player has no units
    // =========================
    private bool ShouldSkipPlayerTurn() => ShouldSkipTeamTurn(playerTeamId);

    private bool ShouldSkipTeamTurn(int teamId)
    {
        if (ScoreMgr.inst != null)
        {
            int remainingUnits = (teamId == playerTeamId) ? ScoreMgr.inst.lastPlayerUnits : ScoreMgr.inst.lastAiUnits;
            if (remainingUnits <= 0)
            {
                Debug.Log($"[TurnManager] Team {teamId} has {remainingUnits} units remaining, skipping turn");
                return true;
            }
            return false;
        }
        int activeUnits = CountActiveUnits(teamId);
        if (activeUnits > 0) return false;
        var spires = FindObjectsOfType<SpireConstruct>();
        foreach (var s in spires)
        {
            if (s != null && s.teamID == teamId && s.remainingGarrison > 0) return false;
        }
        return true;
    }

        // Single AI turn when player turn is skipped
    private System.Collections.IEnumerator RunSingleAiTurnThenReturn()
    {
        if (isResolving) yield break; // guard against re-entrance
        isResolving = true;
        if (ShouldSkipTeamTurn(aiTeamId))
        {
            CheckAndSetGameOver();
            isResolving = false;
            yield break;
        }
        CurrentPhase = Phase.AiPlanning;
        var ai = FindObjectOfType<MinimaxAI>();
        if (ai != null)
        {
            ai.PlanAndQueueAIMoves();
            int guard = 0;
            while (ai.IsBusy && guard < 300) { guard++; yield return null; }
        }
        CurrentPhase = Phase.AiResolving;
        ResolveConflicts(queuedAi);
        yield return ResolveTeamAsync(queuedAi, aiTeamId);
        foreach (var spire in FindObjectsOfType<SpireConstruct>()) spire.ResetTurn();
        isResolving = false;
        if (!CheckAndSetGameOver()) StartPlayerPlanning();
    }

        // ================= Game Over Checks =================
        private bool CheckAndSetGameOver()
        {
            if (CurrentPhase == Phase.GameOver) return true;
            // Determine spire counts; game ends when one side has zero spires (or both zero)
            int playerSpires = CountSpires(playerTeamId);
            int aiSpires = CountSpires(aiTeamId);

            // Extra diagnostic detail: list spire names per team
            if (Application.isPlaying)
            {
                var all = FindObjectsOfType<SpireConstruct>();
                int rawPlayer = 0, rawAi = 0, rawNeutral = 0;
                List<string> playerNames = new();
                List<string> aiNames = new();
                List<string> neutralNames = new();
                foreach (var s in all)
                {
                    if (s == null) continue;
                    if (s.teamID == playerTeamId) { rawPlayer++; playerNames.Add(s.name); }
                    else if (s.teamID == aiTeamId) { rawAi++; aiNames.Add(s.name); }
                    else { rawNeutral++; neutralNames.Add(s.name); }
                }
                 Debug.Log($"[TurnManager] GameOver check. CountSpires(Player)={playerSpires}, CountSpires(AI)={aiSpires}, RawPlayer={rawPlayer}, RawAI={rawAi}, RawNeutral={rawNeutral}\nPlayerSpires:[{string.Join(", ", playerNames)}]\nAISpires:[{string.Join(", ", aiNames)}]\nNeutralSpires:[{string.Join(", ", neutralNames)}]");
            }

            if (playerSpires == 0 || aiSpires == 0)
            {
                string reason = playerSpires == 0 && aiSpires == 0 ? "Both sides lost all spires" : (playerSpires == 0 ? "Player lost all spires" : "AI lost all spires");
                Debug.Log($"[TurnManager] Entering GameOver. Reason: {reason}");
                EndGame();
                return true;
            }
            return false;
        }

    private bool HaveAnySpires()
    {
        // Check either GameMgr lists or raw scene
        int ps = CountSpires(playerTeamId);
        int aspires = CountSpires(aiTeamId);
        if (ps > 0 || aspires > 0) return true;
        return FindObjectsOfType<SpireConstruct>().Length > 0;
    }

    private System.Collections.IEnumerator WaitForInitialSpiresThenStart()
    {
        int guard = 0; int guardMax = 600; // ~10 seconds
        while (!HaveAnySpires() && guard < guardMax)
        {
            guard++; yield return null;
        }
        if (!HaveAnySpires())
        {
            Debug.LogWarning("[TurnManager] No spires detected after waiting; starting anyway.");
        }
        else
        {
            Debug.Log("[TurnManager] Initial spires detected; starting turns.");
            initialSpiresEstablished = true;
        }
        StartPlayerPlanning();
    }

    private System.Collections.IEnumerator RunAiAutoLoop()
    {
        // Prevent re-entrance
        if (isResolving) yield break;
        isResolving = true;

        bool stop = false;
        while (!stop)
        {
            // AI Planning
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
            }

            // AI Resolving
            CurrentPhase = Phase.AiResolving;
            ResolveConflicts(queuedAi);
            yield return ResolveTeamAsync(queuedAi);

            // Reset per-turn flags on spires at end of AI turn
            foreach (var spire in FindObjectsOfType<SpireConstruct>())
                spire.ResetTurn();

            // End-of-turn checks
            int aiUnits = CountActiveUnits(aiTeamId);
            int playerUnits = CountActiveUnits(playerTeamId);
            int aiSpires = CountSpires(aiTeamId);
            int playerSpires = CountSpires(playerTeamId);

            // Stop conditions: AI finished its units OR AI gained spire lead
            if (aiUnits == 0 || aiSpires > playerSpires)
            {
                stop = true;
            }

            // If both finished units, we can finalize immediately
            if (aiUnits == 0 && playerUnits == 0)
            {
                stop = true;
            }

            // Safety: avoid infinite loops
            yield return null;
        }

        isResolving = false;
        // Resume normal cycle (likely back to PlayerPlanning)
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
        Debug.Log("[TurnManager] Game Over - Starting reset sequence");
        StartCoroutine(ResetGameAfterDelay(3f));
    }

    /// <summary>
    /// Reset the entire game state and restart
    /// </summary>
    private System.Collections.IEnumerator ResetGameAfterDelay(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        
        Debug.Log("[TurnManager] Resetting game state...");
        
        // Reset all managers
        if (ScoreMgr.inst != null)
        {
            ScoreMgr.inst.ResetResult();
        }
        
        if (GameMgr.inst != null)
        {
            GameMgr.inst.remainingPlayerUnits = 0;
            GameMgr.inst.remainingAiUnits = 0;
            GameMgr.inst.RebuildSpireLists();
        }
        
        // Clear all queued units
        queuedPlayer.Clear();
        queuedAi.Clear();
        
        // Destroy all active unit groups
        var allUnits = FindObjectsOfType<Units>();
        foreach (var u in allUnits)
        {
            if (u != null)
            {
                Destroy(u.gameObject);
            }
        }
        
        // Destroy all spires - they will be regenerated with the new map
        var allSpires = FindObjectsOfType<SpireConstruct>();
        foreach (var spire in allSpires)
        {
            if (spire != null)
            {
                Destroy(spire.gameObject);
            }
        }
        
        // Reset turn state BEFORE regenerating map
        turnCount = 0;
        playerHadInteractiveTurn = false;
        gameOverChecked = false;
        initialSpiresEstablished = false;
        isResolving = false;
        CurrentPhase = Phase.Init;
        
        Debug.Log("[TurnManager] Waiting for cleanup to complete...");
        yield return new UnityEngine.WaitForSeconds(0.5f);
        
        // Regenerate the map if MapController exists
        var mapController = FindObjectOfType<MapController>();
        if (mapController != null)
        {
            Debug.Log("[TurnManager] Regenerating map via MapController...");
            yield return mapController.RegenerateMapCoroutine();
            
            // SpireGenerator will call StartGame() automatically if configured
            // Wait to see if spires were generated
            int waitFrames = 0;
            while (!HaveAnySpires() && waitFrames < 300)
            {
                waitFrames++;
                yield return null;
            }
            
            if (HaveAnySpires())
            {
                Debug.Log("[TurnManager] Spires detected after regeneration.");
                initialSpiresEstablished = true;
                // If SpireGenerator's startTurnManagerOnComplete is true, it will call StartGame()
                // Otherwise we need to call it manually
                yield return new UnityEngine.WaitForSeconds(0.5f);
                if (CurrentPhase == Phase.Init)
                {
                    Debug.Log("[TurnManager] Manually starting game after map regeneration...");
                    StartGame();
                }
            }
            else
            {
                Debug.LogError("[TurnManager] No spires generated after map regeneration!");
            }
        }
        else
        {
            // No MapController - try to regenerate via SpireGenerator directly
            var spireGen = FindObjectOfType<SpireGenerator>();
            if (spireGen != null)
            {
                Debug.Log("[TurnManager] Regenerating map via SpireGenerator directly...");
                yield return spireGen.GenerateSpiresCoroutine();
                yield return new UnityEngine.WaitForSeconds(0.5f);
                if (CurrentPhase == Phase.Init)
                {
                    StartGame();
                }
            }
            else
            {
                Debug.LogError("[TurnManager] No MapController or SpireGenerator found! Cannot regenerate map.");
                CurrentPhase = Phase.Init;
            }
        }
    }
}
