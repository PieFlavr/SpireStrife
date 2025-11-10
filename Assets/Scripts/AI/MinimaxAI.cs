using System;
using System.Collections.Generic;
using System.Linq;
// using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Simple Minimax-based AI that plans one action per AI turn to capture neutral spires.
/// Objective: maximize AI neutral captures while anticipating one best player counter-move.
///
/// Constraints and simplifications:
/// - Considers a single AI action per turn (one source->neutral capture attempt).
/// - Ignores movement attrition; assumes units arrive intact for claim resolution.
/// - Uses straight-line cell path via HexGrid.GetCellsAlongLine for execution.
/// - Relies on SpireConstruct.CommandUnits to create/queue movement and TurnManager to resolve.
/// </summary>
public class MinimaxAI : MonoBehaviour
{
    public static MinimaxAI inst;
    public bool IsBusy { get; private set; } = false;

    // Fixed-send and flat travel loss constants per constraints
    private static int SEND_CAP = 10;

    [Header("AI Settings")]
    [Tooltip("Maximum search depth in plies (AI move + Player move = 5 plies). Currently supports 1 to 5.")]
    [Range(1, 5)] public int searchDepth = 2;

    

    [Header("Generation (like UiMgr)")]
    [Tooltip("If true, AI will auto-generate units at the chosen source spire before sending, to meet the needed send count in the same turn.")]
    public bool autoGenerateOnCommand = true;
    [Tooltip("Units to generate when issuing a command if autoGenerateOnCommand is true. If 0, will generate exactly what is needed.")]
    public int generateCountPerCommand = 10;

    [Header("Minimax Targeting & Costs")]
    [Tooltip("If true, consider attacking player-owned spires in addition to neutral spires.")]
    public bool considerPlayerTargets = true;

    [Tooltip("If true, consider reinforcing our own spires as valid targets.")]
    public bool considerReinforcementTargets = true;

    [Header("Reinforcement Heuristic")]
    [Tooltip("Extra weight to prioritize increasing remaining garrison (reserve) on owned spires.")]
    [Min(0)] public int reserveWeight = 3;

    [Tooltip("Only consider reinforcing own spires whose remaining reserve is below this threshold. Set 0 to allow all.")]
    [Min(0)] public int reinforceReserveThreshold = 25;

    [Tooltip("In simulation, fraction of arriving reinforcements that convert into reserve (remainingGarrison) at the destination spire.")]
    [Range(0f, 1f)] public float reinforcementToReserveRatio = 1f;

    [Header("Scoring Weights")]
    [Tooltip("Weight for each AI-owned spire vs a player-owned spire. Higher pushes the AI to prioritize captures.")]
    [Min(1)] public int spireOwnershipWeight = 100;

    [Tooltip("Weight per point of AI claim progress advantage on non-owned spires.")]
    [Min(0)] public int claimProgressWeight = 5;

    [Tooltip("Weight per 10 units of leftover AI units (garrison + reserve) to slightly prefer efficient captures.")]
    [Min(0)] public int leftoverUnitsWeight = 1;

    [Header("Search Controls")]
    [Tooltip("Millisecond budget per AI turn for search. Uses iterative deepening and returns best-so-far on timeout.")]
    [Range(5, 200)] public int aiMsBudgetPerTurn = 5000;

    [Tooltip("Enable iterative deepening (always recommended with time budgets).")]
    public bool useIterativeDeepening = true;

    [Tooltip("Per source: number of nearest targets to keep before search.")]
    [Range(1, 6)] public int perSourceTopK = 3;

    [Tooltip("Global cap on ordered moves searched per ply.")]
    [Range(4, 40)] public int globalMoveCap = 20;

    // Cached references and utilities
    private HexGrid cachedGrid;

    // Distance cache (instanceID pair -> length)
    private readonly Dictionary<(int fromId, int toId), int> distanceCache = new Dictionary<(int, int), int>();
    private readonly Dictionary<(int fromId, int toId), int> lastMoveTurn = new Dictionary<(int, int), int>();
    private int aiTurnCounter = 0;

    private void Awake()
    {
        inst = this;
        cachedGrid = FindObjectOfType<HexGrid>();
    }
    
