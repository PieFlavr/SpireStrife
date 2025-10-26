// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the different types of objects that can exist on the hex grid.
/// Used to categorize GridObjects for layer management and type-specific behavior.
/// </summary>
public enum GridObjectType
{
    /// <summary>
    /// Terrain tiles and obstacles that affect movement and pathfinding
    /// </summary>
    Tile,

    /// <summary>
    /// Utility objects that do not directly affect pathfinding
    /// </summary>
    Construct,

    /// <summary>
    /// Moveable units that can travel between grid positions
    /// </summary>
    Unit
}
