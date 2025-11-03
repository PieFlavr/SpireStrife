// Author: Lucas Pinto
// Original Date: 2025-10-25
// Description:

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents a Spire construct on the hex grid.
/// Spires can be claimed by factions by committing units; claiming consumes units to increase a faction's claim progress.
/// Spires keep track of stationed ally units and can issue one command per turn.
/// </summary>
public class SpireConstruct : GridObject
{
    // Raised whenever this spire's team ownership changes via ClaimBy.
    // Args: (spire, oldTeamID, newTeamID)
    public static event System.Action<SpireConstruct, int, int> OwnershipChanged;
    // Raised when a spire is placed on the grid (created/added to a cell)
    public static event System.Action<SpireConstruct> SpirePlaced;
    // Raised when a spire is removed from the grid
    public static event System.Action<SpireConstruct> SpireRemoved;
    public enum OwnerType
    {
        Neutral = -1,
        Player = 0,
        AI = 1
    }

    [Header("Reserve Regeneration")]
    [Tooltip("How many units this spire generates per turn")]
    public int reserveRegenPerTurn = 10;

    [Tooltip("Maximum reserve capacity (0 = unlimited)")]
    public int maxReserve = 100;

    [Header("Spire Configuration")]
    [Tooltip("Number of claim points required for a faction to capture this Spire.")]
    public int costToClaim = 10;
    [Tooltip("If > 0, seed this many units as initial garrison when placed.")]
    public int initialGarrison = 100;

    [Header("Starting Ownership")]
    [Tooltip("Who owns this Spire when it is constructed/placed.")]
    public OwnerType startingOwner = OwnerType.Neutral;

    [Header("Visuals")] 
    [Tooltip("Color to use for Player-owned spires (team 0).")]
    public Color playerColor = Color.yellow;
    [Tooltip("Color to use for AI-owned spires (team 1).")]
    public Color aiColor = Color.blue;
    [Tooltip("Color to use for Neutral spires (team -1).")]
    public Color neutralColor = Color.white;

    /// <summary>
    /// Tracks accumulated claim progress per team (teamID -> progress).
    /// </summary>
    private readonly Dictionary<int, int> claimProgress = new Dictionary<int, int>();

    [Header("Debug")]
    public bool debugLogging = false;

    /// <summary>
    /// Unit groups currently garrisoned at this Spire.
    /// Kept reasonably in sync by RegisterGarrison / RefreshGarrisonFromCell.
    /// Only Stationed units are considered garrison.
    /// </summary>
    private readonly List<Units> garrisonedUnits = new List<Units>();

    /// <summary>
    /// Whether this Spire has issued a command this turn (one command per turn).
    /// </summary>
    private bool hasCommandedThisTurn = false;

    protected override void SetObjectType()
    {
        objectType = GridObjectType.Construct;
    }

    /// <summary>
    /// Ensure team and visuals are initialized even if this spire is placed in the scene
    /// without going through HexCell.TryAddGridObject (e.g., prefabs dropped in editor).
    /// This avoids default teamID=0 (Player) causing incorrect colors on first capture.
    /// </summary>
    protected override void InitializeGridObject()
    {
        base.InitializeGridObject();
        // If team was left at default, apply startingOwner now
        if (startingOwner == OwnerType.Player || startingOwner == OwnerType.AI || startingOwner == OwnerType.Neutral)
        {
            teamID = (int)startingOwner;
            ApplyTeamLayer(teamID);
            UpdateVisualsForTeam(teamID);
        }
    }

