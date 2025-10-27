// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.SceneManagement;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages a hexagonal grid system using axial coordinates.
/// Controls HexCell creation, positioning, and provides grid-wide operations like neighbor calculations.
/// Supports multiple layered grids for different object types (terrain, units, constructs).
/// </summary>
[ExecuteAlways]
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
    /// Dictionary storing all HexCells by their axial coordinates for efficient lookup.
    /// </summary>
    private Dictionary<Vector2Int, HexCell> cells;

    /// <summary>
    /// Cached previous grid size to detect inspector changes during runtime.
    /// </summary>
    private Vector2Int previousGridSize;

    /// <summary>
    /// Cached previous cell spacing to detect inspector changes during runtime.
    /// </summary>
    private float previousCellSpacing;

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

        previousGridSize = gridSize;
        previousCellSpacing = cellSpacing;

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

        // In play mode, rebuild immediately when values change
        if (Application.isPlaying && cells != null)
        {
            RebuildGrid();
        }
        // In edit mode, delay rebuild to avoid Unity editor conflicts
        else if (!Application.isPlaying && transform.childCount > 0)
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
        CreateHexCells();
    }

    /// <summary>
    /// Creates and positions all HexCells within the grid dimensions.
    /// Each cell is assigned axial coordinates and positioned in world space.
    /// </summary>
    // Replace the existing CreateHexCells() implementation with this
    private void CreateHexCells()
    {
        if (hexCellPrefab == null)
        {
            Debug.LogError("HexGrid: No hexCellPrefab assigned!");
            return;
        }

        // Ensure children are cleared first (ClearGrid already handles it if you call RebuildGrid)
        // but when called directly just ensure we start clean
        if (transform.childCount > 0)
        {
            // use DestroyImmediate in editor, Destroy at runtime
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        for (int q = -gridSize.x / 2; q < gridSize.x / 2; q++)
        {
            for (int r = -gridSize.y / 2; r < gridSize.y / 2; r++)
            {
                Vector2Int axialCoord = new Vector2Int(q, r);
                Vector3 worldPos = AxialToWorldPosition(axialCoord);

                GameObject cellObj;

#if UNITY_EDITOR
                // In editor keep the prefab connection where possible
                var prefabInstance = PrefabUtility.InstantiatePrefab(hexCellPrefab, transform) as GameObject;
                if (prefabInstance != null)
                {
                    cellObj = prefabInstance;
                    cellObj.transform.position = worldPos;
                    cellObj.transform.rotation = Quaternion.identity;
                }
                else
                {
                    cellObj = Instantiate(hexCellPrefab, worldPos, Quaternion.identity, transform);
                }
#else
            cellObj = Instantiate(hexCellPrefab, worldPos, Quaternion.identity, transform);
#endif

                HexCell cell = cellObj.GetComponent<HexCell>();

                if (cell != null)
                {
                    cell.axial_coords = axialCoord;
                    cell.parentGrid = this;
                    if (cells == null) cells = new Dictionary<Vector2Int, HexCell>();
                    cells[axialCoord] = cell;
                }
            }
        }

#if UNITY_EDITOR
        // Mark scene dirty in editor so changes persist
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#endif
    }

    /// <summary>
    /// Converts axial coordinates to world position for hex cell placement.
    /// Uses flat-top hexagon layout with proper spacing.
    /// </summary>
    /// <param name="axialCoord">The axial coordinate to convert</param>
    /// <returns>World position for the hex cell</returns>
    private Vector3 AxialToWorldPosition(Vector2Int axialCoord)
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

            // Detect if any values have changed
            if (gridSize != previousGridSize || !Mathf.Approximately(cellSpacing, previousCellSpacing))
            {
                Debug.Log($"HexGrid: Inspector values changed. Rebuilding grid...");
                previousGridSize = gridSize;
                previousCellSpacing = cellSpacing;
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
    /// Clears all existing HexCells from the grid.
    /// Used when rebuilding the grid with new parameters.
    /// </summary>
    private void ClearGrid()
    {
        if (cells != null)
        {
            cells.Clear();
        }

        // Destroy all child cell GameObjects
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
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
}
