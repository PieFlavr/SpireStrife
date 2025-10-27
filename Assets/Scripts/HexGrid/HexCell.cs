// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // for querying object types
using System; // for IComparable

/// <summary>
/// Represents a single hexagonal cell within the hex grid system.
/// Manages grid objects placed on this cell and provides access to coordinate information and neighboring cells.
/// Supports multiple object types through layered storage for flexible grid management.
/// Now also serves as the node for A* pathfinding.
/// </summary>

public class HexCell : MonoBehaviour, IHeapItem<HexCell>
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
    /// Whether this cell is walkable for pathfinding.
    /// </summary>
    [Header("Pathfinding")]
    public bool isWalkable = true;

    /// <summary>
    /// Cached renderer component for changing the cell's color.
    /// </summary>
    private Renderer cellRenderer;
    
    /// <summary>
    /// The current display color of this cell.
    /// </summary>
    [Header("Cell Visuals")]
    [SerializeField]
    private Color cellColor = Color.white;

    // --- A* Pathfinding Data ---

    public int gCost; // Cost from the start node
    public int hCost; // Heuristic cost to the end node
    public HexCell parent; // The cell we came from

    public enum PathfindingState { None, Open, Closed, Path }
    public PathfindingState pathfindingState = PathfindingState.None;

    /// <summary>
    /// Total cost for A* (gCost + hCost)
    /// </summary>
    public int fCost
    {
        get { return gCost + hCost; }
    }

    // --- IHeapItem Implementation ---

    private int heapIndex;

    public int HeapIndex
    {
        get { return heapIndex; }
        set { heapIndex = value; }
    }

    /// <summary>
    /// Compares this cell to another for sorting in the heap.
    /// Lower fCost is higher priority.
    /// If fCosts are equal, lower hCost is higher priority.
    /// </summary>
    public int CompareTo(HexCell cellToCompare)
    {
        int compare = fCost.CompareTo(cellToCompare.fCost);
        if (compare == 0)
        {
            compare = hCost.CompareTo(cellToCompare.hCost);
        }
        // We return -compare because the heap is a Max-Heap,
        // but we want a Min-Heap (lowest cost = highest priority).
        return -compare;
    }

    // --- End of IHeapItem Implementation ---

    /// <summary>
    /// Called when the component is first created.
    /// Initializes the grid object storage system.
    /// </summary>
    private void Awake()
    {
        InitializeObjectStorage();
        
        // Cache the renderer
        cellRenderer = GetComponent<Renderer>(); 
        if (cellRenderer == null)
        {
            // If the renderer is on a child object
            cellRenderer = GetComponentInChildren<Renderer>();
        }
        
        // Apply the initial color
        SetColor(cellColor);
    }

    /// <summary>
    /// Helper to reset the cell's data for a new pathfind
    /// </summary>
    public void ResetPathData()
    {
        gCost = int.MaxValue;
        hCost = 0;
        parent = null;
        pathfindingState = PathfindingState.None;
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

    /// <summary>
    /// Sets the visible color of this hex cell.
    /// This creates a new material instance for this cell.
    /// </summary>
    /// <param name="color">The color to apply</param>
    public void SetColor(Color color)
    {
        cellColor = color;
        if (cellRenderer != null)
        {
            // This creates a new material instance per cell
            // which is fine for a few hundred cells.
            cellRenderer.material.color = color;
        }
    }

    /// <summary>
    /// Gets the current color of this cell.
    /// </summary>
    public Color GetColor()
    {
        return cellColor;
    }

    private void OnDrawGizmosSelected() {
        // Calculate cell center using collider or mesh bounds if available
        Vector3 cellCenter = transform.position;
        var collider = GetComponent<Collider>();
        if (collider != null) {
            cellCenter = collider.bounds.center;
        } else {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null) {
                cellCenter = transform.TransformPoint(meshFilter.sharedMesh.bounds.center);
            }
        }

        // Draw wire sphere with color based on walkability
        Gizmos.color = isWalkable ? Color.green : Color.red;
        Gizmos.DrawWireSphere(cellCenter, 0.15f);

        // Additional gizmos based on pathfinding state
        switch (pathfindingState) {
            case PathfindingState.Open:
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(cellCenter + Vector3.up * 0.1f, Vector3.one * 0.1f);
                break;
            case PathfindingState.Closed:
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(cellCenter + Vector3.up * 0.1f, Vector3.one * 0.1f);
                break;
            case PathfindingState.Path:
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(cellCenter + Vector3.up * 0.2f, 0.05f);
                break;
        }

        // Draw axial coordinates as text
        UnityEditor.Handles.Label(cellCenter + Vector3.up * 0.3f, $"({axial_coords.x}, {axial_coords.y})");

        // If has pathfinding data, show costs
        if (gCost > 0 || hCost > 0) {
            UnityEditor.Handles.Label(cellCenter + Vector3.up * 0.5f, $"G:{gCost} H:{hCost} F:{fCost}");
        }
    }
}
