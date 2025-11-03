using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Displays detailed information about a selected hex cell.
/// Shows terrain, constructs, units, ownership, and traversal costs.
/// Attach to a Canvas prefab with TextMeshPro components.
/// </summary>
public class CellDetailsPanel : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("Main panel GameObject to show/hide")]
    public GameObject panelRoot;

    [Header("Cell Information")]
    [Tooltip("Display cell coordinates")]
    public TextMeshProUGUI coordinatesText;
    [Tooltip("Display terrain type and properties")]
    public TextMeshProUGUI terrainInfoText;
    [Tooltip("Display traversal cost")]
    public TextMeshProUGUI traversalCostText;
    [Tooltip("Display if cell is walkable")]
    public TextMeshProUGUI walkableStatusText;

    [Header("Construct Information")]
    [Tooltip("Display construct name and type")]
    public TextMeshProUGUI constructNameText;
    [Tooltip("Display construct ownership")]
    public TextMeshProUGUI constructOwnerText;
    [Tooltip("Display spire-specific details")]
    public TextMeshProUGUI spireDetailsText;

    [Header("Unit Information")]
    [Tooltip("Display all units on this cell")]
    public TextMeshProUGUI unitsListText;
    [Tooltip("Display total unit count")]
    public TextMeshProUGUI unitCountText;

    [Header("Claim Progress (for Spires)")]
    [Tooltip("Display player claim progress")]
    public TextMeshProUGUI playerClaimText;
    [Tooltip("Display AI claim progress")]
    public TextMeshProUGUI aiClaimText;
    [Tooltip("Visual bar for player claim (optional)")]
    public Image playerClaimBar;
    [Tooltip("Visual bar for AI claim (optional)")]
    public Image aiClaimBar;

    [Header("Visual Feedback")]
    [Tooltip("Background image to tint based on ownership")]
    public Image panelBackground;
    [Tooltip("Color for player-owned cells")]
    public Color playerColor = new Color(1f, 0.9f, 0.2f, 0.8f);
    [Tooltip("Color for AI-owned cells")]
    public Color aiColor = new Color(0.2f, 0.5f, 1f, 0.8f);
    [Tooltip("Color for neutral cells")]
    public Color neutralColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);

    [Header("Update Settings")]
    [Tooltip("Auto-update while a cell is selected")]
    public bool autoUpdate = true;
    [Tooltip("Update interval in seconds")]
    [Min(0.05f)]
    public float updateInterval = 0.2f;

    private HexCell currentCell;
    private float timeSinceUpdate = 0f;

    private void Start()
    {
        HidePanel();
    }

    private void Update()
    {
        if (autoUpdate && currentCell != null)
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                RefreshDisplay();
                timeSinceUpdate = 0f;
            }
        }
    }

    /// <summary>
    /// Display information for the specified cell.
    /// </summary>
    public void ShowCellDetails(HexCell cell)
    {
        if (cell == null)
        {
            HidePanel();
            return;
        }

        currentCell = cell;
        ShowPanel();
        RefreshDisplay();
    }

    /// <summary>
    /// Hide the details panel.
    /// </summary>
    public void HidePanel()
    {
        currentCell = null;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    /// <summary>
    /// Refresh all displayed information for the current cell.
    /// </summary>
    [ContextMenu("Refresh Display")]
    public void RefreshDisplay()
    {
        if (currentCell == null) return;

        // Display coordinates
        UpdateText(coordinatesText, $"Cell: ({currentCell.axial_coords.x}, {currentCell.axial_coords.y})");

        // Terrain information
        DisplayTerrainInfo();

        // Construct information
        DisplayConstructInfo();

        // Unit information
        DisplayUnitInfo();

        // Update visual feedback
        UpdatePanelVisuals();
    }

    private void DisplayTerrainInfo()
    {
        var tile = currentCell.GetObjectsByType(GridObjectType.Tile).FirstOrDefault() as Tile;

        if (tile != null)
        {
            string terrainType = tile.GetType().Name;
            terrainType = terrainType.Replace("Tile", ""); // Clean up name
            if (string.IsNullOrEmpty(terrainType)) terrainType = "Basic";

            UpdateText(terrainInfoText, $"Terrain: {terrainType}");

            float moveCost = tile.GetMovementCost();
            string costDisplay = float.IsPositiveInfinity(moveCost) ? "Impassable" : moveCost.ToString("F1");
            UpdateText(traversalCostText, $"Traversal Cost: {costDisplay}");

            bool walkable = tile.CanEnter();
            UpdateText(walkableStatusText, $"Walkable: {(walkable ? "Yes" : "No")}");
        }
        else
        {
            UpdateText(terrainInfoText, "Terrain: None");
            UpdateText(traversalCostText, "Traversal Cost: N/A");
            UpdateText(walkableStatusText, $"Walkable: {(currentCell.isWalkable ? "Yes" : "No")}");
        }
    }

    private void DisplayConstructInfo()
    {
        var constructs = currentCell.GetObjectsByType(GridObjectType.Construct);

        if (constructs.Count == 0)
        {
            UpdateText(constructNameText, "No Constructs");
            UpdateText(constructOwnerText, "");
            UpdateText(spireDetailsText, "");
            ClearClaimBars();
            return;
        }

        var spire = constructs.FirstOrDefault(c => c is SpireConstruct) as SpireConstruct;

        if (spire != null)
        {
            UpdateText(constructNameText, "Construct: Spire");
            UpdateText(constructOwnerText, $"Owner: {GetTeamName(spire.teamID)}");

            // Spire-specific details
            int garrison = spire.GetTotalGarrisonCount();
            int reserve = spire.remainingGarrison;
            int cost = spire.costToClaim;

            string details = $"Garrison: {garrison}\nReserve: {reserve}\nClaim Cost: {cost}";
            UpdateText(spireDetailsText, details);

            // Display claim progress
            DisplayClaimProgress(spire);
        }
        else
        {
            var construct = constructs[0];
            UpdateText(constructNameText, $"Construct: {construct.GetType().Name}");
            UpdateText(constructOwnerText, $"Owner: {GetTeamName(construct.teamID)}");
            UpdateText(spireDetailsText, "");
            ClearClaimBars();
        }
    }

    private void DisplayClaimProgress(SpireConstruct spire)
    {
        if (TurnManager.inst == null) return;

        int playerTeam = TurnManager.inst.playerTeamId;
        int aiTeam = TurnManager.inst.aiTeamId;

        var progress = spire.GetClaimProgress();
        int playerClaim = progress.ContainsKey(playerTeam) ? progress[playerTeam] : 0;
        int aiClaim = progress.ContainsKey(aiTeam) ? progress[aiTeam] : 0;
        int cost = spire.costToClaim;

        UpdateText(playerClaimText, $"Player: {playerClaim}/{cost}");
        UpdateText(aiClaimText, $"AI: {aiClaim}/{cost}");

        // Update visual bars
        if (playerClaimBar != null)
            playerClaimBar.fillAmount = cost > 0 ? (float)playerClaim / cost : 0f;
        if (aiClaimBar != null)
            aiClaimBar.fillAmount = cost > 0 ? (float)aiClaim / cost : 0f;
    }

    private void ClearClaimBars()
    {
        UpdateText(playerClaimText, "");
        UpdateText(aiClaimText, "");
        if (playerClaimBar != null) playerClaimBar.fillAmount = 0f;
        if (aiClaimBar != null) aiClaimBar.fillAmount = 0f;
    }

    private void DisplayUnitInfo()
    {
        var units = currentCell.GetObjectsByType(GridObjectType.Unit).OfType<Units>().ToList();

        if (units.Count == 0)
        {
            UpdateText(unitsListText, "No Units");
            UpdateText(unitCountText, "Total: 0");
            return;
        }

        int totalUnits = units.Sum(u => u.unitCount);
        UpdateText(unitCountText, $"Total: {totalUnits}");

        // Group by team and state
        var grouped = units.GroupBy(u => (u.teamID, u.state));
        List<string> unitDescriptions = new List<string>();

        foreach (var group in grouped.OrderBy(g => g.Key.teamID))
        {
            int count = group.Sum(u => u.unitCount);
            string team = GetTeamName(group.Key.teamID);
            string state = group.Key.state.ToString();
            unitDescriptions.Add($"{team} {state}: {count}");
        }

        UpdateText(unitsListText, string.Join("\n", unitDescriptions));
    }

    private void UpdatePanelVisuals()
    {
        if (panelBackground == null) return;

        // Color based on dominant ownership
        var constructs = currentCell.GetObjectsByType(GridObjectType.Construct);
        var spire = constructs.FirstOrDefault(c => c is SpireConstruct) as SpireConstruct;

        if (spire != null)
        {
            panelBackground.color = GetTeamColor(spire.teamID);
        }
        else if (constructs.Count > 0)
        {
            panelBackground.color = GetTeamColor(constructs[0].teamID);
        }
        else
        {
            panelBackground.color = neutralColor;
        }
    }

    private string GetTeamName(int teamID)
    {
        if (TurnManager.inst == null) return $"Team {teamID}";

        if (teamID == TurnManager.inst.playerTeamId) return "Player";
        if (teamID == TurnManager.inst.aiTeamId) return "AI";
        if (teamID == (int)SpireConstruct.OwnerType.Neutral) return "Neutral";

        return $"Team {teamID}";
    }

    private Color GetTeamColor(int teamID)
    {
        if (TurnManager.inst == null) return neutralColor;

        if (teamID == TurnManager.inst.playerTeamId) return playerColor;
        if (teamID == TurnManager.inst.aiTeamId) return aiColor;
        return neutralColor;
    }

    private void UpdateText(TextMeshProUGUI textComponent, string content)
    {
        if (textComponent != null)
        {
            textComponent.text = content;
        }
    }

    /// <summary>
    /// Call this from UiMgr or other input handlers when a cell is selected.
    /// </summary>
    public static void ShowDetailsForCell(HexCell cell)
    {
        var panel = FindObjectOfType<CellDetailsPanel>();
        if (panel != null)
        {
            panel.ShowCellDetails(cell);
        }
    }
}