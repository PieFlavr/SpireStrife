using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager inst;
    public int playerTeamId = 0;
    public int aiTeamId = 1;

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

    private void StartPlayerPlanning()
    {
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
        CurrentPhase = Phase.PlayerResolving;

        // Resolve conflicts within player moves if needed (rare in single-team queue)
        ResolveConflicts(queuedPlayer);

        yield return ResolveTeamAsync(queuedPlayer);

        CurrentPhase = Phase.AiPlanning; // lock input by property

        // AI planning hook: let MinimaxAI choose and queue one move
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
        // Player has zero active units AND AI spires <= Player spires
        int playerUnits = CountActiveUnits(playerTeamId);
        if (playerUnits > 0) return false;

        int aiSpires = CountSpires(aiTeamId);
        int playerSpires = CountSpires(playerTeamId);
        return aiSpires <= playerSpires;
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
    }
}
