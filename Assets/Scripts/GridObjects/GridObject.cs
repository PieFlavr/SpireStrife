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

/// <summary>
/// Abstract base class for all objects that can exist on the hex grid.
/// Provides common functionality for grid positioning, cell management, and type identification.
/// Designed to be inherited by specific object types like Tiles, Constructs, and Units.
/// </summary>
public abstract class GridObject : MonoBehaviour
{
    /// <summary>
    /// The type category of this grid object (Tile, Construct, or Unit).
    /// Used for layer management and type-specific operations.
    /// </summary>
    [Header("Grid Object Data")]
    public GridObjectType objectType;

    /// <summary>
    /// Team/player ID for this grid object.
    /// 0 = neutral/undeclared, 1+ = player teams
    /// Use for ownership, combat, and AI evaluation.
    /// </summary>
    [Tooltip("Which player/team owns this object")]
    public int teamID = 0;

    /// <summary>
    /// Reference to the HexCell that contains this GridObject.
    /// Provides access to neighboring cells and grid operations.
    /// </summary>
    public HexCell parentCell;

    /// <summary>
    /// Gets the grid position from the parent cell. Returns Vector2Int.zero if not placed on a cell.
    /// </summary>
    public Vector2Int GridPosition => parentCell?.axial_coords ?? Vector2Int.zero;

    /// <summary>
    /// Called when the component is first created.
    /// Initializes the object type and performs initial setup.
    /// </summary>
    protected virtual void Awake()
    {
        SetObjectType();
        InitializeGridObject();
    }

    /// <summary>
    /// Sets the objectType field based on the concrete implementation.
    /// Must be implemented by derived classes to specify their type.
    /// </summary>
    protected abstract void SetObjectType();

    /// <summary>
    /// Performs initial setup specific to this GridObject.
    /// Override in derived classes for custom initialization logic.
    /// </summary>
    protected virtual void InitializeGridObject()
    {
        // Base initialization - override in derived classes
    }

    /// <summary>
    /// Called when this GridObject is placed on a hex cell.
    /// Updates position references and notifies the cell of the placement.
    /// </summary>
    /// <param name="cell">The HexCell this object is being placed on</param>
    public virtual void OnPlacedOnGrid(HexCell cell)
    {
        parentCell = cell;
        transform.position = cell.transform.position;
    }

    /// <summary>
    /// Called when this GridObject is removed from the hex grid.
    /// Cleans up references and notifies systems of the removal.
    /// </summary>
    public virtual void OnRemovedFromGrid()
    {
        if (parentCell != null)
        {
            parentCell = null;
        }
    }

    /// <summary>
    /// Checks if this GridObject can be placed on the specified cell.
    /// Override in derived classes to implement specific placement rules.
    /// </summary>
    /// <param name="cell">The target HexCell for placement</param>
    /// <returns>True if placement is valid, false otherwise</returns>
    public virtual bool CanBePlacedOn(HexCell cell)
    {
        return cell != null;
    }

    /// <summary>
    /// Gets a string representation of this GridObject for debugging.
    /// </summary>
    /// <returns>Formatted string with object type and position</returns>
    public override string ToString()
    {
        return $"{objectType} at {GridPosition}";
    }

    // Start is called before the first frame update
    //void Start() { }

    // Update is called once per frame
    //void Update() { }
}