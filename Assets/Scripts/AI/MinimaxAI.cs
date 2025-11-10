using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Clean refactored AI MonoBehaviour: owns tunable settings and Unity integration.
/// Core search/simulation lives in static MinimaxAlgorithm for testability.
/// Implements Minimax with alpha-beta pruning for strategic decision making.
/// </summary>
public class MinimaxAI : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Header("AI Search")]
        [Tooltip("How many moves deep to search (e.g., 3 = AI, Player, AI)")]
        [Range(1, 7)] public int SearchDepth = 3;

        [Tooltip("Max number of moves to consider at each step (for performance). Note: Send amount is fixed by UiMgr.generateCountPerCommand")]
        [Range(4, 40)] public int GlobalMoveCap = 20;

        [Tooltip("Should the AI consider reinforcing its own spires?")]
        public bool AllowReinforce = true;

        [Header("AI Heuristic Weights (For Move Ordering)")]
        [Tooltip("Weight for reinforcing. Score = (new_units * k_self)")]
        [Min(1)] public int k_self = 1;

        [Tooltip("Weight for capturing. Score = (remaining_units * k_attack)")]
        [Min(1)] public int k_attack = 10;

        [Header("AI Evaluation Weights (For Final Board Score)")]
        [Tooltip("The value of owning one more spire than the opponent.")]
        [Min(1)] public int SpireOwnershipWeight = 100;

        [Tooltip("Weight for having one more unit (Reserve) than the opponent.")]
        [Min(0)] public int UnitWeight = 1;

        [Header("Strategic Expansion Weights")]
        [Tooltip("Weight for expansion potential (higher = more aggressive expansion)")]
        [Min(0)] public int ExpansionWeight = 150;

        [Tooltip("Weight for territorial control (distance advantage to neutral spires)")]
        [Min(0)] public int TerritorialControlWeight = 50;

        [Tooltip("Weight for strategic positioning (center control)")]
        [Min(0)] public int PositionalWeight = 30;

        [Header("AI Behavior")]
        [Tooltip("How much randomness to introduce into move selection. 0 = deterministic, > 0 = more random.")]
        [Range(0, 100)] public int Randomness = 25;
    }

    public Settings AISettings = new Settings();
    
    /// <summary>
    /// Singleton instance of MinimaxAI. Called by TurnManager during AI planning phase.
    /// </summary>
    public static MinimaxAI Instance { get; private set; }
    
    // Legacy compatibility - will be removed in future
    public static MinimaxAI inst => Instance;
    
    public bool IsBusy { get; private set; } = false;

    private HexGrid cachedGrid;
    private readonly Dictionary<(int, int), int> distanceCache = new Dictionary<(int, int), int>();
    
    // Loop prevention: track recent moves to avoid repetition
    private readonly List<(SpireConstruct from, SpireConstruct to)> recentMoves = new List<(SpireConstruct, SpireConstruct)>();
    private const int MAX_MOVE_HISTORY = 5;

    private void Awake()
    {
        // Enforce singleton pattern - destroy duplicates
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[MinimaxAI] Duplicate instance detected, destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        cachedGrid = FindObjectOfType<HexGrid>();
    }
    
    private void OnDestroy()
    {
        // Clean up singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Entry point invoked by TurnManager during AiPlanning.
    /// </summary>
    public void PlanAndQueueAIMoves()
    {
        if (IsBusy) return;
        StartCoroutine(PlanAndQueueAIMovesCoroutine());
    }

    /// <summary>
    /// Reset AI state for a new turn (called by TurnManager)
    /// </summary>
    public void ResetForNewTurn()
    {
        // Clear move history when starting a fresh turn cycle
        recentMoves.Clear();
    }

    private System.Collections.IEnumerator PlanAndQueueAIMovesCoroutine()
    {
        IsBusy = true;
        var grid = cachedGrid != null ? cachedGrid : FindObjectOfType<HexGrid>();
        if (grid == null) { IsBusy = false; yield break; }

        var allSpires = FindObjectsOfType<SpireConstruct>();
        if (allSpires == null || allSpires.Length == 0) { IsBusy = false; yield break; }

        int aiTeam = TurnManager.inst != null ? TurnManager.inst.aiTeamId : 1;
        int playerTeam = TurnManager.inst != null ? TurnManager.inst.playerTeamId : 0;

        var rootState = AIState.Snapshot(allSpires);

        AIMove bestMove = MinimaxAlgorithm.FindBestMove(
            rootState,
            aiTeam,
            playerTeam,
            AISettings.SearchDepth,
            AISettings,
            GetCachedDistance);

        // Loop prevention: check if this move was recently made
        if (bestMove != null && IsRepeatedMove(bestMove))
        {
            Debug.LogWarning("[MinimaxAI] Detected repeated move, finding alternative...");
            bestMove = FindAlternativeMove(rootState, aiTeam, playerTeam, bestMove);
        }

        if (bestMove != null)
        {
            // Track this move to prevent loops
            TrackMove(bestMove);
            
            bool finished = false;
            ExecuteWithPathfinder(bestMove, () => { finished = true; });
            int guard = 0; int guardMax = 240;
            while (!finished && guard < guardMax) { guard++; yield return null; }
        }
        else
        {
            Debug.Log("[MinimaxAI] No valid move found (may have no units or all moves blocked)");
        }

        IsBusy = false;
    }

    /// <summary>
    /// Check if a move has been made recently (loop detection)
    /// </summary>
    private bool IsRepeatedMove(AIMove move)
    {
        if (move == null || move.From == null || move.To == null) return false;
        
        // Check if this exact move appears in recent history
        int repeatCount = 0;
        foreach (var (from, to) in recentMoves)
        {
            if (from == move.From && to == move.To)
            {
                repeatCount++;
                if (repeatCount >= 2) // Allow once, block if repeated twice
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Track a move in history for loop prevention
    /// </summary>
    private void TrackMove(AIMove move)
    {
        if (move == null || move.From == null || move.To == null) return;
        
        recentMoves.Add((move.From, move.To));
        
        // Keep only recent moves
        while (recentMoves.Count > MAX_MOVE_HISTORY)
        {
            recentMoves.RemoveAt(0);
        }
    }

    /// <summary>
    /// Find an alternative move when the best move would create a loop
    /// </summary>
    private AIMove FindAlternativeMove(AIState rootState, int aiTeam, int playerTeam, AIMove blockedMove)
    {
        var allMoves = MinimaxAlgorithm.GetAllValidMoves(rootState, aiTeam, AISettings, GetCachedDistance, MinimaxAlgorithm.GetSendAmount());
        
        // Filter out the blocked move and other recent moves
        var alternatives = allMoves.Where(m => 
            !IsRepeatedMove(m) && 
            !(m.From == blockedMove.From && m.To == blockedMove.To)
        ).ToList();

        if (alternatives.Count == 0)
        {
            Debug.LogWarning("[MinimaxAI] No alternative moves available, clearing history and retrying original move");
            recentMoves.Clear(); // Reset to break deadlock
            return blockedMove;
        }

        // Evaluate alternatives and pick the best one
        int bestScore = int.MinValue;
        AIMove bestAlternative = alternatives[0];
        
        foreach (var m in alternatives.Take(10)) // Limit evaluation for performance
        {
            var s1 = rootState.SimulateAction(m, aiTeam, MinimaxAlgorithm.GetSendAmount());
            int sc = MinimaxAlgorithm.QuickEvaluate(s1, aiTeam, playerTeam, AISettings, GetCachedDistance);
            if (sc > bestScore)
            {
                bestScore = sc;
                bestAlternative = m;
            }
        }

        Debug.Log($"[MinimaxAI] Selected alternative move from {bestAlternative.From.name} to {bestAlternative.To.name}");
        return bestAlternative;
    }

    private void ExecuteWithPathfinder(AIMove action, System.Action onDone = null)
    {
        if (action == null || UiMgr.inst == null) { onDone?.Invoke(); return; }
        if (action.From == null || action.To == null) { onDone?.Invoke(); return; }
        if (action.From.parentCell == null || action.To.parentCell == null) { onDone?.Invoke(); return; }

        // Compute desired send amount (respect hardcoded cap, don't exceed reserve)
        int dist = GetCachedDistance(action.From, action.To);
        
        // Configure UiMgr for AI command (allow execution during AI phase)
        // Use the hardcoded generateCountPerCommand from UiMgr - don't override it
        UiMgr.inst.allowCommandWhenPlayerInputDisabled = true;
        UiMgr.inst.sendOverrideForNextCommand = null; // Don't override, use UiMgr default
        UiMgr.inst.alwaysGenerateOnCommand = true;

        UiMgr.inst.targetSpire = action.To;
        UiMgr.inst.selectedSpire = action.From;

        StartCoroutine(WaitForAiQueueThenCallback(onDone));
        UiMgr.inst.SelectCell(action.From.parentCell);
        UiMgr.inst.SelectTargetCell(action.To.parentCell);
    }

    private System.Collections.IEnumerator WaitForAiQueueThenCallback(System.Action onDone)
    {
        int aiTeam = TurnManager.inst != null ? TurnManager.inst.aiTeamId : 1;
        int guard = 0;
        while (guard < 180)
        {
            var anyQueued = FindObjectsOfType<Units>()
                .Any(u => u != null && u.teamID == aiTeam && u.state == Units.UnitState.Traversing);
            if (anyQueued) break;
            guard++; yield return null;
        }
        onDone?.Invoke();
    }

    private int GetCachedDistance(SpireConstruct a, SpireConstruct b)
    {
        if (a == null || b == null || a.parentCell == null || b.parentCell == null) return 999;
        var key = (a.GetInstanceID(), b.GetInstanceID());
        if (distanceCache.TryGetValue(key, out var d)) return d;
        var grid = cachedGrid != null ? cachedGrid : FindObjectOfType<HexGrid>();
        int dist = grid != null ? grid.GetDistance(a.parentCell, b.parentCell) : 999;
        distanceCache[key] = dist;
        return dist;
    }
}

// ================= Helper Data Structures =================
public class AIMove
{
    public SpireConstruct From;
    public SpireConstruct To;
    public int EstimatedDistance;
    public int HeuristicScore; // for ordering
}

public class AIState
{
    public class SpireSnapshot
    {
        public int TeamId;
        public int Reserve;  // units available for spawning
        public SpireConstruct LiveRef;
    }

    public List<SpireSnapshot> Spires;

    public static AIState Snapshot(SpireConstruct[] all)
    {
        var st = new AIState { Spires = new List<SpireSnapshot>() };
        foreach (var s in all)
        {
            if (s == null) continue;
            st.Spires.Add(new SpireSnapshot
            {
                TeamId = s.teamID,
                Reserve = s.remainingGarrison,
                LiveRef = s
            });
        }
        return st;
    }

    public AIState Clone()
    {
        var copy = new AIState { Spires = new List<SpireSnapshot>() };
        foreach (var s in Spires)
        {
            copy.Spires.Add(new SpireSnapshot
            {
                TeamId = s.TeamId,
                Reserve = s.Reserve,
                LiveRef = s.LiveRef
            });
        }
        return copy;
    }

    public AIState SimulateAction(AIMove move, int moverTeam, int maxSend)
    {
        var next = Clone();
        var from = next.Spires.FirstOrDefault(x => x.LiveRef == move.From);
        var to = next.Spires.FirstOrDefault(x => x.LiveRef == move.To);
        if (from == null || to == null) return next;

        // Mirror UiMgr flow: spawn from Reserve up to cap, then send that amount
        int toSpawn = Mathf.Min(maxSend, Mathf.Max(0, from.Reserve));
        int send = toSpawn;
        if (send <= 0) return next;

        // Apply spawning and sending
        from.Reserve -= toSpawn;

        // Travel attrition: arrival = send - distance
        int arriving = send - move.EstimatedDistance;
        if (arriving <= 0) return next;

        // Combat against defenders (only Reserve is updated during simulation)
        int R = to.Reserve;
        if (to.TeamId == moverTeam)
        {
            // Reinforce: add arriving to reserve
            to.Reserve += arriving;
        }
        else
        {
            int combat = arriving - R;
            if (combat > 0)
            {
                // Capture: victor's remainder becomes reserve, defender wiped
                to.TeamId = moverTeam;
                to.Reserve = combat;
            }
            else
            {
                // Defender holds; leftover stays as reserve
                to.Reserve = R - arriving;
            }
        }
        return next;
    }
}

public static class MinimaxAlgorithm
{
    // Use the hardcoded value from UiMgr.generateCountPerCommand (default: 10)
    // This should match the value in UiMgr to ensure simulation accuracy
    public static int GetSendAmount()
    {
        return (UiMgr.inst != null) ? UiMgr.inst.generateCountPerCommand : 10;
    }

    public static AIMove FindBestMove(AIState root, int aiTeam, int playerTeam, int depth,
    MinimaxAI.Settings settings,
        Func<SpireConstruct, SpireConstruct, int> getDistance)
    {
        int sendAmount = GetSendAmount();
        var moves = GetValidMoves(root, aiTeam, settings, getDistance, sendAmount);

        var scoredMoves = new List<(AIMove move, int score)>();

        foreach (var m in moves)
        {
            var s1 = root.SimulateAction(m, aiTeam, sendAmount);
            int sc = -Negamax(s1, depth - 1, int.MinValue + 1, int.MaxValue, playerTeam, aiTeam, settings, getDistance, sendAmount);
            scoredMoves.Add((m, sc));
        }

        if (scoredMoves.Count == 0)
        {
            return null;
        }

        // Sort by score descending
        scoredMoves = scoredMoves.OrderByDescending(m => m.score).ToList();
        var bestScoredMove = scoredMoves[0];

        if (settings.Randomness > 0 && scoredMoves.Count > 1)
        {
            // Filter for moves that are "good enough" (e.g., within a percentage of the best score)
            int bestScore = bestScoredMove.score;
            
            // Allow for randomness even with negative scores
            int scoreRange = scoredMoves.Max(m => m.score) - scoredMoves.Min(m => m.score);
            if (scoreRange == 0) scoreRange = Mathf.Abs(bestScore); // Handle case where all scores are equal
            if (scoreRange == 0) return bestScoredMove.move; // All moves are identical, no point in randomness

            int scoreThreshold = bestScore - (scoreRange * settings.Randomness / 100);

            var goodEnoughMoves = scoredMoves.Where(m => m.score >= scoreThreshold).ToList();

            if (goodEnoughMoves.Count > 1)
            {
                // Select a random move from the good enough moves
                int randomIndex = UnityEngine.Random.Range(0, goodEnoughMoves.Count);
                return goodEnoughMoves[randomIndex].move;
            }
        }

        return bestScoredMove.move;
    }

    private static int Negamax(AIState state, int depth, int alpha, int beta, int side,
    int aiTeam, MinimaxAI.Settings settings,
        Func<SpireConstruct, SpireConstruct, int> getDistance, int sendAmount)
    {
        int playerTeam = (aiTeam == 0) ? 1 : 0; // simplistic opposing id assumption
        
        // Terminal node or depth limit reached
        if (depth <= 0)
        {
            int eval = EvaluateComplete(state, aiTeam, playerTeam, settings, getDistance);
            return (side == aiTeam) ? eval : -eval;
        }
        
        var moves = GetValidMoves(state, side, settings, getDistance, sendAmount);
        
        // Terminal state check (no valid moves)
        if (moves.Count == 0)
        {
            int eval = EvaluateComplete(state, aiTeam, playerTeam, settings, getDistance);
            return (side == aiTeam) ? eval : -eval;
        }

        int best = int.MinValue + 1; // Avoid overflow
        
        // Complete minimax with alpha-beta pruning
        foreach (var m in moves)
        {
            var s2 = state.SimulateAction(m, side, sendAmount);
            int opp = (side == aiTeam) ? playerTeam : aiTeam;
            
            // Recursive negamax call with alpha-beta pruning
            int val = -Negamax(s2, depth - 1, -beta, -alpha, opp, aiTeam, settings, getDistance, sendAmount);
            
            // Update best score
            if (val > best) 
                best = val;
            
            // Alpha-beta pruning: update alpha
            if (val > alpha) 
                alpha = val;
            
            // Beta cutoff: prune remaining branches
            if (alpha >= beta) 
                break;
        }
        
        return best;
    }

    /// <summary>
    /// Get all valid moves (public version for loop prevention)
    /// </summary>
    public static List<AIMove> GetAllValidMoves(AIState state, int team,
        MinimaxAI.Settings settings,
        Func<SpireConstruct, SpireConstruct, int> getDistance, int sendAmount)
    {
        return GetValidMoves(state, team, settings, getDistance, sendAmount);
    }

    private static List<AIMove> GetValidMoves(AIState state, int team,
    MinimaxAI.Settings settings,
        Func<SpireConstruct, SpireConstruct, int> getDistance, int sendAmount)
    {
        var moves = new List<AIMove>();
        var sources = state.Spires.Where(s => s.TeamId == team);
        var targets = state.Spires.Where(s => s.TeamId != team || settings.AllowReinforce);
        foreach (var from in sources)
        {
            // Only consider sources that can actually send something (based on Reserve)
            // Use the hardcoded sendAmount from UiMgr
            int send = Mathf.Min(sendAmount, Mathf.Max(0, from.Reserve));
            if (send <= 0) continue;
            foreach (var to in targets)
            {
                if (from.LiveRef == to.LiveRef) continue;
                int dist = getDistance(from.LiveRef, to.LiveRef);
                int arriving = send - dist;
                if (arriving <= 0) continue; // invalid
                var mv = new AIMove { From = from.LiveRef, To = to.LiveRef, EstimatedDistance = dist };
                mv.HeuristicScore = MoveHeuristic(to, arriving, team, settings);
                moves.Add(mv);
            }
        }
        return moves.OrderByDescending(m => m.HeuristicScore).Take(settings.GlobalMoveCap).ToList();
    }

    private static int MoveHeuristic(AIState.SpireSnapshot to, int arriving, int team,
    MinimaxAI.Settings settings)
    {
        int R = to.Reserve; // Only Reserve is updated during simulation
        if (to.TeamId == team)
        {
            int newRemaining = arriving + R;
            return newRemaining * settings.k_self;
        }
        else
        {
            int newRemaining = arriving - R;
            if (newRemaining > 0) return newRemaining * settings.k_attack; // capture potential
            return newRemaining; // negative (loss/draw)
        }
    }

    public static int Evaluate(AIState state, int aiTeam, int playerTeam, MinimaxAI.Settings settings)
    {
        int aiSpires = 0, playerSpires = 0, aiUnits = 0, playerUnits = 0;
        foreach (var s in state.Spires)
        {
            if (s.TeamId == aiTeam) { aiSpires++; aiUnits += s.Reserve; }
            else if (s.TeamId == playerTeam) { playerSpires++; playerUnits += s.Reserve; }
        }
        int score = 0;
        score += (aiSpires - playerSpires) * settings.SpireOwnershipWeight;
        score += (aiUnits - playerUnits) * settings.UnitWeight;
        return score;
    }

    /// <summary>
    /// Quick evaluation for alternative move selection (no deep search)
    /// </summary>
    public static int QuickEvaluate(AIState state, int aiTeam, int playerTeam,
        MinimaxAI.Settings settings, Func<SpireConstruct, SpireConstruct, int> getDistance)
    {
        // Use simpler evaluation for speed when finding alternatives
        return Evaluate(state, aiTeam, playerTeam, settings);
    }

    /// <summary>
    /// Complete evaluation function with expansion and territorial control metrics
    /// </summary>
    public static int EvaluateComplete(AIState state, int aiTeam, int playerTeam, 
        MinimaxAI.Settings settings, Func<SpireConstruct, SpireConstruct, int> getDistance)
    {
        int aiSpires = 0, playerSpires = 0, neutralSpires = 0;
        int aiUnits = 0, playerUnits = 0;
        
        var aiOwnedSpires = new List<AIState.SpireSnapshot>();
        var playerOwnedSpires = new List<AIState.SpireSnapshot>();
        var neutrals = new List<AIState.SpireSnapshot>();
        
        // Categorize spires
        foreach (var s in state.Spires)
        {
            if (s.TeamId == aiTeam) 
            { 
                aiSpires++; 
                aiUnits += s.Reserve; 
                aiOwnedSpires.Add(s);
            }
            else if (s.TeamId == playerTeam) 
            { 
                playerSpires++; 
                playerUnits += s.Reserve; 
                playerOwnedSpires.Add(s);
            }
            else
            {
                neutralSpires++;
                neutrals.Add(s);
            }
        }
        
        int score = 0;
        
        // 1. Basic ownership and unit count (original metrics)
        score += (aiSpires - playerSpires) * settings.SpireOwnershipWeight;
        score += (aiUnits - playerUnits) * settings.UnitWeight;
        
        // 2. EXPANSION METRIC: Evaluate expansion potential (heavily weighted)
        int aiExpansionPotential = CalculateExpansionPotential(aiOwnedSpires, neutrals, aiTeam, getDistance);
        int playerExpansionPotential = CalculateExpansionPotential(playerOwnedSpires, neutrals, playerTeam, getDistance);
        score += (aiExpansionPotential - playerExpansionPotential) * settings.ExpansionWeight;
        
        // 3. TERRITORIAL CONTROL: Distance advantage to neutral/enemy spires
        int aiTerritorialControl = CalculateTerritorialControl(aiOwnedSpires, neutrals, playerOwnedSpires, getDistance);
        int playerTerritorialControl = CalculateTerritorialControl(playerOwnedSpires, neutrals, aiOwnedSpires, getDistance);
        score += (aiTerritorialControl - playerTerritorialControl) * settings.TerritorialControlWeight;
        
        // 4. POSITIONAL ADVANTAGE: Reward central positions and defensive clusters
        int aiPositional = CalculatePositionalValue(aiOwnedSpires, state.Spires, getDistance);
        int playerPositional = CalculatePositionalValue(playerOwnedSpires, state.Spires, getDistance);
        score += (aiPositional - playerPositional) * settings.PositionalWeight;
        
        return score;
    }

    /// <summary>
    /// Calculate expansion potential: how many spires can be captured with current forces
    /// </summary>
    private static int CalculateExpansionPotential(List<AIState.SpireSnapshot> ownedSpires, 
        List<AIState.SpireSnapshot> targets, int team, Func<SpireConstruct, SpireConstruct, int> getDistance)
    {
        int potential = 0;
        
        foreach (var target in targets)
        {
            int targetDefense = target.Reserve; // Only Reserve is updated during simulation
            int bestAttackValue = 0;
            
            // Find the best owned spire that could attack this target
            foreach (var owned in ownedSpires)
            {
                int availableForces = owned.Reserve; // Units available for sending
                int distance = getDistance(owned.LiveRef, target.LiveRef);
                int arriving = availableForces - distance;
                
                if (arriving > targetDefense)
                {
                    // Can capture this target
                    int surplus = arriving - targetDefense;
                    int attackValue = 100 + surplus; // Base value + extra units
                    if (attackValue > bestAttackValue)
                        bestAttackValue = attackValue;
                }
                else if (arriving > 0)
                {
                    // Can weaken but not capture
                    int attackValue = (arriving * 50) / Mathf.Max(1, targetDefense);
                    if (attackValue > bestAttackValue)
                        bestAttackValue = attackValue;
                }
            }
            
            potential += bestAttackValue;
        }
        
        return potential;
    }

    /// <summary>
    /// Calculate territorial control: average distance advantage to key spires
    /// </summary>
    private static int CalculateTerritorialControl(List<AIState.SpireSnapshot> ownedSpires,
        List<AIState.SpireSnapshot> neutrals, List<AIState.SpireSnapshot> enemySpires,
        Func<SpireConstruct, SpireConstruct, int> getDistance)
    {
        if (ownedSpires.Count == 0) return 0;
        
        int control = 0;
        var allTargets = new List<AIState.SpireSnapshot>();
        allTargets.AddRange(neutrals);
        allTargets.AddRange(enemySpires);
        
        foreach (var target in allTargets)
        {
            int minOwnedDist = int.MaxValue;
            int minEnemyDist = int.MaxValue;
            
            // Find closest owned spire to target
            foreach (var owned in ownedSpires)
            {
                int dist = getDistance(owned.LiveRef, target.LiveRef);
                if (dist < minOwnedDist)
                    minOwnedDist = dist;
            }
            
            // Find closest enemy spire to target
            foreach (var enemy in enemySpires)
            {
                int dist = getDistance(enemy.LiveRef, target.LiveRef);
                if (dist < minEnemyDist)
                    minEnemyDist = dist;
            }
            
            // Positive if we're closer, negative if enemy is closer
            if (minOwnedDist < int.MaxValue)
            {
                int advantage = (minEnemyDist == int.MaxValue) ? 10 : (minEnemyDist - minOwnedDist);
                control += advantage;
            }
        }
        
        return control;
    }

    /// <summary>
    /// Calculate positional value: centrality and clustering for defense
    /// </summary>
    private static int CalculatePositionalValue(List<AIState.SpireSnapshot> ownedSpires,
        List<AIState.SpireSnapshot> allSpires, Func<SpireConstruct, SpireConstruct, int> getDistance)
    {
        if (ownedSpires.Count == 0) return 0;
        
        int positional = 0;
        
        // Reward spires that are centrally located (close to many other spires)
        foreach (var owned in ownedSpires)
        {
            int totalDist = 0;
            int count = 0;
            
            foreach (var other in allSpires)
            {
                if (owned.LiveRef == other.LiveRef) continue;
                totalDist += getDistance(owned.LiveRef, other.LiveRef);
                count++;
            }
            
            if (count > 0)
            {
                int avgDist = totalDist / count;
                // Lower average distance = more central = better
                positional += Mathf.Max(0, 20 - avgDist);
            }
        }
        
        // Reward defensive clusters (owned spires close to each other)
        for (int i = 0; i < ownedSpires.Count; i++)
        {
            for (int j = i + 1; j < ownedSpires.Count; j++)
            {
                int dist = getDistance(ownedSpires[i].LiveRef, ownedSpires[j].LiveRef);
                if (dist <= 3) // Close spires can support each other
                {
                    positional += (4 - dist) * 5; // Closer = better
                }
            }
        }
        
        return positional;
    }
}