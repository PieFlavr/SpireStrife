// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents a Spire construct on the hex grid.
/// Spires can be claimed by factions by committing units; claiming consumes units to increase a faction's claim progress.
/// Spires keep track of stationed ally units and can issue one command per turn.
/// </summary>
public class SpireConstruct : GridObject
{
    [Header("Spire Configuration")]
    [Tooltip("Number of claim points required for a faction to capture this Spire.")]
    public int costToClaim = 10;

    /// <summary>
    /// Tracks accumulated claim progress per team (teamID -> progress).
    /// </summary>
    private readonly Dictionary<int, int> claimProgress = new Dictionary<int, int>();

    /// <summary>
    /// Unit groups currently garrisoned at this Spire.
    /// Kept reasonably in sync by RegisterGarrison / RefreshGarrisonFromCell.
    /// Only Stationed units are considered garrison.
    /// </summary>
    private readonly List<Units> garrisonedUnits = new List<Units>();

    /// <summary>
    /// Whether this Spire has issued a command this turn (one command per turn).
    /// </summary>
    private bool hasCommandedThisTurn = false;

    protected override void SetObjectType()
    {
        objectType = GridObjectType.Construct;
    }

    /// <summary>
    /// Called when this spire is placed on a HexCell.
    /// Refreshes the local garrison list from the cell contents.
    /// </summary>
    public override void OnPlacedOnGrid(HexCell cell)
    {
        base.OnPlacedOnGrid(cell);
        RefreshGarrisonFromCell();
    }

    /// <summary>
    /// Called when the spire is removed from the grid.
    /// Clears internal lists.
    /// </summary>
    public override void OnRemovedFromGrid()
    {
        base.OnRemovedFromGrid();
        garrisonedUnits.Clear();
        claimProgress.Clear();
    }

    /// <summary>
    /// Registers allied/owned Units on this cell. 
    /// Use when the spire is created or when external code needs to resync.
    /// </summary>
    public void RefreshGarrisonFromCell()
    {
        garrisonedUnits.Clear();
        if (parentCell == null) return;

        var unitsHere = parentCell.GetObjectsByType(GridObjectType.Unit)
            .OfType<Units>();

        foreach (var u in unitsHere)
        {
            // Prefer explicit owner link; fallback to same-team units on the cell
            if (u.owner == this || u.teamID == teamID)
            {
                RegisterGarrison(u);
            }
        }
    }

    /// <summary>
    /// Register a Units group as garrisoned here.
    /// Makes the unit group owned by this spire and sets state to Stationed.
    /// </summary>
    /// <param name="u">Units to register</param>
    public void RegisterGarrison(Units u)
    {
        if (u == null) return;
        if (!garrisonedUnits.Contains(u)) garrisonedUnits.Add(u);
        u.AttachToOwner(this);
    }

    /// <summary>
    /// Remove a Units group from this spire's garrison list.
    /// Does not destroy the unit group.
    /// </summary>
    /// <param name="u">Units to remove</param>
    public void UnregisterGarrison(Units u)
    {
        if (u == null) return;
        garrisonedUnits.Remove(u);
        if (u.owner == this) u.DetachFromOwner();
    }

    /// <summary>
    /// Returns the total number of stationed units currently garrisoned at this Spire.
    /// Counts only Stationed groups registered in garrisonedUnits.
    /// </summary>
    public int GetTotalGarrisonCount()
    {
        return garrisonedUnits.Where(u => u.state == Units.UnitState.Stationed).Sum(u => u.unitCount);
    }

