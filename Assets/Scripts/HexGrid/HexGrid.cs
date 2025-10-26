// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a hexagonal grid system using axial coordinates.
/// Controls HexCell creation, positioning, and provides grid-wide operations like neighbor calculations.
/// Supports multiple layered grids for different object types (terrain, units, constructs).
/// </summary>
public class HexGrid : MonoBehaviour
{
    /// <summary>
    /// The dimensions of the hex grid (width x height in cells).
    /// </summary>
    [Header("Grid Configuration")]
    public Vector2Int gridSize = new Vector2Int(10, 10);

    /// <summary>
    /// The distance between adjacent hex cell centers.
    /// </summary>
    public float cellSpacing = 1.0f;

    /// <summary>
    /// Prefab used to instantiate individual HexCells.
    /// </summary>
    [Header("Cell Prefab")]
    public GameObject hexCellPrefab;

    /// <summary>
    /// Dictionary storing all HexCells by their axial coordinates for efficient lookup.
    /// </summary>
    private Dictionary<Vector2Int, HexCell> cells;

    /// <summary>
    /// Axial coordinate direction vectors for the six hex neighbors.
    /// Used for neighbor calculations in axial coordinate system.
    /// </summary>
    private static readonly Vector2Int[] axialDirections = {
        new Vector2Int(1, 0),   // East
        new Vector2Int(1, -1),  // Northeast  
        new Vector2Int(0, -1),  // Northwest
        new Vector2Int(-1, 0),  // West
        new Vector2Int(-1, 1),  // Southwest
        new Vector2Int(0, 1)    // Southeast
    };

    /// <summary>
    /// Called when the component is first created.
    /// Initializes the cell storage system.
    /// </summary>
    private void Awake()
    {
        InitializeGrid();
    }

    /// <summary>
    /// Initializes the grid storage system and creates all HexCells.
    /// </summary>
    private void InitializeGrid()
    {
        cells = new Dictionary<Vector2Int, HexCell>();
        CreateHexCells();
    }

    /// <summary>
    /// Creates and positions all HexCells within the grid dimensions.
    /// Each cell is assigned axial coordinates and positioned in world space.
    /// </summary>
    private void CreateHexCells()
    {
        if (hexCellPrefab == null)
        {
            Debug.LogError("HexGrid: No hexCellPrefab assigned!");
            return;
        }

        for (int q = -gridSize.x / 2; q < gridSize.x / 2; q++)
        {
            for (int r = -gridSize.y / 2; r < gridSize.y / 2; r++)
            {
                Vector2Int axialCoord = new Vector2Int(q, r);
                Vector3 worldPos = AxialToWorldPosition(axialCoord);

                GameObject cellObj = Instantiate(hexCellPrefab, worldPos, Quaternion.identity, transform);
                HexCell cell = cellObj.GetComponent<HexCell>();

                if (cell != null)
                {
                    cell.axial_coords = axialCoord;
                    cell.parentGrid = this;
                    cells[axialCoord] = cell;
                }
            }
        }
    }

    /// <summary>
    /// Converts axial coordinates to world position for hex cell placement.
    /// Uses flat-top hexagon layout with proper spacing.
    /// </summary>
    /// <param name="axialCoord">The axial coordinate to convert</param>
    /// <returns>World position for the hex cell</returns>
    private Vector3 AxialToWorldPosition(Vector2Int axialCoord)
    {
        float x = cellSpacing * (3.0f / 2.0f * axialCoord.x);
        float z = cellSpacing * (Mathf.Sqrt(3.0f) / 2.0f * axialCoord.x + Mathf.Sqrt(3.0f) * axialCoord.y);
        return new Vector3(x, 0, z);
    }

    /// <summary>
    /// Gets the HexCell at the specified axial coordinates.
    /// </summary>
    /// <param name="axialCoord">The axial coordinate to look up</param>
    /// <returns>The HexCell at those coordinates, or null if none exists</returns>
    public HexCell GetCell(Vector2Int axialCoord)
    {
        cells.TryGetValue(axialCoord, out HexCell cell);
        return cell;
    }

    /// <summary>
    /// Gets all neighboring HexCells adjacent to the specified coordinates.
    /// Returns up to 6 neighbors in hexagonal grid layout.
    /// </summary>
    /// <param name="axialCoord">The center coordinate to find neighbors for</param>
    /// <returns>List of neighboring HexCells that exist in the grid</returns>
    public List<HexCell> GetNeighbors(Vector2Int axialCoord)
    {
        List<HexCell> neighbors = new List<HexCell>();

        foreach (Vector2Int direction in axialDirections)
        {
            Vector2Int neighborCoord = axialCoord + direction;
            HexCell neighbor = GetCell(neighborCoord);

            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Checks if a coordinate position exists within this grid.
    /// </summary>
    /// <param name="axialCoord">The coordinate to check</param>
    /// <returns>True if the coordinate exists in the grid, false otherwise</returns>
    public bool IsValidCoordinate(Vector2Int axialCoord)
    {
        return cells.ContainsKey(axialCoord);
    }

    /// <summary>
    /// Gets all HexCells currently managed by this grid.
    /// </summary>
    /// <returns>Collection of all HexCells in the grid</returns>
    public IEnumerable<HexCell> GetAllCells()
    {
        return cells.Values;
    }
}
