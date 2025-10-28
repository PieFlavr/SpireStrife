// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:
//
// Modified by Gemini to include a configurable base plane.
//

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
    /// The axial dimensions of the hex grid (width x height in cells).
    /// </summary>
    [Header("Grid Configuration")]
    public Vector2Int gridSize = new Vector2Int(10, 10); //defaulted to 10x10 axial

    /// <summary>
    /// The distance between adjacent hex cell center based on prefab dimensions.
    /// </summary>
    public float cellSpacing = 1.0f;

    /// <summary>
    /// Prefab used to instantiate individual HexCells.
    /// </summary>
    [Header("Cell Prefab")]
    public GameObject hexCellPrefab;

    /// <summary>
    /// Prefab for the base plane (optional). Should be a standard 10x10 Unity Plane.
    /// </summary>
    [Header("Base Plane")]
    public GameObject basePlanePrefab;

    /// <summary>
    /// Whether to create a base plane under the grid.
    /// </summary>
    public bool createBasePlane = true;

    /// <summary>
    /// The world-space size (X and Z) of the base plane.
    /// </summary>
    public Vector2 basePlaneSize = new Vector2(100, 100);

    /// <summary>
    /// The world-space offset for the base plane.
    /// </summary>
    public Vector3 basePlaneOffset = new Vector3(0, -0.5f, 0);

    /// <summary>
    /// Dictionary storing all HexCells by their axial coordinates for efficient lookup.
    /// </summary>
    private Dictionary<Vector2Int, HexCell> cells;

    /// <summary>
    /// The total number of cells in the grid, used for heap initialization.
    /// </summary>
    public int MaxSize
    {
        get
        {
            if (cells == null) return 0;
            return cells.Count;
        }
    }

    /// <summary>
    /// Instance of the created base plane.
    /// </summary>
    private GameObject _basePlaneInstance;

    /// <summary>
    /// Cached previous grid size to detect inspector changes during runtime.
    /// </summary>
    private Vector2Int previousGridSize;

    /// <summary>
    /// Cached previous cell spacing to detect inspector changes during runtime.
    /// </summary>
    private float previousCellSpacing;

    /// <summary>
    /// Cached previous base plane toggle state.
    /// </summary>
    private bool previousCreateBasePlane;

    /// <summary>
    /// Cached previous base plane size.
    /// </summary>
    private Vector2 previousBasePlaneSize;

    /// <summary>
    /// Cached previous base plane offset.
    /// </summary>
    private Vector3 previousBasePlaneOffset;

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

        // Cache initial inspector values
        previousGridSize = gridSize;
        previousCellSpacing = cellSpacing;
        previousCreateBasePlane = createBasePlane;
        previousBasePlaneSize = basePlaneSize;
        previousBasePlaneOffset = basePlaneOffset;

        if (Application.isPlaying)
        {
            StartCoroutine(MonitorInspectorChanges());
        }
    }

    /// <summary>
    /// Called when values are changed in the Unity Inspector.
    /// Validates grid parameters and triggers rebuild when appropriate.
    /// </summary>
    private void OnValidate()
    {
        // Clamp values to valid ranges
        gridSize.x = Mathf.Max(1, gridSize.x);
        gridSize.y = Mathf.Max(1, gridSize.y);
        cellSpacing = Mathf.Max(0.1f, cellSpacing);
        basePlaneSize.x = Mathf.Max(0.1f, basePlaneSize.x);
        basePlaneSize.y = Mathf.Max(0.1f, basePlaneSize.y);
        

        // In play mode, rebuild immediately when values change
        if (Application.isPlaying && cells != null)
        {
            RebuildGrid();
        }
        // In edit mode, delay rebuild to avoid Unity editor conflicts
        else if (!Application.isPlaying && (transform.childCount > 0 || _basePlaneInstance != null))
        {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null) RebuildGrid();
            };
        #endif
        }
    }

    /// <summary>
    /// Initializes the grid storage system and creates all HexCells.
    /// </summary>
    private void InitializeGrid()
    {
        cells = new Dictionary<Vector2Int, HexCell>();
        CreateBasePlane(); // Create plane first so it's "under"
        CreateHexCells();
    }

    /// <summary>
    /// Creates a base plane GameObject based on prefab and settings.
    /// </summary>
    private void CreateBasePlane()
    {
        // If the instance already exists, destroy it first.
        if (_basePlaneInstance != null)
        {
            if (Application.isPlaying) Destroy(_basePlaneInstance);
            else DestroyImmediate(_basePlaneInstance);
            _basePlaneInstance = null;
        }

        if (!createBasePlane || basePlanePrefab == null)
        {
            return; // Don't create if toggled off or no prefab
        }

        _basePlaneInstance = Instantiate(basePlanePrefab, transform);
        _basePlaneInstance.name = "GridBasePlane";
        _basePlaneInstance.transform.localPosition = basePlaneOffset;
        
        // A standard Unity plane is 10x10 units. 
        // We scale it to match the desired basePlaneSize.
        _basePlaneInstance.transform.localScale = new Vector3(basePlaneSize.x / 10.0f, 1.0f, basePlaneSize.y / 10.0f);
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
    int radius = gridSize.x;
        for (int q = -radius; q <= radius; q++)
    {
        // Calculate the valid range for 'r' based on 'q'
        int r1 = Mathf.Max(-radius, -q - radius);
        int r2 = Mathf.Min(radius, -q + radius);
        for (int r = r1; r <= r2; r++)
        {
            Vector2Int axialCoord = new Vector2Int(q, r);
            Vector3 worldPos = AxialToWorldPosition(axialCoord);

                GameObject cellObj = Instantiate(hexCellPrefab, worldPos, Quaternion.identity, transform);
                HexCell cell = cellObj.GetComponent<HexCell>();
                cellObj.name = $"HexCell_{q}_{r}";
                cellObj.SetActive(true);
                // Resize the instantiated cell to match cellSpacing (uniform scale)
                cellObj.transform.localScale = new Vector3(cellSpacing * 2, 0.1f, cellSpacing * 2);

                if (cell != null)
                {
                    cell.axial_coords = axialCoord;
                    cell.parentGrid = this;
                    cells[axialCoord] = cell;

                    // Give it a random green color as an example
                    float randomGreen = Random.Range(0.3f, 1.0f);
                    cell.SetColor(new Color(0f, randomGreen, 0f));
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
    public Vector3 AxialToWorldPosition(Vector2Int axialCoord)
    {
        // Should pprobably double check this formula later...
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
    /// Monitors inspector values during play mode and rebuilds grid when changes are detected.
    /// Runs continuously during gameplay to provide reactive updates.
    /// </summary>
    private IEnumerator MonitorInspectorChanges()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // Check twice per second

            bool planeSettingsChanged = createBasePlane != previousCreateBasePlane ||
                                        basePlaneSize != previousBasePlaneSize ||
                                        basePlaneOffset != previousBasePlaneOffset;

            // Detect if any values have changed
            if (gridSize != previousGridSize || !Mathf.Approximately(cellSpacing, previousCellSpacing) || planeSettingsChanged)
            {
                Debug.Log($"HexGrid: Inspector values changed. Rebuilding grid...");
                previousGridSize = gridSize;
                previousCellSpacing = cellSpacing;
                previousCreateBasePlane = createBasePlane;
                previousBasePlaneSize = basePlaneSize;
                previousBasePlaneOffset = basePlaneOffset;

                RebuildGrid();
            }
        }
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

    /// <summary>
    /// Clears all existing HexCells and the base plane from the grid.
    /// Used when rebuilding the grid with new parameters.
    /// </summary>
    private void ClearGrid()
    {
        if (cells != null)
        {
            cells.Clear();
        }

        // Destroy the base plane instance
        if (_basePlaneInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_basePlaneInstance);
            }
            else
            {
                DestroyImmediate(_basePlaneInstance);
            }
            _basePlaneInstance = null; // Clear the reference
        }

        // Destroy all child cell GameObjects
        // We loop backwards because the child list is modified
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject childObj = transform.GetChild(i).gameObject;
            
            // Avoid destroying the plane again if it hasn't been removed yet
            if (childObj == _basePlaneInstance)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(childObj);
            }
            else
            {
                DestroyImmediate(childObj);
            }
        }
    }

    /// <summary>
    /// Rebuilds the entire grid with current inspector values.
    /// Clears existing cells and creates new ones based on updated parameters.
    /// </summary>
    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        ClearGrid();
        InitializeGrid();
    }
    public Vector2Int WorldToAxial(Vector3 pos) {
    float qf = (2f/3f) * pos.x / cellSpacing;
    float rf = (-1f/3f) * pos.x / cellSpacing + (1f/Mathf.Sqrt(3f)) * pos.z / cellSpacing;
    return CubeRound_Axial(qf, rf);
}
    private Vector2Int CubeRound_Axial(float qf, float rf)
    {
        // axial→cube
        float xf = qf;
        float zf = rf;
        float yf = -xf - zf;
        int xi = Mathf.RoundToInt(xf);
        int yi = Mathf.RoundToInt(yf);
        int zi = Mathf.RoundToInt(zf);
        float dx = Mathf.Abs(xi - xf), dy = Mathf.Abs(yi - yf), dz = Mathf.Abs(zi - zf);
        if (dx > dy && dx > dz) xi = -yi - zi;
        else if (dy > dz) yi = -xi - zi;
        else zi = -xi - yi;
        // cube→axial
        return new Vector2Int(xi, zi);
    }

public HexCell GetCellFromWorldPosition(Vector3 worldPos)
{
    
    foreach (HexCell cell in cells.Values) 
    {
        // We check if the worldPos is very close to the cell's transform position.
        // We use a small tolerance (0.1f) because floating-point numbers
        // from pathfinding might not be perfectly exact.
        if (Vector3.Distance(cell.transform.position, worldPos) < 0.1f)
        {
            return cell;
        }
    }

    // If no cell is found at that exact position
    Debug.LogWarning("HexGrid.GetCellFromWorldPosition: No cell found at " + worldPos);
    return null;
}
}