    /// <summary>
    /// Entry point invoked by TurnManager during AiPlanning.
    /// Chooses and issues at most one command for AI this turn.
    /// </summary>
    public void PlanAndQueueAIMoves()
    {
        if (IsBusy)
        {
            Debug.LogWarning("[MinimaxAI] PlanAndQueueAIMoves called while already busy!");
            return;
        }
        
        if (TurnManager.inst == null)
        {
            Debug.LogError("[MinimaxAI] Cannot plan - TurnManager.inst is null!");
            return;
        }
        
        if (SEND_CAP <= 0)
        {
            Debug.LogError($"[MinimaxAI] Cannot plan - SEND_CAP is {SEND_CAP}!");
            return;
        }

        StartCoroutine(PlanAndQueueAIMovesMinimaxCoroutine());
    }

    private System.Collections.IEnumerator PlanAndQueueAIMovesMinimaxCoroutine()
    {
        IsBusy = true;
        var grid = cachedGrid != null ? cachedGrid : FindObjectOfType<HexGrid>();
        if (grid == null) { IsBusy = false; yield break; }

        var allSpires = FindObjectsOfType<SpireConstruct>();
        if (allSpires == null || allSpires.Length == 0) { IsBusy = false; yield break; }

        int aiTeam = TurnManager.inst != null ? TurnManager.inst.aiTeamId : 1;
        int playerTeam = TurnManager.inst != null ? TurnManager.inst.playerTeamId : 0;

    // New AI turn
        aiTurnCounter++;

        var baseState = Snapshot(allSpires);

    // Single-pass alpha-beta search for best move from root
    AIAction best = SearchBest(baseState, aiTeam, playerTeam);

        if (best != null)
        {
            // Remember chosen move for tabu
            RememberMove(best, aiTurnCounter);
            bool finished = false;
            ExecuteWithPathfinder(best, () => { finished = true; });
            // Short settle wait with guard to avoid infinite stall
            int guard = 0; int guardMax = 240;
            while (!finished && guard < guardMax) { guard++; yield return null; }
        }
        IsBusy = false;
        yield break;
    }

    private List<AIAction> BuildCandidateActions(SimState state, int team, HexGrid grid)
    {
        var list = new List<AIAction>();

        int enemy = (TurnManager.inst != null && team == TurnManager.inst.aiTeamId)
            ? TurnManager.inst.playerTeamId : (TurnManager.inst?.aiTeamId ?? 1);

        foreach (var from in state.Spires.Where(s => s.team == team && s.LiveRef != null))
        {
            int available = from.garrison + (autoGenerateOnCommand ? from.reserve : 0);
            int send = Math.Min(SEND_CAP, Math.Max(0, available));
            
            var potentialTargets = state.Spires.Where(s => s.LiveRef != null && s != from);
            if (!considerReinforcementTargets) potentialTargets = potentialTargets.Where(s => s.team != team);
            if (!considerPlayerTargets) potentialTargets = potentialTargets.Where(s => s.team == (int)SpireConstruct.OwnerType.Neutral || s.team == team);

            var targetActions = new List<AIAction>();
            foreach (var to in potentialTargets)
            {
                int estimatedDistance = GetCachedDistance(from.LiveRef, to.LiveRef);
                int arriving = Math.Max(0, send - estimatedDistance);
                if (arriving <= 0) continue; // cannot land anything

                // Skip illegal same-team if reinforcement disabled
                if (to.team == team)
                {
                    if (!considerReinforcementTargets) continue;
                    bool low = reinforceReserveThreshold > 0 && to.reserve < reinforceReserveThreshold;
                    bool threatened = IsThreatened(state, to.LiveRef, team, enemy);
                    if (!low && !threatened) continue; // don't reinforce unnecessarily
                }

                var act = new AIAction
                {
                    From = from.LiveRef,
                    To = to.LiveRef,
                    SendCount = send,
                    EstimatedDistance = GetCachedDistance(from.LiveRef, to.LiveRef)
                };
                act.Heuristic = MoveHeuristic(state, act, team, enemy);
                targetActions.Add(act);
            }

            // Per source: sort by heuristic descending and take top K
            targetActions = targetActions.OrderByDescending(a => a.Heuristic).Take(perSourceTopK).ToList();
            list.AddRange(targetActions);
        }

        // No need for additional global ordering here; will be ordered in OrderedMoves
        return list;
    }

