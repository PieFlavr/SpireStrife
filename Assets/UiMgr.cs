using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class UiMgr : MonoBehaviour
{
    // Start is called before the first frame update
    public HexCell startCell;
    public HexCell targetCell;
    public static UiMgr inst;
    public SpireConstruct selectedSpire;
    public SpireConstruct targetSpire;

    [Header("Dev/Test Settings")]
    [Tooltip("If true, automatically generate units at the source spire for each command.")]
    public bool alwaysGenerateOnCommand = true;
    [Tooltip("Number of units to auto-generate when issuing a command (if enabled).")]
    public int generateCountPerCommand = 10;
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
        // Disable input when it's not the player's planning phase
        if (TurnManager.inst != null && !TurnManager.inst.PlayerInputEnabled)
        {
            return;
        }
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
                        selectedSpire = root.GetComponentInParent<SpireConstruct>();
                        startCell = selectedSpire != null ? selectedSpire.parentCell : null;
                        SelectCell(startCell);
                    }
                }
            }
        }
    if (Input.GetMouseButtonDown(1))
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit) && selectedSpire != null)
        {
            // Get the root object (parent) or use transform.root as needed
            GameObject root = hit.collider.transform.root.gameObject;
            int neutralLayer = LayerMask.NameToLayer("Neutral");
            int aiLayer = LayerMask.NameToLayer("Ai");

            if (root.GetComponentInParent<SpireConstruct>() != null)
            {
                if (root.layer == neutralLayer || root.layer == aiLayer || root.layer == LayerMask.NameToLayer("Player"))
                    {
                        Debug.Log("Spire hit on root: " + root.name + ", Layer: " + LayerMask.LayerToName(root.layer));
                        targetSpire = root.GetComponentInParent<SpireConstruct>();
                        if (targetSpire == selectedSpire)
                        {
                            Debug.Log("Cannot target the same spire as the source.");
                            return;
                        }
                        SelectTargetCell(targetSpire != null ? targetSpire.parentCell : null);
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

    public void SelectCell(HexCell cell)
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

    private void ClearPath()
    {
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

    public void SelectTargetCell(HexCell cell)
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

    private void OnPathFound(Vector3[] path, bool pathSuccessful)
    {
        if (pathSuccessful)
        {
            Debug.Log("Path found with " + path.Length + " waypoints.");
            currentPath = path;
            ColorPathCells();

            // Convert to cell path
            List<HexCell> cellPath = new List<HexCell>();
            foreach (var pos in path)
            {
                var c = grid.GetCellFromWorldPosition(pos);
                if (c != null) cellPath.Add(c);
            }

            // Use cached spire references from click selection; fall back to lookup if needed
            SpireConstruct startSpire = selectedSpire;
            SpireConstruct destSpire = targetSpire;
            if (startSpire == null && currentlySelectedCell != null)
            {
                startSpire = currentlySelectedCell
                    .GetComponentInChildren<SpireConstruct>();
            }
            if (destSpire == null && currentlyTargetCell != null)
            {
                destSpire = currentlyTargetCell
                    .GetComponentInChildren<SpireConstruct>();
            }

            if (startSpire != null && destSpire != null)
            {
                // Determine how many units are needed to claim, including travel losses.
                int playerTeam = TurnManager.inst != null ? TurnManager.inst.playerTeamId : 0;
                int needToClaim = destSpire.GetRemainingClaimCostForTeam(playerTeam);

                // Sum per-step movement loss: loss = steps = waypoints - 1
                int travelLoss = Mathf.Max(0, path.Length - 1);

                // Units needed = claim + travel loss
                int desiredSend = Mathf.Max(1, needToClaim + travelLoss);

                int send = 0;
                if (alwaysGenerateOnCommand)
                {
                    // Consume from the spire's remaining reserve when generating.
                    int reserve = startSpire.remainingGarrison;
                    if (reserve <= 0)
                    {
                        Debug.LogWarning("No reserve remaining at source spire to generate units.");
                        ClearPath();
                        return;
                    }

                    // Ensure we spawn enough to meet either desiredSend or the configured minimum per command.
                    int minToSendThisCommand = generateCountPerCommand > 0 ? Mathf.Max(desiredSend, generateCountPerCommand) : desiredSend;

                    // If reserve is less than generateCountPerCommand, send all remaining; otherwise cap by reserve.
                    int toSpawn = reserve < generateCountPerCommand ? reserve : Mathf.Min(minToSendThisCommand, reserve);
                    if (toSpawn <= 0)
                    {
                        Debug.LogWarning("Nothing to spawn after applying reserve constraints.");
                        ClearPath();
                        return;
                    }

                    var spawned = startSpire.SpawnGarrison(toSpawn);
                    if (spawned == null)
                    {
                        Debug.LogWarning("Failed to spawn units at source spire.");
                        ClearPath();
                        return;
                    }

                    // Reduce remaining reserve by the amount generated
                    startSpire.remainingGarrison -= toSpawn;

                    int available = startSpire.GetTotalGarrisonCount();
                    // Send the generated amount (bounded by what's available on the cell)
                    send = Mathf.Min(available, toSpawn);
                }
                else
                {
                    // Use existing garrison only
                    int available = startSpire.GetTotalGarrisonCount();
                    send = Mathf.Min(available, desiredSend);
                    if (send <= 0)
                    {
                        Debug.LogWarning("No units available at source spire.");
                        ClearPath();
                        return;
                    }
                }

                // Issue the command
                bool commanded = startSpire.CommandUnits(send, cellPath, destSpire, path);
                if (!commanded)
                {
                    Debug.Log("Failed to command units.");
                    ClearPath();
                }
                else
                {
                    // Immediately resolve player turn (no queue): player then AI then back to player
                    if (TurnManager.inst != null)
                    {
                        TurnManager.inst.EndPlayerTurn();
                    }
                }
            }
            else
            {
                Debug.LogError("Missing SpireConstruct on selected/target cells.");
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

    
    private void ColorPathCells()
    {
        if (currentPath == null || grid == null) return;
        previousPathCellColors.Clear();

        for (int i = 0; i < currentPath.Length; i++)
        {
            HexCell cell = grid.GetCellFromWorldPosition(currentPath[i]);
            if (cell != null && cell != currentlySelectedCell && cell != currentlyTargetCell)
            {
                if (!previousPathCellColors.ContainsKey(cell))
                    previousPathCellColors[cell] = cell.GetColor();
                cell.SetColor(pathColor);
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
        selectedSpire = null;
        targetSpire = null;
        ClearPath();
    }
}