    private void OnValidate()
    {
        // Keep visuals/layer/team in sync when tweaking in the Inspector
        if (!Application.isPlaying)
        {
            teamID = (int)startingOwner;
        }
        ApplyTeamLayer(teamID);
        UpdateVisualsForTeam(teamID);
    }
    public int remainingGarrison;
    public void Start()
    {
        remainingGarrison = initialGarrison;    
    }
    /// <summary>
    /// Called when this spire is placed on a HexCell.
    /// Refreshes the local garrison list from the cell contents.
    /// </summary>
    public override void OnPlacedOnGrid(HexCell cell)
    {
        base.OnPlacedOnGrid(cell);

        // Set initial team based on startingOwner if not already set elsewhere
        // We treat any non -1/0/1 custom value as "already set" and leave it alone.
        if (startingOwner == OwnerType.Player || startingOwner == OwnerType.AI || startingOwner == OwnerType.Neutral)
        {
            teamID = (int)startingOwner;
            if (debugLogging) Debug.Log($"Spire placed: startingOwner={startingOwner}, teamID={teamID}");
        }

        // Sync layer and visuals based on current team
        ApplyTeamLayer(teamID);
        UpdateVisualsForTeam(teamID);

    RefreshGarrisonFromCell();

        // Optional: Seed initial garrison for quick testing / setups
        // Only auto-seed for non-neutral starting owners
        if (initialGarrison > 0 && GetTotalGarrisonCount() == 0 && teamID != (int)OwnerType.Neutral)
        {
            var seeded = SpawnGarrison(initialGarrison);
            if (seeded == null)
            {
                Debug.LogWarning("Failed to seed initial garrison: could not add Units to cell.");
            }
            else if (debugLogging)
            {
                Debug.Log($"Seeded initial garrison {initialGarrison} for team {teamID}");
            }
        }

        // Notify listeners that this spire has been placed/created
        SpirePlaced?.Invoke(this);
    }

    /// <summary>
    /// Called when the spire is removed from the grid.
    /// Clears internal lists.
    /// </summary>
    public override void OnRemovedFromGrid()
    {
        base.OnRemovedFromGrid();
        garrisonedUnits.Clear();
        claimProgress.Clear();
        // Notify listeners that this spire has been removed
        SpireRemoved?.Invoke(this);
    }

    /// <summary>
    /// Registers allied/owned Units on this cell. 
    /// Use when the spire is created or when external code needs to resync.
    /// </summary>
    public void RefreshGarrisonFromCell()
    {
        garrisonedUnits.Clear();
        if (parentCell == null) return;

        var unitsHere = parentCell.GetObjectsByType(GridObjectType.Unit)
            .OfType<Units>();

        foreach (var u in unitsHere)
        {
            // Prefer explicit owner link; fallback to same-team units on the cell
            if (u.owner == this || u.teamID == teamID)
            {
                RegisterGarrison(u);
            }
        }
    }

    /// <summary>
    /// Register a Units group as garrisoned here.
    /// Makes the unit group owned by this spire and sets state to Stationed.
    /// </summary>
    /// <param name="u">Units to register</param>
    public void RegisterGarrison(Units u)
    {
        if (u == null) return;
        if (!garrisonedUnits.Contains(u)) garrisonedUnits.Add(u);
        u.AttachToOwner(this);
    }

    /// <summary>
    /// Remove a Units group from this spire's garrison list.
    /// Does not destroy the unit group.
    /// </summary>
    /// <param name="u">Units to remove</param>
    public void UnregisterGarrison(Units u)
    {
        if (u == null) return;
        garrisonedUnits.Remove(u);
        if (u.owner == this) u.DetachFromOwner();
    }

    /// <summary>
    /// Returns the total number of stationed units currently garrisoned at this Spire.
    /// Counts only Stationed groups registered in garrisonedUnits.
    /// </summary>
    public int GetTotalGarrisonCount()
    {
        return garrisonedUnits.Where(u => u.state == Units.UnitState.Stationed).Sum(u => u.unitCount);
    }