    private int MoveHeuristic(SimState state, AIAction action, int team, int enemy)
    {
        if (action == null || action.To == null || action.From == null) return int.MinValue;

        var to = state.Spires.FirstOrDefault(x => x.LiveRef == action.To);
        if (to == null) return int.MinValue;
        var from = state.Spires.FirstOrDefault(x => x.LiveRef == action.From);
        if (from == null) return int.MinValue;

        int dist = action.EstimatedDistance;
        int heuristic = 0;

        if (to.team == team)
        {
            // Reinforcement heuristic: prefer low reserve, threatened, closer
            heuristic = 1000 - to.reserve;
            if (IsThreatened(state, action.To, team, enemy)) heuristic += 500;
            heuristic -= dist * 2; // Penalize distance more for reinforcements
            // Penalize draining the source spire
            heuristic -= from.reserve < action.SendCount ? 200 : 0;
        }
        else
        {
            // Capture heuristic: prefer low needed, finishers, blockers, closer
            int cur = team == TurnManager.inst?.aiTeamId ? to.claimAi : to.claimPlayer;
            int needed = Math.Max(0, to.cost - cur);
            heuristic = 1000 - needed;
            int steps = Mathf.Max(0, action.EstimatedDistance);
            int arriving = Math.Max(0, action.SendCount - steps);
            if (needed > 0 && arriving >= needed) heuristic += 10000; // Finisher bonus
            if (BlocksEnemyFinisher(state, action, team)) heuristic += 5000; // Blocker bonus
            heuristic -= steps; // Prefer closer targets

            // Prefer attacking targets with low reserves and leaving source with high reserves
            heuristic -= to.reserve;
            heuristic -= from.reserve < action.SendCount ? 100 : 0;
        }

        return heuristic;
    }

    private bool IsFinisher(SimState s, AIAction m, int moverTeam)
    {
        if (m == null || m.To == null || m.From == null) return false;
        var to = s.Spires.FirstOrDefault(x => x.LiveRef == m.To);
        if (to == null || to.team == moverTeam) return false;
        int cur = moverTeam == (TurnManager.inst?.aiTeamId ?? 1) ? to.claimAi : to.claimPlayer;
        int needed = Math.Max(0, to.cost - cur);
        var from = s.Spires.FirstOrDefault(x => x.LiveRef == m.From);
        if (from == null) return false;
        int steps = GetCachedDistance(from.LiveRef, to.LiveRef);
        int arriving = Math.Max(0, Math.Min(SEND_CAP,
            from.garrison + (autoGenerateOnCommand ? from.reserve : 0)) - steps);
        return arriving >= needed && needed > 0;
    }

    private bool BlocksEnemyFinisher(SimState s, AIAction m, int moverTeam)
    {
        if (m == null || m.To == null) return false;
        int enemy = (TurnManager.inst != null && moverTeam == TurnManager.inst.aiTeamId)
            ? TurnManager.inst.playerTeamId : (TurnManager.inst?.aiTeamId ?? 1);

        // If enemy could finish this spire next ply, count as a block
        var to = s.Spires.FirstOrDefault(x => x.LiveRef == m.To);
        if (to == null) return false;
        int enemyCur = enemy == (TurnManager.inst?.aiTeamId ?? 1) ? to.claimAi : to.claimPlayer;
        int needed = Math.Max(0, to.cost - enemyCur);
        int steps = GetCachedDistance(m.From, m.To);
        int enemyArriving = Math.Max(0, Math.Min(SEND_CAP, EnemyAvailableAt(s, to, enemy)) - steps);
        return enemyArriving >= needed && needed > 0 && to.team != moverTeam;
    }

    private int EnemyAvailableAt(SimState s, SpireSnapshot target, int enemy)
    {
        // crude: pick enemy source with max available; still O(N)
        int best = 0;
        foreach (var e in s.Spires.Where(x => x.team == enemy))
            best = Math.Max(best, e.garrison + (autoGenerateOnCommand ? e.reserve : 0));
        return best;
    }

