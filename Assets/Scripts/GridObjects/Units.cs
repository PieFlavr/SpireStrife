﻿// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents a group of units on the hex grid.
/// Units can belong to any GridObject (Spires, other Units, Tiles, etc.).
/// Movement cost is paid in unit count based on tile traversal costs.
/// </summary>
public class Units : GridObject
{
    /// <summary>
    /// How many units currently exist in this unit group.
    /// Decreases/increases as units travel across tiles or engage in combat.
    /// </summary>
    [Header("Unit Properties")]
    [Tooltip("Number of units in this group")]
    public int unitCount = 10;

    /// <summary>
    /// Current state of this unit group.
    /// </summary>
    public enum UnitState
    {
        Stationed,   // Attached to a GridObject (owner is set)
        Traversing,  // Queued movement (owner still references source)
        Destroyed    // Eliminated (unit count = 0)
    }

    /// <summary>
    /// Current state of this unit group.
    /// </summary>
    public UnitState state = UnitState.Stationed;

    /// <summary>
    /// The GridObject this unit group belongs to.
    /// When Stationed: the GridObject at current location
    /// When Traversing: the source GridObject (where units came from)
    /// Null only if this is a truly neutral/unaligned unit group.
    /// </summary>
    [Tooltip("GridObject this unit belongs to")]
    public GridObject owner;

    /// <summary>
    /// The planned path this unit group will follow (if in transit).
    /// Use for visualization and conflict detection.
    /// </summary>
    [HideInInspector]
    public List<HexCell> plannedPath;

    /// <summary>
    /// The destination GridObject (if moving to join/capture/attack).
    /// </summary>
    [HideInInspector]
    public GridObject targetObject;

    protected override void SetObjectType()
    {
        objectType = GridObjectType.Unit;
    }

    /// <summary>
    /// Tries to find an owner GridObject when initialized on a cell.
    /// This default behaviour is mostly for convenience.
    /// </summary>
    protected override void InitializeGridObject()
    {
        base.InitializeGridObject();
    }

    /// <summary>
    /// Attempts to find a suitable owner GridObject on the current cell.
    /// For now, only Spires are considered valid owners.
    /// </summary>
    private void TryFindOwner()
    {
        if (parentCell == null) return;

        // Check for Spires first
        var spire = parentCell.GetObjectsByType(GridObjectType.Construct)
            .FirstOrDefault(obj => obj is SpireConstruct);

        if (spire != null)
        {
            AttachToOwner(spire);
            return;
        }
    }

    /// <summary>
    /// Attaches this unit group to an owner GridObject.
    /// Units count towards the owner's total.
    /// </summary>
    /// <param name="newOwner">The GridObject to belong to</param>
    public void AttachToOwner(GridObject newOwner)
    {
        if (newOwner == this) return; // Can't own yourself

        // Log detachment if changing owners
        if (owner != null && owner != newOwner)
        {
            Debug.Log($"{unitCount} units detached from {owner.GetType().Name}");
        }

        owner = newOwner;
        state = UnitState.Stationed;

        Debug.Log($"{unitCount} units (Team {teamID}) attached to {newOwner.GetType().Name} at {newOwner.GridPosition}");
    }

    /// <summary>
    /// Detaches this unit group from its current owner.
    /// Makes this a truly neutral unit group.
    /// </summary>
    public void DetachFromOwner()
    {
        if (owner != null)
        {
            Debug.Log($"{unitCount} units detached from {owner.GetType().Name}");
            owner = null;
        }
    }

    /// <summary>
    /// Calculates how many units will arrive at the destination after traversing a path.
    /// First cell in path is the starting position (no cost).
    /// Units are lost based on tile traversal costs along the path.
    /// Does not account for combat.
    /// </summary>
    /// <param name="path">Complete path from source to destination</param>
    /// <param name="startingCount">Initial unit count</param>
    /// <returns>Number of units remaining at destination, or -1 if path is invalid</returns>
    public static int CalculateRemainingUnits(List<HexCell> path, int startingCount)
    {
        if (path == null || path.Count <= 1)
            return startingCount;

        int remainingUnits = startingCount;

        // Skip first cell (starting position), traverse the rest
        for (int i = 1; i < path.Count; i++)
        {
            Tile tile = path[i].GetObjectsByType(GridObjectType.Tile)
                .FirstOrDefault() as Tile;

            if (tile == null || !tile.CanEnter())
                return -1; // Invalid path

            // Units lost = ceil(traversal cost)
            int unitsLost = Mathf.CeilToInt(tile.GetMovementCost());
            remainingUnits -= unitsLost;

            if (remainingUnits <= 0)
                return 0; // All units eliminated during travel
        }

        return remainingUnits;
    }

    /// <summary>
    /// Calculates the unit count at each step along a path for visualization.
    /// Use to show "queued action" with unit counts displayed on tiles.
    /// </summary>
    /// <param name="path">Complete path from source to destination</param>
    /// <param name="startingCount">Initial unit count</param>
    /// <returns>Dictionary mapping each cell to the unit count when arriving there</returns>
    public static Dictionary<HexCell, int> GetUnitCountAlongPath(List<HexCell> path, int startingCount)
    {
        Dictionary<HexCell, int> unitCounts = new Dictionary<HexCell, int>();

        if (path == null || path.Count == 0)
            return unitCounts;

        int currentCount = startingCount;
        unitCounts[path[0]] = currentCount; // Starting position

        for (int i = 1; i < path.Count; i++)
        {
            Tile tile = path[i].GetObjectsByType(GridObjectType.Tile)
                .FirstOrDefault() as Tile;

            if (tile == null || !tile.CanEnter())
                break;

            int unitsLost = Mathf.CeilToInt(tile.GetMovementCost());
            currentCount -= unitsLost;

            if (currentCount <= 0)
            {
                unitCounts[path[i]] = 0;
                break;
            }

            unitCounts[path[i]] = currentCount;
        }

        return unitCounts;
    }

