# Code Readability Improvements for SpireStrife

This document contains specific examples of how to improve code readability throughout your project.

---

## 1. Singleton Pattern (Apply to ALL Manager classes)

### ❌ Current Pattern (Inconsistent & Error-Prone)
```csharp
public class GameMgr : MonoBehaviour
{
    public static GameMgr inst;
    
    private void Awake()
    {
        inst = this;
    }
}
```

**Problems**:
- No protection against duplicates
- Public static field can be reassigned
- Name `inst` is unclear
- No lifecycle management

### ✅ Improved Pattern
```csharp
public class GameMgr : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of GameMgr. Manages spire ownership lists and unit counts.
    /// </summary>
    public static GameMgr Instance { get; private set; }
    
    private void Awake()
    {
        // Enforce singleton pattern - destroy duplicates
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GameMgr] Duplicate instance detected, destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Optional: Make persistent across scenes
        // DontDestroyOnLoad(gameObject);
    }
    
    private void OnDestroy()
    {
        // Clean up singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
```

**Apply this pattern to**: GameMgr, ScoreMgr, TurnManager, UiMgr, UnitMgr, MinimaxAI, PathRequestManager

---

## 2. XML Documentation Comments

### ❌ Current (No Documentation)
```csharp
public static int CalculateRemainingUnits(List<HexCell> path, int startingCount)
{
    if (path == null || path.Length <= 1)
        return startingCount;
    // ...
}
```

### ✅ Improved (Clear Documentation)
```csharp
/// <summary>
/// Calculates how many units will survive traveling along a path.
/// Units are lost based on tile traversal costs (1 per tile).
/// Does NOT account for combat at destination.
/// </summary>
/// <param name="path">Complete path from source to destination (includes both endpoints)</param>
/// <param name="startingCount">Initial number of units in the group</param>
/// <returns>Number of units remaining at destination, or -1 if path is invalid (blocked tiles)</returns>
/// <example>
/// // 10 units traveling 7 tiles will arrive with 3 units (10 - 7 = 3)
/// int survivors = Units.CalculateRemainingUnits(pathCells, 10);
/// </example>
public static int CalculateRemainingUnits(List<HexCell> path, int startingCount)
{
    if (path == null || path.Count <= 1)
        return startingCount;
        
    int remainingUnits = startingCount;
    
    // Skip first cell (starting position - no cost to be there)
    for (int i = 1; i < path.Count; i++)
    {
        Tile tile = path[i].GetObjectsByType(GridObjectType.Tile).FirstOrDefault() as Tile;
        
        if (tile == null || !tile.CanEnter())
            return -1; // Path blocked
            
        // Units lost = ceil of traversal cost
        int unitsLost = Mathf.CeilToInt(tile.GetMovementCost());
        remainingUnits -= unitsLost;
        
        if (remainingUnits <= 0)
            return 0; // All units eliminated during travel
    }
    
    return remainingUnits;
}
```

**Benefits**:
- IntelliSense shows documentation
- Clear parameter descriptions
- Example usage included
- Return value explained

---

## 3. Magic Numbers → Named Constants

### ❌ Current (Magic Numbers Everywhere)
```csharp
// In UiMgr.cs
public int generateCountPerCommand = 10;

// In MinimaxAI.cs
private const int MAX_SEND_AMOUNT = 20;

// In MoveEntity.cs
int toSpawn = Mathf.Min(maxSend, Mathf.Max(0, from.Reserve));
```

### ✅ Improved (Named Constants)
```csharp
// In GameConstants.cs (new file)
public static class GameConstants
{
    // Unit Generation
    public const int DEFAULT_UNITS_PER_COMMAND = 10;
    public const int MAX_UNITS_PER_SEND = 20;
    public const int MIN_UNITS_FOR_ACTION = 1;
    
    // Attrition
    public const float ATTRITION_RATE = 1.0f; // Units lost per tile
    
    // AI
    public const int MIN_AI_SEARCH_DEPTH = 1;
    public const int MAX_AI_SEARCH_DEPTH = 7;
    public const int DEFAULT_AI_SEARCH_DEPTH = 3;
    
    // Spires
    public const int DEFAULT_SPIRE_GARRISON = 50;
    public const int NEUTRAL_SPIRE_GARRISON = 20;
    public const int MIN_SPIRE_DISTANCE = 2; // Hex cells
    
    // Victory Conditions
    public const int MIN_SPIRES_TO_CONTINUE = 1;
}

// Usage in UiMgr.cs
public int unitsPerCommand = GameConstants.DEFAULT_UNITS_PER_COMMAND;

// Usage in MinimaxAI.cs
int maxSend = GameConstants.MAX_UNITS_PER_SEND;

// Usage in Units.cs
int unitsLost = Mathf.CeilToInt(GameConstants.ATTRITION_RATE * tilesTraversed);
```

**Benefits**:
- Easy to tune all values in one place
- Self-documenting code
- Prevents typos (10 vs 1_0)
- Makes balance testing faster

---

## 4. Enum for Phase/State (Instead of Comments)

