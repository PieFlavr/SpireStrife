using UnityEngine;

/// <summary>
/// Helper script to integrate GameScoreboard and CellDetailsPanel with your existing systems.
/// Attach to a persistent GameObject in your scene (like TurnManager or a dedicated UIManager).
/// Handles UI instantiation, updates, and coordination with game events.
/// </summary>
public class UIIntegrationHelper : MonoBehaviour
{
    [Header("UI Prefabs")]
    [Tooltip("Assign the GameScoreboard prefab")]
    public GameObject scoreboardPrefab;
    [Tooltip("Assign the CellDetailsPanel prefab")]
    public GameObject cellDetailsPrefab;

    [Header("Runtime References")]
    [Tooltip("Created at runtime, or assign manually if already in scene")]
    public GameScoreboard scoreboard;
    [Tooltip("Created at runtime, or assign manually if already in scene")]
    public CellDetailsPanel cellDetailsPanel;

    [Header("Auto-Setup")]
    [Tooltip("Automatically instantiate prefabs on Start")]
    public bool autoInstantiate = true;
    [Tooltip("Don't destroy UI when loading new scenes")]
    public bool persistAcrossScenes = true;

    private static UIIntegrationHelper instance;

    private void Awake()
    {
        // Singleton pattern for easy access
        if (instance == null)
        {
            instance = this;
            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Find existing UI in scene if not instantiating
        if (!autoInstantiate)
        {
            if (scoreboard == null)
                scoreboard = FindObjectOfType<GameScoreboard>();
            if (cellDetailsPanel == null)
                cellDetailsPanel = FindObjectOfType<CellDetailsPanel>();
        }
    }

    private void Start()
    {
        if (autoInstantiate)
        {
            InstantiateUI();
        }

        // Subscribe to turn events if TurnManager exists
        if (TurnManager.inst != null)
        {
            // You can add event subscriptions here when turns change
            // For now, the scoreboard auto-refreshes via SpireConstruct events
        }
    }

    private void InstantiateUI()
    {
        // Create scoreboard if needed
        if (scoreboard == null && scoreboardPrefab != null)
        {
            GameObject scoreObj = Instantiate(scoreboardPrefab, transform);
            scoreboard = scoreObj.GetComponent<GameScoreboard>();
            if (scoreboard != null)
            {
                Debug.Log("UIIntegrationHelper: Scoreboard instantiated");
            }
        }

        // Create cell details panel if needed
        if (cellDetailsPanel == null && cellDetailsPrefab != null)
        {
            GameObject detailsObj = Instantiate(cellDetailsPrefab, transform);
            cellDetailsPanel = detailsObj.GetComponent<CellDetailsPanel>();
            if (cellDetailsPanel != null)
            {
                cellDetailsPanel.HidePanel(); // Start hidden
                Debug.Log("UIIntegrationHelper: Cell Details Panel instantiated");
            }
        }
    }

    /// <summary>
    /// Call this from your input handler when a cell is clicked/selected.
    /// Integrates with your existing UiMgr cell selection.
    /// </summary>
    public static void OnCellSelected(HexCell cell)
    {
        if (instance == null || instance.cellDetailsPanel == null) return;
        instance.cellDetailsPanel.ShowCellDetails(cell);
    }

    /// <summary>
    /// Call this when deselecting cells or closing selection UI.
    /// </summary>
    public static void OnCellDeselected()
    {
        if (instance == null || instance.cellDetailsPanel == null) return;
        instance.cellDetailsPanel.HidePanel();
    }

    /// <summary>
    /// Force refresh the scoreboard (useful after major game events).
    /// </summary>
    public static void RefreshScoreboard()
    {
        if (instance == null || instance.scoreboard == null) return;
        instance.scoreboard.RefreshScoreboard();
    }

    /// <summary>
    /// Toggle the cell details panel visibility.
    /// </summary>
    public static void ToggleCellDetails()
    {
        if (instance == null || instance.cellDetailsPanel == null) return;
        
        if (instance.cellDetailsPanel.panelRoot.activeSelf)
            instance.cellDetailsPanel.HidePanel();
        else if (instance.cellDetailsPanel != null)
            instance.cellDetailsPanel.RefreshDisplay();
    }

    /// <summary>
    /// Get the current scoreboard stats as a string (for logging or debug UI).
    /// </summary>
    public static string GetGameStatsString()
    {
        if (instance == null || instance.scoreboard == null) return "No scoreboard";
        return instance.scoreboard.GetStatsString();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    #region Input Integration Examples

    /// <summary>
    /// Example: Add this to your UiMgr.SelectCell method
    /// </summary>
    private void ExampleIntegrationWithUiMgr()
    {
        // In UiMgr.cs SelectCell method, add:
        // UIIntegrationHelper.OnCellSelected(cell);

        // In UiMgr.cs when deselecting or closing, add:
        // UIIntegrationHelper.OnCellDeselected();
    }

    /// <summary>
    /// Example: Keyboard shortcut to toggle details panel
    /// Add this to your Update() in a control script
    /// </summary>
    private void ExampleKeyboardShortcut()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            UIIntegrationHelper.ToggleCellDetails();
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log(UIIntegrationHelper.GetGameStatsString());
        }
    }

    #endregion
}

// ============================================================================
// INTEGRATION GUIDE: Add these lines to your existing UiMgr.cs
// ============================================================================

/* 

// At the top of UiMgr.cs, after other using statements:
// (no additional using needed)

// In UiMgr.SelectCell method:
public void SelectCell(HexCell cell)
{
    // ... your existing selection logic ...
    selectedCell = cell;
    
    // ADD THIS LINE:
    UIIntegrationHelper.OnCellSelected(cell);
}

// In UiMgr when deselecting (wherever you clear selection):
public void DeselectAll()
{
    // ... your existing deselect logic ...
    selectedCell = null;
    
    // ADD THIS LINE:
    UIIntegrationHelper.OnCellDeselected();
}

// Optional: In TurnManager.StartTurn or TurnManager.EndTurn:
public void EndTurn()
{
    // ... your existing turn logic ...
    
    // ADD THIS LINE for immediate scoreboard update:
    UIIntegrationHelper.RefreshScoreboard();
}

*/