    private void ExecuteWithPathfinder(AIAction action, System.Action onDone = null)
    {
        if (action == null || UiMgr.inst == null) { onDone?.Invoke(); return; }
        if (action.From == null || action.To == null) { onDone?.Invoke(); return; }
        if (action.From.parentCell == null || action.To.parentCell == null) { onDone?.Invoke(); return; }

        // Sync UiMgr generation behavior with AI settings
        UiMgr.inst.alwaysGenerateOnCommand = autoGenerateOnCommand;

        // Set UiMgr context and trigger its async path request -> OnPathFound -> CommandUnits
        UiMgr.inst.targetSpire = action.To;
        UiMgr.inst.selectedSpire = action.From;

        // Start a short watcher that completes once AI units are queued (or after a timeout)
        StartCoroutine(WaitForAiQueueThenCallback(onDone));

        // Kick off selection + pathfinding
        UiMgr.inst.SelectCell(action.From.parentCell);
        UiMgr.inst.SelectTargetCell(action.To.parentCell);
    }

    private System.Collections.IEnumerator WaitForAiQueueThenCallback(System.Action onDone)
    {
        int aiTeam = TurnManager.inst != null ? TurnManager.inst.aiTeamId : 1;
        int guard = 0;
        // Wait until at least one AI unit is in Traversing state (queued) or timeout
        while (guard < 180)
        {
            var anyQueued = FindObjectsOfType<Units>()
                .Any(u => u != null && u.teamID == aiTeam && u.state == Units.UnitState.Traversing);
            if (anyQueued) break;
            guard++;
            yield return null;
        }
        onDone?.Invoke();
    }

    // =====================
    // Minimax simulation
    // =====================

    private class SpireSnapshot
    {
        public int team;
        public int garrison;
        public int reserve;
        public int cost;
        public int claimAi;
        public int claimPlayer;
        public Vector2Int axial;
        public SpireConstruct LiveRef; // for mapping back to scene if needed
    }

    private class SimState
    {
        public List<SpireSnapshot> Spires = new List<SpireSnapshot>();
    }

    private class AIAction
    {
        public SpireConstruct From;
        public SpireConstruct To;
        public int SendCount;
        public int EstimatedDistance;
        public int Heuristic; // used for move ordering only
    }

    private SimState Snapshot(SpireConstruct[] spires)
    {
        int aiTeam = TurnManager.inst != null ? TurnManager.inst.aiTeamId : 1;
        int playerTeam = TurnManager.inst != null ? TurnManager.inst.playerTeamId : 0;

        var st = new SimState();
        foreach (var s in spires)
        {
            if (s == null) continue;
            int aiClaim = 0;
            int playerClaim = 0;
            var prog = s.GetClaimProgress();
            if (prog != null)
            {
                prog.TryGetValue(aiTeam, out aiClaim);
                prog.TryGetValue(playerTeam, out playerClaim);
            }
            st.Spires.Add(new SpireSnapshot
            {
                team = s.teamID,
                garrison = s.GetTotalGarrisonCount(),
                reserve = s.remainingGarrison,
                cost = s.costToClaim,
                claimAi = aiClaim,
                claimPlayer = playerClaim,
                axial = s.parentCell != null ? s.parentCell.axial_coords : Vector2Int.zero,
                LiveRef = s
            });
        }
        return st;
    }

    private SimState Clone(SimState s)
    {
        var copy = new SimState();
        foreach (var sp in s.Spires)
        {
            copy.Spires.Add(new SpireSnapshot
            {
                team = sp.team,
                garrison = sp.garrison,
                reserve = sp.reserve,
                cost = sp.cost,
                claimAi = sp.claimAi,
                claimPlayer = sp.claimPlayer,
                axial = sp.axial,
                LiveRef = sp.LiveRef
            });
        }
        return copy;
    }

