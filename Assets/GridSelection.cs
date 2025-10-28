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

        previousPathCellColors.Clear();

        for (int i = 0; i < currentPath.Length - 1; i++)
        {
            Vector3 start = currentPath[i];
            Vector3 end = currentPath[i + 1];

            // Get the cells corresponding to start and end
            HexCell startCell = grid.GetCellFromWorldPosition(start);
            HexCell endCell = grid.GetCellFromWorldPosition(end);

            if (startCell == null || endCell == null)
                continue;

            // Use a hex line algorithm to get all cells between start and end
            List<HexCell> lineCells = grid.GetCellsAlongLine(startCell, endCell);

            foreach (HexCell cell in lineCells)
            {
                if (cell != currentlySelectedCell && cell != currentlyTargetCell)
                {
                    if (!previousPathCellColors.ContainsKey(cell))
                        previousPathCellColors[cell] = cell.GetColor();

                    cell.SetColor(pathColor);
                }
            }
        }
    }
}