### ❌ Current
```csharp
public enum Phase { Init, PlayerPlanning, PlayerResolving, AiPlanning, AiResolving, GameOver }
```

### ✅ Improved (With Documentation)
```csharp
/// <summary>
/// Represents the current phase of the turn-based game loop.
/// </summary>
public enum Phase
{
    /// <summary>
    /// Game is initializing, waiting for spires to spawn and systems to be ready.
    /// </summary>
    Init,
    
    /// <summary>
    /// Player's planning phase - can select spires and queue actions.
    /// Input is enabled during this phase.
    /// </summary>
    PlayerPlanning,
    
    /// <summary>
    /// Player's actions are being resolved (units moving, combat happening).
    /// Input is disabled during this phase.
    /// </summary>
    PlayerResolving,
    
    /// <summary>
    /// AI is calculating its moves using Minimax algorithm.
    /// Input is disabled during this phase.
    /// </summary>
    AiPlanning,
    
    /// <summary>
    /// AI's actions are being resolved (units moving, combat happening).
    /// Input is disabled during this phase.
    /// </summary>
    AiResolving,
    
    /// <summary>
    /// Game has ended, displaying results and preparing for restart.
    /// </summary>
    GameOver
}
```

---

## 5. Better Variable Names

### ❌ Current (Unclear Names)
```csharp
public class MinimaxAI : MonoBehaviour
{
    public int k_self = 1;
    public int k_attack = 10;
    private readonly Dictionary<(int, int), int> distanceCache = new Dictionary<(int, int), int>();
    
    public void PlanAndQueueAIMoves() { }
}
```

### ✅ Improved (Clear Names)
```csharp
public class MinimaxAI : MonoBehaviour
{
    [Header("Move Ordering Weights")]
    [Tooltip("Weight for reinforcing friendly spires (defensive value)")]
    public int reinforcementWeight = 1;
    
    [Tooltip("Weight for capturing enemy/neutral spires (offensive value)")]
    public int captureWeight = 10;
    
    /// <summary>
    /// Caches distance calculations between spires to avoid expensive pathfinding.
    /// Key: (fromSpireID, toSpireID), Value: distance in hex cells
    /// </summary>
    private readonly Dictionary<(int fromSpireID, int toSpireID), int> _spireDistanceCache 
        = new Dictionary<(int, int), int>();
    
    /// <summary>
    /// Main entry point for AI turn. Calculates best move using Minimax and queues it for execution.
    /// Called by TurnManager during AiPlanning phase.
    /// </summary>
    public void CalculateAndQueueBestMove() 
    { 
        // Implementation...
    }
}
```

---

## 6. Method Organization & Regions

### ❌ Current (Messy Organization)
```csharp
public class TurnManager : MonoBehaviour
{
    public void StartGame() { }
    private void StartPlayerPlanning() { }
    public void QueueUnits(Units u) { }
    private bool CheckAndSetGameOver() { }
    public void EndPlayerTurn() { }
    // ... 50 more methods in random order
}
```

### ✅ Improved (Logical Organization)
```csharp
public class TurnManager : MonoBehaviour
{
    #region Singleton & Initialization
    
    public static TurnManager Instance { get; private set; }
    
    private void Awake() { /* ... */ }
    private void Start() { /* ... */ }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Starts the game and begins the first player turn.
    /// Called by SpireGenerator when map is ready.
    /// </summary>
    public void StartGame() { /* ... */ }
    
    /// <summary>
    /// Ends the current player turn and begins resolution phase.
    /// Called by UI when player clicks "End Turn" button.
    /// </summary>
    public void EndPlayerTurn() { /* ... */ }
    
    /// <summary>
    /// Registers a unit group for resolution during the current turn.
    /// </summary>
    public void QueueUnits(Units unitGroup) { /* ... */ }
    
    #endregion
    
    #region Turn Phase Management
    
    private void StartPlayerPlanning() { /* ... */ }
    private System.Collections.IEnumerator ResolvePlayerThenAi() { /* ... */ }
    private System.Collections.IEnumerator ResolveTeamAsync(List<Units> queued) { /* ... */ }
    
    #endregion
    
    #region Turn Skip Logic
    
    private bool ShouldSkipTeamTurn(int teamId) { /* ... */ }
    private System.Collections.IEnumerator RunSingleAiTurnThenReturn() { /* ... */ }
    
    #endregion
    
    #region Game Over Handling
    
    private bool CheckAndSetGameOver() { /* ... */ }
    public void EndGame() { /* ... */ }
    private System.Collections.IEnumerator ResetGameAfterDelay(float delay) { /* ... */ }
    
    #endregion
    
    #region Helper Methods
    
    private int CountActiveUnits(int teamId) { /* ... */ }
    private int CountSpires(int teamId) { /* ... */ }
    private bool HaveAnySpires() { /* ... */ }
    
    #endregion
}
```

**Benefits**:
- Easy to find related methods
- Clear separation of concerns
- Better code navigation
- Collapsible regions in IDE

---

## 7. Error Handling & Validation