    /// <summary>
    /// Issue a command to send units from this spire to a target spire along a provided path.
    /// The caller must provide a valid path to the destination (path[0] should be this spire's cell).
    /// </summary>
    /// <param name="sendCount">How many units to send</param>
    /// <param name="path">List of HexCells forming the route (including source and destination)</param>
    /// <param name="target">Target SpireConstruct</param>
    /// <returns>True if the command was accepted</returns>
    public bool CommandUnits(int sendCount, List<HexCell> path, SpireConstruct target)
    {
        if (hasCommandedThisTurn)
        {
            Debug.LogWarning("Spire has already commanded units this turn");
            return false;
        }

        if (sendCount <= 0)
        {
            Debug.LogWarning("sendCount must be positive");
            return false;
        }

        if (parentCell == null)
        {
            Debug.LogError("Spire must be placed on a cell before issuing commands.");
            return false;
        }

        int available = GetTotalGarrisonCount();
        if (available < sendCount)
        {
            Debug.LogWarning("Not enough units in garrison");
            return false;
        }

        // Deduct units from local garrison groups (consume counts; destroy empty groups)
        int remainingToRemove = sendCount;
        for (int i = garrisonedUnits.Count - 1; i >= 0 && remainingToRemove > 0; i--)
        {
            Units g = garrisonedUnits[i];
            if (g == null || g.state != Units.UnitState.Stationed) continue;

            int take = Mathf.Min(g.unitCount, remainingToRemove);
            g.unitCount -= take;
            remainingToRemove -= take;

            if (g.unitCount <= 0)
            {
                // remove empty group
                g.OnRemovedFromGrid();
                Destroy(g.gameObject);
                garrisonedUnits.RemoveAt(i);
            }
        }

        // Create new Units group representing the dispatched force
        GameObject unitObj = new GameObject($"Units_{teamID}");
        Units dispatched = unitObj.AddComponent<Units>();
        dispatched.unitCount = sendCount;
        dispatched.teamID = this.teamID;

        // Place on this spire's cell
        if (!parentCell.TryAddGridObject(dispatched))
        {
            Debug.LogError("Failed to place dispatched units on spire cell");
            Destroy(dispatched.gameObject);
            return false;
        }

        // Keep owner as source while traversing
        dispatched.AttachToOwner(this);

        // Queue movement to target
        dispatched.QueueMovement(path, target);

        hasCommandedThisTurn = true;
        return true;
    }

    /// <summary>
    /// Called when an arriving Units group reaches this spire.
    /// This method implements reinforcement (same-team) and claim logic (opposing team).
    /// Units used to advance a claim are destroyed. If claim completes, ownership switches to the claimant and any leftover arriving units become garrisoned.
    /// </summary>
    /// <param name="arriving">The arriving Units group</param>
    public void OnUnitsArrived(Units arriving)
    {
        if (arriving == null) return;

        // Reinforcement for the owning team
        if (arriving.teamID == teamID)
        {
            RegisterGarrison(arriving);
            return;
        }

        // Opposition — apply claim progress
        int contribution = arriving.unitCount;
        if (contribution <= 0)
        {
            arriving.OnRemovedFromGrid();
            Destroy(arriving.gameObject);
            return;
        }

        int current = claimProgress.ContainsKey(arriving.teamID) ? claimProgress[arriving.teamID] : 0;
        int needed = costToClaim - current;

        if (contribution >= needed)
        {
            // Enough to capture: consume only what is necessary
            int used = needed;
            claimProgress[arriving.teamID] = current + used;
            arriving.unitCount -= used;

            // Claim the spire
            ClaimBy(arriving.teamID);

            // Leftover units (if any) become garrisoned at the new owner
            if (arriving.unitCount > 0)
            {
                RegisterGarrison(arriving);
            }
            else
            {
                arriving.OnRemovedFromGrid();
                Destroy(arriving.gameObject);
            }
        }
        else
        {
            // Not enough to claim yet: consume all arriving units toward progress, destroy group
            claimProgress[arriving.teamID] = current + contribution;
            arriving.OnRemovedFromGrid();
            Destroy(arriving.gameObject);
        }
    }

    /// <summary>
    /// Perform ownership transfer to a new team and reset claim state.
    /// </summary>
    /// <param name="newTeamID">ID of the claiming team</param>
    private void ClaimBy(int newTeamID)
    {
        teamID = newTeamID;
        claimProgress.Clear();
        Debug.Log($"Spire at {GridPosition} claimed by Team {newTeamID}");
        // (Optional) Notify visuals / UI / game manager here.
    }

    /// <summary>
    /// Reset per-turn state; call at start of each new turn.
    /// </summary>
    public void ResetTurn()
    {
        hasCommandedThisTurn = false;
    }

    /// <summary>
    /// Read-only snapshot of claim progress (useful for UI
    /// </summary>
    public IReadOnlyDictionary<int, int> GetClaimProgress()
    {
        return claimProgress; // Shouldn't have weird side effects? Apparently best practice for this stuff.
    }

    /// <summary>
    /// Read-only snapshot of currently garrisoned unit groups (useful for UI).
    /// </summary>
    public IReadOnlyList<Units> GetGarrisonedUnits()
    {
        return garrisonedUnits.AsReadOnly();
    }
}