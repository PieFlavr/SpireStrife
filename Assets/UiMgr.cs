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
    
    /// <summary>
    /// Singleton instance of UiMgr. Handles player input and command issuing.
    /// </summary>
    public static UiMgr Instance { get; private set; }
    
    // Legacy compatibility - will be removed in future
    public static UiMgr inst => Instance;
    
    public SpireConstruct selectedSpire;
    public SpireConstruct targetSpire;

    [Header("Dev/Test Settings")]
    [Tooltip("If true, automatically generate units at the source spire for each command.")]
    public bool alwaysGenerateOnCommand = true;
    [Tooltip("Number of units to auto-generate when issuing a command (if enabled).")]
    public int generateCountPerCommand = 10;

    // === AI Integration Overrides ===
    // When true, allow issuing a command even when PlayerInputEnabled is false (AI phase)
    [HideInInspector] public bool allowCommandWhenPlayerInputDisabled = false;
    // When set, overrides how many units to spawn/send on the next command
    [HideInInspector] public int? sendOverrideForNextCommand = null;
    
    public void Awake()
    {
        // Enforce singleton pattern - destroy duplicates
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[UiMgr] Duplicate instance detected, destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    private void OnDestroy()
    {
        // Clean up singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
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
        // === MODIFIED FIX ===
        // Only allow input if the TurnManager exists AND it's the player's planning phase.
        // If the manager is missing (inst == null) OR it's not the player's turn, do nothing.
        if (TurnManager.inst == null || !TurnManager.inst.PlayerInputEnabled)
        {
            return;
        }
        // === END FIX ===

        // --- At this point, we know TurnManager.inst exists AND PlayerInputEnabled is true ---

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
        UIIntegrationHelper.OnCellSelected(cell);
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
        UIIntegrationHelper.OnCellSelected(cell);
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
        // === ADDED FIX ===
        // Check if player input is still allowed. If not (e.g., turn ended
        // while path was being calculated), abort this move.
        if (TurnManager.inst != null && !TurnManager.inst.PlayerInputEnabled && !allowCommandWhenPlayerInputDisabled)
        {
            Debug.LogWarning("OnPathFound: Path was found, but player input is no longer enabled. Aborting move.");
            ClearPath();
            return;
        }
        // === END FIX ===

        if (!pathSuccessful)
        {
            Debug.Log("Path not found!");
            currentPath = null;
            ClearPath();
            return;
        }

        Debug.Log($"Path found with {path.Length} waypoints.");
        currentPath = path;
        ColorPathCells();

        // Convert to cell path
        List<HexCell> cellPath = path
            .Select(pos => grid.GetCellFromWorldPosition(pos))
            .Where(c => c != null)
            .ToList();

        // Use cached spire references from click selection; fall back to lookup if needed
        SpireConstruct startSpire = selectedSpire ?? currentlySelectedCell?.GetComponentInChildren<SpireConstruct>();
        SpireConstruct destSpire = targetSpire ?? currentlyTargetCell?.GetComponentInChildren<SpireConstruct>();

        if (startSpire == null || destSpire == null)
        {
            Debug.LogError("Missing SpireConstruct on selected/target cells.");
            ClearPath();
            return;
        }

        int send = 0;
        int? desiredOverride = sendOverrideForNextCommand;
        if (alwaysGenerateOnCommand)
        {
            int reserve = startSpire.remainingGarrison;
            if (reserve <= 0)
            {
                Debug.LogWarning("No reserve remaining at source spire to generate units.");
                ClearPath();
                return;
            }

            int targetToSpawn = generateCountPerCommand;
            if (desiredOverride.HasValue)
            {
                targetToSpawn = Mathf.Min(desiredOverride.Value, reserve);
            }
            int toSpawn = Mathf.Min(targetToSpawn, reserve);
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

            startSpire.remainingGarrison -= toSpawn;
            if (desiredOverride.HasValue)
                send = Mathf.Min(desiredOverride.Value, startSpire.GetTotalGarrisonCount());
            else
                send = Mathf.Min(startSpire.GetTotalGarrisonCount(), toSpawn);
        }
        else
        {
            if (desiredOverride.HasValue)
                send = Mathf.Min(desiredOverride.Value, startSpire.GetTotalGarrisonCount());
            else
                send = startSpire.GetTotalGarrisonCount();
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
            return;
        }

        // If the player issued this command during planning, end the player's turn.
        // For AI-issued commands (during AI phase), do not end the player's turn here.
        if (TurnManager.inst == null || TurnManager.inst.PlayerInputEnabled)
        {
            TurnManager.inst?.EndPlayerTurn();
        }

        // Reset AI overrides after issuing the command
        allowCommandWhenPlayerInputDisabled = false;
        sendOverrideForNextCommand = null;
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
        UIIntegrationHelper.OnCellDeselected();
        ClearPath();
    }
}