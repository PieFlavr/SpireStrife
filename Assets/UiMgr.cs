using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UiMgr : MonoBehaviour
{
    // Start is called before the first frame update
    public HexCell startCell;
    public HexCell targetCell;
    public static UiMgr inst;
    public void Awake()
    {
        inst = this;
    }
    void Start()
    {
        if (grid == null)
        {
            grid = FindObjectOfType<HexGrid>();
        }

    }

    // Update is called once per frame
    void Update()
{
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Get the root object (parent) or use transform.root as needed
                GameObject root = hit.collider.transform.root.gameObject;
                int spireLayer = LayerMask.NameToLayer("Player");

                if (root.GetComponentInParent<SpireConstruct>() != null)
                {
                    if (root.layer == spireLayer)
                    {
                        Debug.Log("Spire hit on root: " + root.name + ", Layer: " + LayerMask.LayerToName(root.layer));
                        startCell = root.GetComponentInParent<SpireConstruct>().parentCell;
                        SelectCell(startCell);
                    }
                }
            }
        }
    if (Input.GetMouseButtonDown(1))
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Get the root object (parent) or use transform.root as needed
            GameObject root = hit.collider.transform.root.gameObject;
            int neutralLayer = LayerMask.NameToLayer("Neutral");
            int aiLayer = LayerMask.NameToLayer("Ai");

            if (root.GetComponentInParent<SpireConstruct>() != null)
            {
                if (root.layer == neutralLayer || root.layer == aiLayer)
                {
                        Debug.Log("Spire hit on root: " + root.name + ", Layer: " + LayerMask.LayerToName(root.layer));
                        SelectTargetCell(root.GetComponentInParent<SpireConstruct>().parentCell);
                }
            }
        }
    }
}

    public HexGrid grid; // Assign in Inspector
    private HexCell currentlySelectedCell;
    private HexCell currentlyTargetCell;
    private Color previousSelectedColor;
    private Color previousTargetColor;
    private Color selectedColor = Color.yellow;
    private Color targetColor = Color.yellow;

    private Color pathColor = Color.cyan;

    private Dictionary<HexCell, Color> previousPathCellColors = new Dictionary<HexCell, Color>();

    private List<LineRenderer> pathLineRenderers = new List<LineRenderer>();
    private Vector3[] currentPath;

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
            Debug.Log("Path found with " + path.Length + " waypoints.");
            currentPath = path;
            ColorPathCells();
            bool generated = UnitMgr.inst.generateUnits(currentPath[0] != null ? grid.GetCellFromWorldPosition(currentPath[0]) : null);
            // once unit are generated, give them move order to target cell
            bool moved = false;
            if (generated)
            {
                moved = UnitMgr.inst.moveUnitsAlongPath(currentPath);
            }
            if (!generated || !moved)
            {
                Debug.Log("Failed to generate or move units along path.");
                ClearPath();
            }
        }
        else
        {
            Debug.Log("Path not found!");
            currentPath = null;
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
    public void ClearSelection()
    {
        if (currentlySelectedCell != null)
        {
            currentlySelectedCell.SetColor(previousSelectedColor);
            currentlySelectedCell = null;
        }
        if (currentlyTargetCell != null)
        {
            currentlyTargetCell.SetColor(previousTargetColor);
            currentlyTargetCell = null;
        }
        ClearPath();
    }
}