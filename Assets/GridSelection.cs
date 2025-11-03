using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSelection : MonoBehaviour
{
    public HexGrid grid; // Assign in Inspector
    private HexCell currentlySelectedCell;
    private HexCell currentlyTargetCell;
    private Color previousSelectedColor;
    private Color previousTargetColor;
    private Color selectedColor = Color.blue;
    private Color targetColor = Color.yellow;

    // --- NEW ---
    // Color for the path cells
    private Color pathColor = Color.cyan;
    // Dictionary to store the original colors of the cells along the path
    private Dictionary<HexCell, Color> previousPathCellColors = new Dictionary<HexCell, Color>();

    // --- MODIFIED ---
    // We now use a list to hold multiple LineRenderers
    private List<LineRenderer> pathLineRenderers = new List<LineRenderer>();
    private Vector3[] currentPath;

    // --- NEW ---
    // Store line properties to apply to each new line segment
    private Material lineMaterial;
    private Color lineColor = Color.red;
    private float lineStartWidth = 1f;
    private float lineEndWidth = 1f;

    void Start()
    {
        if (grid == null)
        {
            grid = FindObjectOfType<HexGrid>();
        }

        // --- MODIFIED ---
        // Initialize the material that all line segments will share
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.color = lineColor;
        
        // We no longer create a single LineRenderer here
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                HexCell clickedCell = hit.collider.GetComponent<HexCell>();

                if (clickedCell != null && clickedCell.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
                {
                    SelectCell(clickedCell);
                }
            }
        }

        if (Input.GetMouseButtonDown(1)) // Right mouse button
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                HexCell clickedCell = hit.collider.GetComponent<HexCell>();

                if (clickedCell != null && clickedCell.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
                {
                    SelectTargetCell(clickedCell);
                }
            }
        }
    }

    private void SelectCell(HexCell cell)
    {
        if (cell == currentlyTargetCell)
        {
            return;
        }

        if (currentlySelectedCell != null)
        {
            currentlySelectedCell.SetColor(previousSelectedColor);
        }

        currentlySelectedCell = cell;
        previousSelectedColor = currentlySelectedCell.GetColor();
        currentlySelectedCell.SetColor(selectedColor);

        ClearPath();
    }

    // --- MODIFIED ---
    private void ClearPath()
    {
        // --- NEW ---
        // Restore the original color of all cells in the previous path
        foreach (var cellColorPair in previousPathCellColors)
        {
            // Check if the cell still exists and isn't the *new* selected or target cell
            if (cellColorPair.Key != null && 
                cellColorPair.Key != currentlySelectedCell && 
                cellColorPair.Key != currentlyTargetCell)
            {
                cellColorPair.Key.SetColor(cellColorPair.Value);
            }
        }
        previousPathCellColors.Clear(); // Clear the color cache
        // --- END NEW ---

        currentPath = null;
        
        // Loop through all the LineRenderers we created and destroy their GameObjects
        foreach (LineRenderer line in pathLineRenderers)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }
        // Clear the list itself
        pathLineRenderers.Clear();
    }

    private void SelectTargetCell(HexCell cell)
    {
        if (cell == currentlySelectedCell)
        {
            return;
        }

        if (currentlyTargetCell != null)
        {
            currentlyTargetCell.SetColor(previousTargetColor);
        }

        currentlyTargetCell = cell;
        previousTargetColor = currentlyTargetCell.GetColor();
        currentlyTargetCell.SetColor(targetColor);

        ClearPath(); // Clear old path before requesting a new one

        if (currentlySelectedCell != null && currentlyTargetCell != null)
        {
            RequestPath();
        }
    }

    private void RequestPath()
    {
        if (currentlySelectedCell == null || currentlyTargetCell == null)
            return;

        Vector3 startPos = currentlySelectedCell.transform.position;
        Vector3 targetPos = currentlyTargetCell.transform.position;

        PathRequestManager.RequestPath(startPos, targetPos, new List<Vector3>(), OnPathFound);
    }

    // --- MODIFIED ---
    private void OnPathFound(Vector3[] path, bool pathSuccessful)
    {
        if (pathSuccessful)
        {
            currentPath = path;
            
            // --- NEW ---
            ColorPathCells(); // Color the cells along the new path
            // --- END NEW ---

            // RenderPath();
        }
        else
        {
            Debug.Log("Path not found!");
            currentPath = null;
            
            // Use our ClearPath method to clean up lines AND colors
            ClearPath();
        }
    }

    // --- NEW ---
    /// <summary>
    /// Colors all cells along the currentPath.
    /// Assumes grid.GetCellFromWorldPosition() exists.
    /// </summary>
    private void ColorPathCells()
    {
        if (currentPath == null || grid == null)
            return;

        // ClearPath() should have already cleared the dictionary, but we do
        // it here just in case to avoid any potential bugs.
        previousPathCellColors.Clear();

        foreach (Vector3 cellPosition in currentPath)
        {
            // This is the crucial step. You need a method in your HexGrid script
            // that can convert a world position (Vector3) back to a HexCell.
            HexCell cell = grid.GetCellFromWorldPosition(cellPosition); 

            if (cell != null)
            {
                // Don't color the start or end cell, as they
                // already have their 'selected' and 'target' colors.
                if (cell != currentlySelectedCell && cell != currentlyTargetCell)
                {
                    // Store the cell's original color so we can restore it later
                    previousPathCellColors[cell] = cell.GetColor();
                    // Set the new path color
                    cell.SetColor(pathColor);
                }
            }
            else
            {
                Debug.LogWarning("Could not find cell at position: " + cellPosition);
            }
        }
    }
    // --- END NEW ---

    // --- HEAVILY MODIFIED ---
    // private void RenderPath()
    // {
    //     // We already cleared the old path in SelectTargetCell or SelectCell,
    //     // so we just need to build the new one.
        
    //     // Need at least 2 points to draw a line segment
    //     if (currentPath == null || currentPath.Length < 2)
    //     {
    //         return;
    //     }

    //     // Loop through the path array, stopping at the second-to-last point
    //     for (int i = 0; i < currentPath.Length - 1; i++)
    //     {
    //         // Create a new GameObject to hold the line segment
    //         // Name it for clarity in the hierarchy
    //         GameObject lineObject = new GameObject("PathSegment_" + i);
            
    //         // Parent it to this object so it gets destroyed if this object is
    //         lineObject.transform.SetParent(this.transform);

    //         // Add the LineRenderer component
    //         LineRenderer line = lineObject.AddComponent<LineRenderer>();

    //         // Configure its properties
    //         line.material = lineMaterial;
    //         line.startWidth = lineStartWidth;
    //         line.endWidth = lineEndWidth;
    //         line.positionCount = 2; // Each segment only has a start and an end

    //         // Set the two points for this segment
    //         line.SetPosition(0, currentPath[i]);
    //         line.SetPosition(1, currentPath[i + 1]);

    //         // Add the new LineRenderer to our list for cleanup later
    //         pathLineRenderers.Add(line);
    //     }
    // }
}