    /// <summary>
    /// Issue a command to send units from this spire to a target spire along a provided path.
    /// The caller must provide a valid path to the destination (path[0] should be this spire's cell).
    /// </summary>
    /// <param name="sendCount">How many units to send</param>
    /// <param name="path">List of HexCells forming the route (including source and destination)</param>
    /// <param name="target">Target SpireConstruct</param>
    /// <returns>True if the command was accepted</returns>
    public bool CommandUnits(int sendCount, List<HexCell> path, SpireConstruct target, Vector3[] waypoints = null)
    {
        if (hasCommandedThisTurn)
        {
            Debug.LogWarning("Spire has already commanded units this turn");
            return false;
        }

        if (sendCount <= 0)
        {
            Debug.LogWarning("sendCount must be positive");
            return false;
        }

        if (parentCell == null)
        {
            Debug.LogError("Spire must be placed on a cell before issuing commands.");
            return false;
        }

        int available = GetTotalGarrisonCount();
        if (debugLogging) Debug.Log($"CommandUnits from team {teamID} -> target team {target?.teamID}, send={sendCount}, available={available}");
        if (available < sendCount)
        {
            Debug.LogWarning("Not enough units in garrison");
            return false;
        }

        // Deduct units from local garrison groups (consume counts; destroy empty groups)
        int remainingToRemove = sendCount;
        for (int i = garrisonedUnits.Count - 1; i >= 0 && remainingToRemove > 0; i--)
        {
            Units g = garrisonedUnits[i];
            if (g == null || g.state != Units.UnitState.Stationed) continue;

            int take = Mathf.Min(g.unitCount, remainingToRemove);
            g.unitCount -= take;
            remainingToRemove -= take;

            if (g.unitCount <= 0)
            {
                // remove empty group
                g.OnRemovedFromGrid();
                Destroy(g.gameObject);
                garrisonedUnits.RemoveAt(i);
            }
        }

        // Create new Units group representing the dispatched force
        GameObject unitObj = new GameObject($"Units_{teamID}");
        Units dispatched = unitObj.AddComponent<Units>();
    dispatched.unitCount = sendCount;
    dispatched.teamID = this.teamID;

        // Place on this spire's cell
        if (!parentCell.TryAddGridObject(dispatched))
        {
            Debug.LogError("Failed to place dispatched units on spire cell");
            Destroy(dispatched.gameObject);
            return false;
        }

        // Keep owner as source while traversing
    dispatched.AttachToOwner(this);
    if (debugLogging) Debug.Log($"Dispatched units created team={dispatched.teamID} count={dispatched.unitCount}");

        // Queue movement to target
        dispatched.QueueMovement(path, target);
        if (waypoints != null)
        {
            // Store waypoints; TurnManager will play animation on resolve.
            dispatched.SetPlannedWaypoints(waypoints);
        }

        hasCommandedThisTurn = true;
        return true;
    }

    /// <summary>
    /// Called when an arriving Units group reaches this spire.
    /// This method implements reinforcement (same-team) and claim logic (opposing team).
    /// Units used to advance a claim are destroyed. If claim completes, ownership switches to the claimant and any leftover arriving units become garrisoned.
    /// </summary>
    /// <param name="arriving">The arriving Units group</param>
    public void OnUnitsArrived(Units arriving)
    {
        if (arriving == null) return;
        if (debugLogging) Debug.Log($"OnUnitsArrived: arriving team={arriving.teamID}, spire team={teamID}, count={arriving.unitCount}");

        // Reinforcement for the owning team
        if (arriving.teamID == teamID)
        {
            RegisterGarrison(arriving);
            return;
        }

        // Opposition - apply claim progress
        int contribution = arriving.unitCount;
        if (contribution <= 0)
        {
            arriving.OnRemovedFromGrid();
            Destroy(arriving.gameObject);
            return;
        }

        int current = claimProgress.ContainsKey(arriving.teamID) ? claimProgress[arriving.teamID] : 0;
        int needed = costToClaim - current;

        if (contribution >= needed)
        {
            // Enough to capture: consume only what is necessary
            int used = needed;
            claimProgress[arriving.teamID] = current + used;
            arriving.unitCount -= used;

            // Claim the spire
            if (debugLogging) Debug.Log($"Claiming spire by team {arriving.teamID}");
            ClaimBy(arriving.teamID);

            // Leftover units (if any) become garrisoned at the new owner
            if (arriving.unitCount > 0)
            {
                RegisterGarrison(arriving);
            }
            else
            {
                arriving.OnRemovedFromGrid();
                Destroy(arriving.gameObject);
            }
        }
        else
        {
            // Not enough to claim yet: consume all arriving units toward progress, destroy group
            claimProgress[arriving.teamID] = current + contribution;
            arriving.OnRemovedFromGrid();
            Destroy(arriving.gameObject);
        }
    }

    /// <summary>
    /// Perform ownership transfer to a new team and reset claim state.
    /// </summary>
    /// <param name="newTeamID">ID of the claiming team</param>
    private void ClaimBy(int newTeamID)
    {
        int oldTeam = teamID;
        teamID = newTeamID;
        claimProgress.Clear();
        if (debugLogging) Debug.Log($"Spire at {GridPosition} claimed by Team {newTeamID}");
        // Update layer and visuals for UI selection and interaction
        ApplyTeamLayer(newTeamID);
        UpdateVisualsForTeam(newTeamID);
        // Notify listeners (e.g., GameMgr) that ownership changed
        OwnershipChanged?.Invoke(this, oldTeam, newTeamID);
        // (Optional) Notify visuals / UI / game manager here.
    }