### ❌ Current (Silent Failures)
```csharp
public void CommandUnits(int sendCount, List<HexCell> path, SpireConstruct target)
{
    if (sendCount <= 0) return false;
    if (available < sendCount) return false;
    // ... rest of method
}
```

### ✅ Improved (Clear Error Messages)
```csharp
/// <summary>
/// Commands units from this spire to travel to a target spire.
/// </summary>
/// <returns>True if command was accepted and queued, false if command was invalid</returns>
public bool CommandUnits(int sendCount, List<HexCell> path, SpireConstruct target, Vector3[] waypoints = null)
{
    // Validation with clear error messages
    if (hasCommandedThisTurn)
    {
        Debug.LogWarning($"[SpireConstruct] {name} has already commanded units this turn");
        return false;
    }
    
    if (sendCount <= 0)
    {
        Debug.LogWarning($"[SpireConstruct] Invalid send count: {sendCount}. Must be positive.");
        return false;
    }
    
    if (parentCell == null)
    {
        Debug.LogError($"[SpireConstruct] {name} must be placed on a cell before commanding units");
        return false;
    }
    
    int available = GetTotalGarrisonCount();
    if (available < sendCount)
    {
        Debug.LogWarning($"[SpireConstruct] {name} only has {available} units but tried to send {sendCount}");
        return false;
    }
    
    if (path == null || path.Count < 2)
    {
        Debug.LogError($"[SpireConstruct] Invalid path provided (null or too short)");
        return false;
    }
    
    if (target == null)
    {
        Debug.LogError($"[SpireConstruct] Target spire is null");
        return false;
    }
    
    // Command is valid, proceed with execution
    Debug.Log($"[SpireConstruct] {name} (Team {teamID}) commanding {sendCount} units to {target.name} (Team {target.teamID})");
    
    // ... rest of implementation
    return true;
}
```

**Benefits**:
- Easy to debug when things go wrong
- Clear feedback about what failed
- Helps catch logic errors early
- Makes logs actually useful

---

## 8. Boolean Method Names

### ❌ Current (Unclear Names)
```csharp
public void CheckAndSetGameOver() { }
public bool AllowReinforce = true;
```

### ✅ Improved (Clear Intent)
```csharp
/// <summary>
/// Checks if game over conditions are met and transitions to GameOver phase if so.
/// </summary>
/// <returns>True if game over was triggered, false if game continues</returns>
private bool TryTriggerGameOver() { }

/// <summary>
/// Whether the AI should consider reinforcing its own spires during move generation.
/// Disable this to make AI more aggressive (attack-only).
/// </summary>
[Tooltip("Allow AI to send units to friendly spires for reinforcement")]
public bool allowReinforcementMoves = true;
```

**Pattern**: Boolean methods should be named `Is...()`, `Has...()`, `Can...()`, `Should...()`

---

## 9. Remove Dead Code

### ❌ Current (Unused Methods Everywhere)
```csharp
public class TurnManager : MonoBehaviour
{
    // This method is NEVER called anywhere in the codebase!
    private System.Collections.IEnumerator RunAiAutoLoop()
    {
        // 50 lines of complex logic that nobody uses
    }
    
    // This property is set but never read
    private bool gameOverChecked = false;
}
```

### ✅ Improved (Clean Codebase)
```csharp
public class TurnManager : MonoBehaviour
{
    // Dead code removed!
    // If you might need it later, use git history or move to a "Deprecated.cs" file
}
```

**How to find dead code**:
1. Use your IDE's "Find Usages" feature
2. Comment out suspicious code and see if anything breaks
3. Run the game - if it works, the code was dead

---

## 10. Consistent Formatting

### ❌ Current (Inconsistent)
```csharp
public class TurnManager : MonoBehaviour {
    public void StartGame() {
        if(condition) {
            DoThing();
        }
    }
    
    private void 
    StartPlayerPlanning()
    {
        turnCount++;
    }
}
```

### ✅ Improved (Consistent)
```csharp
public class TurnManager : MonoBehaviour
{
    public void StartGame()
    {
        if (condition)
        {
            DoThing();
        }
    }
    
    private void StartPlayerPlanning()
    {
        turnCount++;
    }
}
```

**Use a formatter**: Tools → C# → Format Document (or Ctrl+K, Ctrl+D in Visual Studio)

---

## Summary Checklist

Apply these improvements to make your code more readable:

- [ ] Convert all `inst` to `Instance` with proper singleton pattern
- [ ] Add XML documentation to all public methods
- [ ] Extract magic numbers to GameConstants.cs
- [ ] Document all enums
- [ ] Rename unclear variables (k_self → reinforcementWeight, etc.)
- [ ] Organize methods into logical regions
- [ ] Add validation with clear error messages
- [ ] Rename boolean methods to Is/Has/Can/Should pattern
- [ ] Remove all dead code
- [ ] Run code formatter on all files

**Estimated Time**: 6-8 hours to apply to entire codebase

**Benefits**:
- Faster debugging
- Easier to onboard new team members
- Less likely to introduce bugs
- More professional presentation
- Better grade on code quality! 📚
