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
    // Track whether we've granted the player at least one interactive planning phase
    private bool playerHadInteractiveTurn = false;
    // Track whether game over has been evaluated this frame
    private bool gameOverChecked = false;
    // Mark when initial spires have been found so we don't trigger premature GameOver
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
            // If a SpireGenerator exists and is still generating, defer start.
            var gen = FindObjectOfType<SpireGenerator>();
            if (gen != null && gen.IsGenerating)
            {
                // SpireGenerator will call StartGame() when done if configured.
                return;
            }
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
        // If spires aren't spawned yet, wait a short time before starting
        if (!HaveAnySpires())
        {
            StartCoroutine(WaitForInitialSpiresThenStart());
            return;
        }
        initialSpiresEstablished = true;
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
        
        // Reset AI for new turn cycle to prevent loops
        var ai = FindObjectOfType<MinimaxAI>();
        if (ai != null)
        {
            ai.ResetForNewTurn();
        }
        
        // Only declare game over after initial spires exist
        if (initialSpiresEstablished && CheckAndSetGameOver()) return;

        // Allow at least one interactive player turn even if they start with zero units (e.g. can select spires to spawn)
        if (!playerHadInteractiveTurn)
        {
            CurrentPhase = Phase.PlayerPlanning;
            playerHadInteractiveTurn = true;
            return;
        }

        // After first interactive turn, skip if player truly has no active units
        if (ShouldSkipTeamTurn(playerTeamId))
        {
            StartCoroutine(RunSingleAiTurnThenReturn());
            return;
        }

        CurrentPhase = Phase.PlayerPlanning;
        // PlayerInputEnabled becomes true via property
    }

    private System.Collections.IEnumerator ResolvePlayerThenAi()
    {
        isResolving = true;
        turnCount++;
        CurrentPhase = Phase.PlayerResolving;

        // Resolve conflicts within player moves if needed (rare in single-team queue)
        ResolveConflicts(queuedPlayer);

        yield return ResolveTeamAsync(queuedPlayer);

        // Check if AI should be skipped before planning
        if (ShouldSkipTeamTurn(aiTeamId))
        {
            Debug.Log("[TurnManager] AI has no units/reserves, skipping AI turn");
            foreach (var spire in FindObjectsOfType<SpireConstruct>())
                spire.ResetTurn();
            isResolving = false;
            if (!CheckAndSetGameOver())
                StartPlayerPlanning();
            yield break;
        }

        CurrentPhase = Phase.AiPlanning; // lock input by property

        // AI planning hook: use clean AI
    var ai = FindObjectOfType<MinimaxAI>();
        if (ai != null)
        {
            ai.PlanAndQueueAIMoves();
            // Wait until AI finishes async path planning so movement queues are ready this phase
            int guard = 0;
            while (ai.IsBusy && guard < 300)
            {
                guard++;
                yield return null;
            }
        }

        CurrentPhase = Phase.AiResolving;

    // Resolve conflicts within AI moves (again, single-team queue)
        ResolveConflicts(queuedAi);

        yield return ResolveTeamAsync(queuedAi);

        // New turn cycle: reset spires and go back to player planning
        foreach (var spire in FindObjectsOfType<SpireConstruct>())
        {
            spire.ResetTurn();
        }
        isResolving = false;
        if (!CheckAndSetGameOver())
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
    private bool ShouldSkipPlayerTurn()
    {
           // Legacy method retained for compatibility; now simply checks player unit count
           return ShouldSkipTeamTurn(playerTeamId);
    }

        private bool ShouldSkipTeamTurn(int teamId)
        {
            // Use ScoreMgr's unit tracking for accurate remaining units count
            if (ScoreMgr.inst != null)
            {
                // ScoreMgr tracks remaining units (reserves) from GameMgr
                int remainingUnits = (teamId == playerTeamId) 
                    ? ScoreMgr.inst.lastPlayerUnits 
                    : ScoreMgr.inst.lastAiUnits;
                
                // Skip turn if team has no remaining units
                if (remainingUnits <= 0)
                {
                    Debug.Log($"[TurnManager] Team {teamId} has {remainingUnits} units remaining, skipping turn");
                    return true;
                }
                
                return false;
            }
            
            // Fallback: Skip if no active units AND no available reserves at owned spires
            int activeUnits = CountActiveUnits(teamId);
            if (activeUnits > 0) return false;
            
            // Check if team has any reserves available
            var spires = FindObjectsOfType<SpireConstruct>();
            foreach (var s in spires)
            {
                if (s != null && s.teamID == teamId && s.remainingGarrison > 0)
                {
                    return false; // Has reserves, don't skip
                }
            }
            
            return true; // No units and no reserves, skip turn
        }

        // Single AI turn when player turn is skipped
        private System.Collections.IEnumerator RunSingleAiTurnThenReturn()
        {
            // If AI also has no units, end game
            if (ShouldSkipTeamTurn(aiTeamId))
            {
                CheckAndSetGameOver();
                yield break;
            }

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

            CurrentPhase = Phase.AiResolving;
            ResolveConflicts(queuedAi);
            yield return ResolveTeamAsync(queuedAi);

            foreach (var spire in FindObjectsOfType<SpireConstruct>())
                spire.ResetTurn();

            isResolving = false;
            if (!CheckAndSetGameOver())
                StartPlayerPlanning();
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