    /// <summary>
    /// Reset per-turn state; call at start of each new turn.
    /// </summary>
    public void ResetTurn()
    {
        hasCommandedThisTurn = false;
        RegenerateReserves();
    }

    /// <summary>
    /// Read-only snapshot of claim progress (useful for UI
    /// </summary>
    public IReadOnlyDictionary<int, int> GetClaimProgress()
    {
        return claimProgress; // Shouldn't have weird side effects? Apparently best practice for this stuff.
    }

    /// <summary>
    /// Read-only snapshot of currently garrisoned unit groups (useful for UI).
    /// </summary>
    public IReadOnlyList<Units> GetGarrisonedUnits()
    {
        return garrisonedUnits.AsReadOnly();
    }

    /// <summary>
    /// Spawns a new garrison Units group on this spire's cell and registers it.
    /// </summary>
    public Units SpawnGarrison(int count)
    {
        if (count <= 0 || parentCell == null) return null;
        GameObject unitObj = new GameObject($"Units_{teamID}");
        Units u = unitObj.AddComponent<Units>();
        u.unitCount = count;
        u.teamID = this.teamID;
        if (parentCell.TryAddGridObject(u))
        {
            RegisterGarrison(u);
            return u;
        }
        Destroy(unitObj);
        return null;
    }

   
    public int GetRemainingClaimCostForTeam(int contributorTeamID)
    {
        int current = claimProgress.ContainsKey(contributorTeamID) ? claimProgress[contributorTeamID] : 0;
        return Mathf.Max(0, costToClaim - current);
    }

    /// <summary>
    /// Update the GameObject's layer based on team for selection/interaction filters.
    /// Expects layers named "Player", "Ai", and optionally "Default" for neutral.
    /// </summary>
    private void ApplyTeamLayer(int team)
    {
        string layerName = team == (int)OwnerType.Player
            ? "Player"
            : team == (int)OwnerType.AI
                ? "Ai"
                : "Neutral"; // Neutral layer explicitly
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Set simple color tint based on team. Attempts to find a Renderer on this object or its children.
    /// If no renderer found, does nothing.
    /// </summary>
    private void UpdateVisualsForTeam(int team)
    {
        Color c = team == (int)OwnerType.Player
            ? playerColor
            : team == (int)OwnerType.AI
                ? aiColor
                : neutralColor;

        // Try to tint all renderers safely
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            // SpriteRenderer special-case
            if (r is SpriteRenderer sr)
            {
                sr.color = c;
                continue;
            }

            var mat = r.sharedMaterial; // do not instantiate
            if (mat != null)
            {
                // Use MPB when possible; pick property based on shader
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                if (mat.HasProperty("_BaseColor"))
                {
                    mpb.SetColor("_BaseColor", c);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mpb.SetColor("_Color", c);
                }
                else
                {
                    // Fallback: set via material color (may instance the material)
                    try { r.material.color = c; } catch { /* ignore */ }
                    r.SetPropertyBlock(mpb);
                    continue;
                }
                r.SetPropertyBlock(mpb);
            }
        }
    }

    public void CaptureSpire(int unitsCount, int teamID)
    {
        if (unitsCount <= 0) return;

        if (teamID != this.teamID)
        {
            remainingGarrison -= unitsCount;

            if (remainingGarrison <= 0)
            {
                // Capture the spire
                ClaimBy(teamID);
                remainingGarrison = Mathf.Abs(remainingGarrison); // Start adding if negative
            }
        }
        else
        {
            remainingGarrison += unitsCount;
        }
    }

    /// <summary>
/// Regenerates reserve units for this spire.
/// </summary>
    private void RegenerateReserves()
    {
        if (reserveRegenPerTurn <= 0)
            return;
        
        // Only regenerate for owned spires (not neutral)
        if (teamID == (int)OwnerType.Neutral)
            return;
        
        int oldReserve = remainingGarrison;
        remainingGarrison += reserveRegenPerTurn;
        
        // Cap at max if specified
        if (maxReserve > 0)
        {
            remainingGarrison = Mathf.Min(remainingGarrison, maxReserve);
        }
        
        if (debugLogging)
        {
            Debug.Log($"[Spire] {name} regenerated reserves: {oldReserve} -> {remainingGarrison} (+{reserveRegenPerTurn})");
        }
}
}