    private SimState SimulateAction(SimState current, AIAction a, int moverTeam, int aiTeam, int playerTeam)
    {
        // Apply flat-loss, fixed-send simulation
        var s = Clone(current);
        var from = s.Spires.FirstOrDefault(x => x.LiveRef == a.From);
        var to   = s.Spires.FirstOrDefault(x => x.LiveRef == a.To);
        if (from == null || to == null) return s;

        // Determine availability
        int available = from.garrison + (autoGenerateOnCommand ? from.reserve : 0);
        if (available <= 0) return s;

        int send = Math.Min(SEND_CAP, available);

        // Consume from reserve first if auto-generate enabled, else from garrison
        int useReserve = 0, useGarrison = 0;
        if (autoGenerateOnCommand)
        {
            useReserve  = Math.Min(from.reserve, send);
            useGarrison = send - useReserve;
            from.reserve  -= useReserve;
            from.garrison -= useGarrison;
        }
        else
        {
            useGarrison = Math.Min(from.garrison, send);
            from.garrison -= useGarrison;
        }

        int steps = GetCachedDistance(a.From, a.To);
        int arriving = Math.Max(0, send - steps);
        if (arriving <= 0) return s;

        if (to.team == moverTeam)
        {
            // Reinforcement
            to.garrison += arriving;
            if (reinforcementToReserveRatio > 0f)
                to.reserve += Mathf.FloorToInt(arriving * reinforcementToReserveRatio);
            return s;
        }

        // Claim application with fixed arriving
        bool moverIsAi = moverTeam == aiTeam;
        int cur = moverIsAi ? to.claimAi : to.claimPlayer;
        int needed = Math.Max(0, to.cost - cur);

        if (arriving >= needed && needed > 0)
        {
            // Capture
            to.team = moverTeam;
            to.claimAi = 0; to.claimPlayer = 0;
            int leftover = arriving - needed;
            to.garrison += leftover;
        }
        else
        {
            if (moverIsAi) to.claimAi = cur + arriving;
            else           to.claimPlayer = cur + arriving;
        }
        return s;
    }

    private int Evaluate(SimState state, int aiTeam, int playerTeam)
    {
        // 1) Primary objective: maximize number of AI-owned spires, minimize player-owned
        int aiOwned = 0;
        int playerOwned = 0;
        foreach (var s in state.Spires)
        {
            if (s.team == aiTeam) aiOwned++;
            else if (s.team == playerTeam) playerOwned++;
        }

        int score = spireOwnershipWeight * (aiOwned - playerOwned);

        // 2) Secondary: push toward imminent captures by valuing AI claim progress on non-AI spires,
        //    and slightly penalize opponent claim progress on non-player spires.
        int claimDelta = 0;
        foreach (var s in state.Spires)
        {
            if (s.team != aiTeam)
            {
                // Cap claims by cost just in case
                int aiClaim = Mathf.Clamp(s.claimAi, 0, s.cost);
                int oppClaim = Mathf.Clamp(s.claimPlayer, 0, s.cost);

                // Only value progress on neutral or enemy spires; ignore on our own (always zero anyway)
                claimDelta += (aiClaim - oppClaim);
            }
        }
        score += claimDelta * claimProgressWeight;

        // 3) Tertiary: prefer keeping leftover units (garrison + reserve) after captures, lightly weighted.
        //    Use a coarse scale (per 10 units) to avoid overshadowing capture choices.
        int aiLeftoverUnits = 0;
        int playerLeftoverUnits = 0;
        foreach (var s in state.Spires)
        {
            if (s.team == aiTeam)
            {
                // Clamp to avoid runaway scoring on very large reserves
                aiLeftoverUnits += Mathf.Min(s.garrison, 100);
                aiLeftoverUnits += Mathf.Min(s.reserve, 200);
            }
            else if (s.team == playerTeam)
            {
                playerLeftoverUnits += Mathf.Min(s.garrison, 100);
                playerLeftoverUnits += Mathf.Min(s.reserve, 200);
            }
        }
        score += (aiLeftoverUnits / 10) * leftoverUnitsWeight;
        score -= (playerLeftoverUnits / 10) * leftoverUnitsWeight;

        // Near-term finisher terms with fixed-send
        score += 30 * CountOurFinishersNextPly(state, aiTeam, playerTeam);
        score -= 30 * CountEnemyFinishersNextPly(state, aiTeam, playerTeam);

        // Border reserve coverage bonus
        score += reserveWeight * CountCoveredFrontier(state, aiTeam, playerTeam);

        return score;
    }

