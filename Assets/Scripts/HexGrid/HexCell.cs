// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // for querying object types

/// <summary>
/// Represents a single hexagonal cell within the hex grid system.
/// Manages grid objects placed on this cell and provides access to coordinate information and neighboring cells.
/// Supports multiple object types through layered storage for flexible grid management.
/// </summary>

public class HexCell : MonoBehaviour
{
    /// <summary>
    /// The axial coordinate position of this hex cell within the grid.
    /// Uses Vector2Int where x = q coordinate and y = r coordinate in axial system.
    /// </summary>
    [Header("Grid Position")]
    public Vector2Int axial_coords;

    /// <summary>
    /// List of all GridObjects currently placed on this cell.
    /// Exclusivity and placement rules are enforced by GridObject.CanBePlacedOn() method.
    /// </summary>
    [Header("Grid Objects")]
    [Tooltip("All GridObjects currently placed on this cell")]
    private List<GridObject> gridObjects;

    /// <summary>
    /// Reference to the parent HexGrid that contains this cell.
    /// Provides access to grid-wide operations and neighbor calculations.
    /// </summary>
    [Header("Grid Reference")]
    public HexGrid parentGrid;

    /// <summary>
    /// Called when the component is first created.
    /// Initializes the grid object storage system.
    /// </summary>
    private void Awake()
    {
        InitializeObjectStorage();
    }

    /// <summary>
    /// Sets up the list for storing GridObjects.
    /// Exclusivity logic is handled by individual GridObject placement validation.
    /// </summary>
    private void InitializeObjectStorage()
    {
        gridObjects = new List<GridObject>();
    }

    /// <summary>
    /// Adds a GridObject to this cell if placement rules allow it.
    /// Validates placement through the object's CanBePlacedOn method before adding.
    /// </summary>
    /// <param name="obj">The GridObject to add to this cell</param>
    /// <returns>True if successfully placed, false if placement was invalid</returns>
    public bool TryAddGridObject(GridObject obj)
    {
        if (obj.CanBePlacedOn(this))
        {
            gridObjects.Add(obj);
            obj.OnPlacedOnGrid(this);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a GridObject from this cell and notifies the object of its removal.
    /// Cleans up references and maintains list integrity.
    /// </summary>
    /// <param name="obj">The GridObject to remove from this cell</param>
    public void RemoveGridObject(GridObject obj)
    {
        if (gridObjects.Remove(obj))
        {
            obj.OnRemovedFromGrid();
        }
    }

    /// <summary>
    /// Gets all GridObjects of a specific type placed on this cell.
    /// </summary>
    /// <param name="type">The GridObjectType to retrieve</param>
    /// <returns>List of GridObjects matching the specified type</returns>
    public List<GridObject> GetObjectsByType(GridObjectType type)
    {
        return gridObjects.Where(obj => obj.objectType == type).ToList();
    }

    /// <summary>
    /// Gets all neighboring HexCells adjacent to this cell.
    /// Uses the parent grid to calculate neighbors based on axial coordinates.
    /// </summary>
    /// <returns>List of neighboring HexCells, or empty list if no parent grid is set</returns>
    public List<HexCell> GetNeighbors()
    {
        return parentGrid?.GetNeighbors(axial_coords) ?? new List<HexCell>();
    }

    /// <summary>
    /// Checks if this cell contains any GridObjects of the specified type.
    /// </summary>
    /// <param name="type">The GridObjectType to check for</param>
    /// <returns>True if objects of the specified type exist on this cell</returns>
    public bool HasObjectOfType(GridObjectType type)
    {
        return gridObjects.Any(obj => obj.objectType == type);
    }
}