    /// <summary>
    /// Checks if this unit group's path conflicts with another unit group.
    /// Used for combat resolution during simultaneous movement.
    /// </summary>
    /// <param name="other">Other unit group to check against</param>
    /// <returns>True if paths intersect and units are on opposing teams</returns>
    public bool HasPathConflict(Units other)
    {
        if (other == null || other.teamID == teamID)
            return false;

        if (plannedPath == null || other.plannedPath == null)
            return false;

        // Check if paths share any cells
        return plannedPath.Intersect(other.plannedPath).Any();
    }

    /// <summary>
    /// Resolves combat between two unit groups.
    /// Units reduce each other's count 1-for-1 until one is eliminated.
    /// </summary>
    /// <param name="other">Enemy unit group</param>
    public void ResolveCombat(Units other)
    {
        if (other == null || other.teamID == teamID)
            return;

        int thisCount = unitCount;
        int otherCount = other.unitCount;

        // 1-for-1 combat
        int casualties = Mathf.Min(thisCount, otherCount);

        unitCount -= casualties;
        other.unitCount -= casualties;

        Debug.Log($"Combat: Team {teamID} ({thisCount} → {unitCount}) vs Team {other.teamID} ({otherCount} → {other.unitCount})");

        // Destroy eliminated unit groups
        if (unitCount <= 0)
        {
            state = UnitState.Destroyed;
            DestroyUnitGroup();
        }

        if (other.unitCount <= 0)
        {
            other.state = UnitState.Destroyed;
            other.DestroyUnitGroup();
        }
    }

    /// <summary>
    /// Commands this unit group to move to a target along path.
    /// Creates a "queued action" that will be executed on turn resolution.
    /// Owner reference is KEPT to remember where units came from.
    /// </summary>
    /// <param name="path">Path from current position to destination</param>
    /// <param name="destination">Target GridObject (for capture/reinforce/merge)</param>
    public void QueueMovement(List<HexCell> path, GridObject destination)
    {
        if (state != UnitState.Stationed)
        {
            Debug.LogWarning("Can only command stationed units");
            return;
        }

        plannedPath = path;
        targetObject = destination;
        state = UnitState.Traversing;

        // KEEP owner reference - it now represents the source
        // Owner = where these units came from
        // targetObject = where they're going

        Debug.Log($"{unitCount} units from Team {teamID} (from {owner?.GetType().Name}) queued to move");
    }

    /// <summary>
    /// Executes the queued movement, actually moving units along the path.
    /// Called during turn resolution after all commands are queued.
    /// </summary>
    public void ExecuteMovement()
    {
        if (state != UnitState.Traversing || plannedPath == null)
            return;

        int finalCount = CalculateRemainingUnits(plannedPath, unitCount);

        if (finalCount <= 0)
        {
            Debug.Log($"All units eliminated during movement");
            DestroyUnitGroup();
            return;
        }

        unitCount = finalCount;

        // Move to destination cell
        HexCell destination = plannedPath[plannedPath.Count - 1];
        parentCell?.RemoveGridObject(this);

        if (destination.TryAddGridObject(this))
        {
            // Try to attach to target object if specified
            if (targetObject != null && targetObject.parentCell == destination)
            {
                AttachToOwner(targetObject);  // Changes owner to destination
            }
            else
            {
                // Otherwise, try to find a suitable owner
                TryFindOwner();  // Changes owner to whatever is found
            }
        }

        if (targetObject is SpireConstruct spire)
            spire.OnUnitsArrived(this);

        plannedPath = null;
        targetObject = null;
    }

    /// <summary>
    /// Cancels queued movement, returning units to stationed state.
    /// Owner reference is preserved, so units return to their source.
    /// </summary>
    public void CancelMovement()
    {
        if (state == UnitState.Traversing)
        {
            state = UnitState.Stationed;
            plannedPath = null;
            targetObject = null;

            // owner is still set to the source, so units "remember" where they came from
            Debug.Log($"{unitCount} units cancelled movement, remaining with {owner?.GetType().Name}");
        }
    }

    /// <summary>
    /// Destroys this unit group and cleans up references.
    /// </summary>
    private void DestroyUnitGroup()
    {
        owner = null; // Clear reference before destroying
        OnRemovedFromGrid();
        Destroy(gameObject);
    }

    /// <summary>
    /// Gets the total unit count belonging to this GridObject (including attached units).
    /// Use for easy calculation of strategic value of Spires, unit groups, etc.
    /// </summary>
    /// <returns>Total unit count including all attached units</returns>
    public int GetTotalUnitCount()
    {
        int total = unitCount;

        // Find all units that belong to this unit group
        if (parentCell != null)
        {
            var attachedUnits = parentCell.GetObjectsByType(GridObjectType.Unit)
                .OfType<Units>()
                .Where(u => u.owner == this && u.state == UnitState.Stationed);

            total += attachedUnits.Sum(u => u.unitCount);
        }

        return total;
    }

    /// <summary>
    /// Gets a debug string showing ownership chain.
    /// </summary>
    public override string ToString()
    {
        string stateInfo = state == UnitState.Traversing ? $" (traversing to {targetObject?.GetType().Name})" : "";
        string ownerInfo = owner != null ? $" (belongs to {owner.GetType().Name})" : " (neutral/independent)";
        return $"Units[{unitCount}] Team {teamID} at {GridPosition}{ownerInfo}{stateInfo}";
    }
}