    // =====================
    // Alpha-Beta + Iterative Deepening + Quiescence
    // =====================

    private AIAction SearchBest(SimState root, int aiTeam, int playerTeam)
    {
        AIAction bestMove = null;
        int bestScore = int.MinValue + 1;

        int depth = Mathf.Max(1, searchDepth);
        int alpha = int.MinValue + 1;
        int beta = int.MaxValue - 1;

        var moves = OrderedMoves(root, aiTeam, playerTeam);
        
        Debug.Log($"[MinimaxAI] SearchBest: {moves.Count} candidate moves before tabu filter");
        
        // Root-level tabu filter
        moves = moves.Where(m => !IsTabu(m, aiTurnCounter) || IsFinisher(root, m, aiTeam)).ToList();
        
        Debug.Log($"[MinimaxAI] SearchBest: {moves.Count} candidate moves after tabu filter");

        if (moves.Count == 0)
        {
            Debug.LogWarning("[MinimaxAI] SearchBest: No valid moves available!");
            return null;
        }

        foreach (var m in moves)
        {
            var s1 = SimulateAction(root, m, aiTeam, aiTeam, playerTeam);
            int sc = -Negamax(s1, depth - 1, -beta, -alpha, playerTeam, aiTeam, playerTeam);
            
            Debug.Log($"[MinimaxAI] Evaluating {m.From?.name} -> {m.To?.name}: score={sc}");
            
            if (sc > bestScore) 
            { 
                bestScore = sc; 
                bestMove = m; 
                Debug.Log($"[MinimaxAI] New best move: score={bestScore}");
            }
            if (sc > alpha) alpha = sc;
        }
        
        if (bestMove == null)
        {
            Debug.LogWarning("[MinimaxAI] SearchBest: All moves evaluated but none selected!");
        }
        
        return bestMove;
    }

    private bool IsTabu(AIAction m, int currentTurn)
    {
        if (m == null || m.From == null || m.To == null) return false;
        var key = (m.From.GetInstanceID(), m.To.GetInstanceID());
        if (lastMoveTurn.TryGetValue(key, out var last))
        {
            // Tabu if issued within last 2 turns
            return currentTurn - last <= 2;
        }
        return false;
    }

    private void RememberMove(AIAction m, int currentTurn)
    {
        if (m == null || m.From == null || m.To == null) return;
        var key = (m.From.GetInstanceID(), m.To.GetInstanceID());
        lastMoveTurn[key] = currentTurn;
    }

    private int Negamax(SimState s, int depth, int alpha, int beta, int side, int aiTeam, int playerTeam)
    {
        if (depth <= 0) return Evaluate(s, aiTeam, playerTeam);

        int best = int.MinValue + 1;
        var moves = OrderedMoves(s, side, side == aiTeam ? playerTeam : aiTeam);
        foreach (var m in moves)
        {
            var s2 = SimulateAction(s, m, side, aiTeam, playerTeam);
            int val = -Negamax(s2, depth - 1, -beta, -alpha, (side == aiTeam) ? playerTeam : aiTeam, aiTeam, playerTeam);
            if (val > best) best = val;
            if (val > alpha) alpha = val;
            if (alpha >= beta) break; // prune
        }
        return best;
    }

    private List<AIAction> OrderedMoves(SimState s, int side, int opp)
    {
        var grid = cachedGrid != null ? cachedGrid : FindObjectOfType<HexGrid>();
        var moves = BuildCandidateActions(s, side, grid);
        // Order by heuristic descending and cap globally
        moves = moves.OrderByDescending(a => a.Heuristic).Take(globalMoveCap).ToList();
        return moves;
    }

    private bool IsThreatened(SimState s, SpireConstruct targetLive, int side, int opp)
    {
        if (targetLive == null) return false;
        var snap = s.Spires.FirstOrDefault(x => x.LiveRef == targetLive);
        if (snap == null) return false;
        int neededByOpp = NeededToCapture(snap, opp);
        if (neededByOpp <= 0) return false;
        int oppArriving = MaxArrivingFromSideTo(s, snap, opp);
        return oppArriving >= neededByOpp;
    }


