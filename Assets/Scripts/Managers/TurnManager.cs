using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of TurnManager. Controls the turn-based game loop.
    /// </summary>
    public static TurnManager Instance { get; private set; }
    
    // Legacy compatibility - will be removed in future
    public static TurnManager inst => Instance;
    
    public int playerTeamId = 0;
    public int aiTeamId = 1;
    public int turnCount = 0;
    [Tooltip("If true, game auto-starts on Start(). If false, waits in Init phase until StartGame() is called.")]
    public bool AutoStart = true;
    // Track whether we've granted the player at least one interactive planning phase
    private bool playerHadInteractiveTurn = false;
    // Mark when initial spires have been found so we don't trigger premature GameOver
    private bool initialSpiresEstablished = false;

    public enum Phase { Init, PlayerPlanning, PlayerResolving, AiPlanning, AiResolving, GameOver }
    public Phase CurrentPhase { get; private set; } = Phase.Init;
    public bool PlayerInputEnabled => CurrentPhase == Phase.PlayerPlanning;

    private readonly List<Units> queuedPlayer = new List<Units>();
    private readonly List<Units> queuedAi = new List<Units>();
    private bool isResolving = false;

    void Awake() 
    { 
        // Enforce singleton pattern - destroy duplicates
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[TurnManager] Duplicate instance detected, destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    private void OnDestroy()
    {
        // Clean up singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
    }

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

        /// <summary>
        /// Determines if a team's turn should be skipped due to lack of units.
        /// Uses GameMgr as single source of truth for unit counts.
        /// </summary>
        /// <param name="teamId">Team ID to check (0=Player, 1=AI)</param>
        /// <returns>True if turn should be skipped, false if team can still play</returns>
        private bool ShouldSkipTeamTurn(int teamId)
        {
            // SINGLE SOURCE OF TRUTH: Use GameMgr for unit counts
            if (GameMgr.inst == null) 
            {
                Debug.LogWarning("[TurnManager] GameMgr is null, cannot determine turn skip");
                return false; // Default to not skipping if systems not ready
            }
            
            // Get remaining units (reserves) from GameMgr
            int remainingUnits = (teamId == playerTeamId) 
                ? GameMgr.inst.remainingPlayerUnits 
                : GameMgr.inst.remainingAiUnits;
            
            // Also check if team has any owned spires with reserves
            int reservesAtSpires = 0;
            var spires = (teamId == playerTeamId) 
                ? GameMgr.inst.playerSpires 
                : GameMgr.inst.aiSpires;
            
            foreach (var spire in spires)
            {
                if (spire != null)
                {
                    reservesAtSpires += spire.remainingGarrison;
                }
            }
            
            bool shouldSkip = (remainingUnits <= 0 && reservesAtSpires <= 0);
            
            if (shouldSkip)
            {
                Debug.Log($"[TurnManager] Skipping team {teamId} turn: {remainingUnits} units in reserves, {reservesAtSpires} at spires");
            }
            
            return shouldSkip;
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
        /// <summary>
        /// Checks if game over conditions are met and transitions to GameOver phase if so.
        /// Victory condition: Game ends when BOTH sides run out of units (resource exhaustion).
        /// Then compares spire counts to determine winner.
        /// This matches the presentation description.
        /// </summary>
        /// <returns>True if game over was triggered, false if game continues</returns>
        private bool CheckAndSetGameOver()
        {
            if (CurrentPhase == Phase.GameOver) return true;
            
            // Wait for systems to be ready
            if (GameMgr.inst == null) return false;
            
            // Get unit and spire counts
            int playerUnits = GameMgr.inst.remainingPlayerUnits;
            int aiUnits = GameMgr.inst.remainingAiUnits;
            int playerSpires = CountSpires(playerTeamId);
            int aiSpires = CountSpires(aiTeamId);
            
            // Game ends only when BOTH sides run out of units (resource exhaustion)
            if (playerUnits <= 0 && aiUnits <= 0)
            {
                // Compare spire counts to determine winner
                if (playerSpires > aiSpires)
                {
                    Debug.Log($"[TurnManager] Game Over - Player wins with {playerSpires} spires vs AI's {aiSpires}");
                }
                else if (aiSpires > playerSpires)
                {
                    Debug.Log($"[TurnManager] Game Over - AI wins with {aiSpires} spires vs Player's {playerSpires}");
                }
                else
                {
                    Debug.Log($"[TurnManager] Game Over - Draw with {playerSpires} spires each");
                }
                
                EndGame();
                return true;
            }
            
            // Optional: Also end if one side has no spires (can't generate more units)
            if (playerSpires == 0 && aiSpires > 0)
            {
                Debug.Log("[TurnManager] Game Over - Player lost all spires (cannot generate more units)");
                EndGame();
                return true;
            }
            else if (aiSpires == 0 && playerSpires > 0)
            {
                Debug.Log("[TurnManager] Game Over - AI lost all spires (cannot generate more units)");
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
    /// SIMPLIFIED: Just reload the scene to avoid complex state management bugs
    /// </summary>
    private System.Collections.IEnumerator ResetGameAfterDelay(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        
        Debug.Log("[TurnManager] Reloading scene for fresh start...");
        
        // Simple scene reload is much more reliable than manual cleanup
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
}
