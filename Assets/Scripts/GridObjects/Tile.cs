using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all tile types in the hex grid.
/// Defines terrain properties that affect pathfinding and movement.
/// Derived classes implement specific tile behaviors.
/// </summary>
public class Tile : GridObject
{
    /// <summary>
    /// Base movement cost inherent to this tile type.
    /// </summary>
    [Header("Movement Properties")]
    [Tooltip("Base cost to traverse this tile")]
    protected float baseTraversalCost = 1.0f;

    /// <summary>
    /// Dynamic cost modifier applied temporarily.
    /// Added to baseTraversalCost. Can be positive (harder) or negative (easier).
    /// Reset this to 0 to clear all temporary effects.
    /// </summary>
    [Tooltip("Temporary modifier to traversal cost (effects, spells, etc.)")]
    public float modifierTraversalCost = 0f;

    /// <summary>
    /// Determines if units can move through this tile at all.
    /// </summary>
    [Tooltip("Can units traverse this tile?")]
    public bool traversible = true;

    protected override void SetObjectType()
    {
        objectType = GridObjectType.Tile;
    }

    /// <summary>
    /// Gets the total effective movement cost for pathfinding.
    /// Returns infinity if not traversible, otherwise base + modifiers.
    /// Use this in pathfinding algorithms.
    /// </summary>
    /// <returns>Total movement cost, or float.PositiveInfinity if impassable</returns>
    public virtual float GetMovementCost()
    {
        if (!traversible)
            return float.PositiveInfinity;

        return Mathf.Max(0.1f, baseTraversalCost + modifierTraversalCost);
    }

    /// <summary>
    /// Checks if a unit can enter this tile.
    /// Override in derived classes for additional conditions.
    /// </summary>
    /// <returns>True if tile can be entered</returns>
    public virtual bool CanEnter()
    {
        return traversible;
    }

    /// <summary>
    /// Applies a temporary cost modifier to this tile.
    /// Use for spells, weather effects, or other dynamic changes.
    /// </summary>
    /// <param name="modifier">Amount to add to traversal cost</param>
    public void ApplyCostModifier(float modifier)
    {
        modifierTraversalCost += modifier;
    }

    /// <summary>
    /// Clears all temporary cost modifiers, resetting to base cost.
    /// </summary>
    public void ClearModifiers()
    {
        modifierTraversalCost = 0f;
    }
}