    private int NeededToCapture(SpireSnapshot to, int side)
    {
        if (to.team == side) return 0;
        int cur = side == (TurnManager.inst?.aiTeamId ?? 1) ? to.claimAi : to.claimPlayer;
        return Mathf.Max(0, to.cost - cur);
    }

    private int MaxArrivingFromSideTo(SimState s, SpireSnapshot target, int side)
    {
        // With fixed-send and flat loss, target distance is irrelevant; compute best arrival capacity for side
        int best = 0;
        foreach (var src in s.Spires)
        {
            if (src.team != side) continue;
            int available = src.garrison + (autoGenerateOnCommand ? src.reserve : 0);
            // Use a conservative step estimate when simulating: at least 1 step if different cells
            int steps = (src.axial != target.axial) ? Mathf.Max(1, GetCachedDistance(src.LiveRef, target.LiveRef)) : 0;
            int arriving = Math.Max(0, Math.Min(SEND_CAP, Math.Max(0, available)) - steps);
            if (arriving > best) best = arriving;
        }
        return best;
    }

    private int CountEnemyFinishersNextPly(SimState s, int aiTeam, int playerTeam)
    {
        int count = 0;
        foreach (var sp in s.Spires)
        {
            if (sp.team == playerTeam) continue; // already enemy-owned
            int needed = NeededToCapture(sp, playerTeam);
            if (needed <= 0) continue;
            int arriving = MaxArrivingFromSideTo(s, sp, playerTeam);
            if (arriving >= needed) count++;
        }
        return count;
    }

    private int CountOurFinishersNextPly(SimState s, int aiTeam, int playerTeam)
    {
        int count = 0;
        foreach (var sp in s.Spires)
        {
            if (sp.team == aiTeam) continue;
            int needed = NeededToCapture(sp, aiTeam);
            if (needed <= 0) continue;
            int arriving = MaxArrivingFromSideTo(s, sp, aiTeam);
            if (arriving >= needed) count++;
        }
        return count;
    }

    private int CountCoveredFrontier(SimState s, int aiTeam, int playerTeam)
    {
        // Frontier = AI spire with neighbor that is neutral or enemy; reward if reserve > 0
        var byAxial = s.Spires.ToDictionary(x => x.axial, x => x);
        Vector2Int[] dirs = new[]
        {
            new Vector2Int(1,0), new Vector2Int(1,-1), new Vector2Int(0,-1),
            new Vector2Int(-1,0), new Vector2Int(-1,1), new Vector2Int(0,1)
        };
        int covered = 0;
        foreach (var sp in s.Spires)
        {
            if (sp.team != aiTeam) continue;
            bool frontier = false;
            foreach (var d in dirs)
            {
                var ax = sp.axial + d;
                if (byAxial.TryGetValue(ax, out var nb))
                {
                    if (nb.team != aiTeam) { frontier = true; break; }
                }
            }
            if (frontier && sp.reserve > 0) covered++;
        }
        return covered;
    }


    private int GetCachedDistance(SpireConstruct a, SpireConstruct b)
    {
        if (a == null || b == null || a.parentCell == null || b.parentCell == null) return 0;
        var key = (a.GetInstanceID(), b.GetInstanceID());
        if (distanceCache.TryGetValue(key, out var d)) return d;
        var grid = cachedGrid != null ? cachedGrid : FindObjectOfType<HexGrid>();
        int dist = grid != null ? grid.GetDistance(a.parentCell, b.parentCell) : 0;
        distanceCache[key] = dist;
        return dist;
    }
    void Start()
    {
        if (UiMgr.inst != null && UiMgr.inst.generateCountPerCommand > 0)
        {
            SEND_CAP = UiMgr.inst.generateCountPerCommand;
        }
        else
        {
            SEND_CAP = 10; // Safe fallback
            UnityEngine.Debug.LogWarning("[MinimaxAI] UiMgr not found or generateCountPerCommand is 0, using default SEND_CAP=10");
        }
        
        UnityEngine.Debug.Log($"[MinimaxAI] Initialized with SEND_CAP={SEND_CAP}");
    }
}