# SpireStrife Architecture Overview

## System Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         GAME STARTUP                             │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
                    ┌───────────────────────┐
                    │   SpireGenerator      │
                    │  - Places spires      │
                    │  - Applies difficulty │
                    │  - Calls StartGame()  │
                    └───────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TURN-BASED GAME LOOP                          │
└─────────────────────────────────────────────────────────────────┘

    ┌──────────────────────────────────────────────────┐
    │ 1. PLAYER PLANNING PHASE                         │
    │  - TurnManager.CurrentPhase = PlayerPlanning     │
    │  - PlayerInputEnabled = true                     │
    │  - Player can click spires and targets           │
    └──────────────────────────────────────────────────┘
                    │
                    │ Player clicks "End Turn"
                    ▼
    ┌──────────────────────────────────────────────────┐
    │ 2. PLAYER RESOLUTION PHASE                       │
    │  - TurnManager.CurrentPhase = PlayerResolving    │
    │  - PlayerInputEnabled = false                    │
    │  - Units move along paths                        │
    │  - Combat resolved                               │
    │  - Attrition applied                             │
    └──────────────────────────────────────────────────┘
                    │
                    │ Player units finish moving
                    ▼
    ┌──────────────────────────────────────────────────┐
    │ 3. AI PLANNING PHASE                             │
    │  - TurnManager.CurrentPhase = AiPlanning         │
    │  - MinimaxAI calculates best move                │
    │  - Uses alpha-beta pruning                       │
    │  - Queues units for movement                     │
    └──────────────────────────────────────────────────┘
                    │
                    │ AI finishes planning
                    ▼
    ┌──────────────────────────────────────────────────┐
    │ 4. AI RESOLUTION PHASE                           │
    │  - TurnManager.CurrentPhase = AiResolving        │
    │  - AI units move along paths                     │
    │  - Combat resolved                               │
    │  - Attrition applied                             │
    └──────────────────────────────────────────────────┘
                    │
                    │ AI units finish moving
                    ▼
    ┌──────────────────────────────────────────────────┐
    │ 5. CHECK GAME OVER                               │
    │  - ScoreMgr checks victory conditions            │
    │  - If not over, reset turn flags                 │
    │  - Loop back to step 1                           │
    └──────────────────────────────────────────────────┘
                    │
                    │ Game over condition met
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                        GAME OVER                                 │
│  - Display results (Victory/Defeat/Draw)                        │
│  - Wait 3 seconds                                                │
│  - Reload scene (fresh start)                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Unit Movement Flow

```
PLAYER SELECTS SOURCE SPIRE (Left Click)
    │
    ├─► UiMgr.SelectCell(cell)
    │   └─► Highlights spire yellow
    │
PLAYER SELECTS TARGET SPIRE (Right Click)
    │
    ├─► UiMgr.SelectTargetCell(cell)
    │   ├─► Requests path from PathRequestManager
    │   │   └─► HexPathfinder.StartFindPath()
    │   │       └─► A* algorithm finds optimal path
    │   │           └─► Returns waypoints array
    │   │
    │   ├─► Colors path cells cyan
    │   │
    │   ├─► UiMgr.OnPathFound()
    │   │   ├─► Spawns units at source spire
    │   │   │   └─► Deducts from remainingGarrison
    │   │   │
    │   │   ├─► SpireConstruct.CommandUnits()
    │   │   │   ├─► Creates Units GameObject
    │   │   │   ├─► Calls Units.QueueMovement()
    │   │   │   │   ├─► Sets state = Traversing
    │   │   │   │   ├─► Stores plannedPath
    │   │   │   │   ├─► Stores targetObject
    │   │   │   │   └─► Generates visual units (dots)
    │   │   │   │
    │   │   │   └─► TurnManager.QueueUnits(units)
    │   │   │       └─► Adds to queuedPlayer list
    │   │   │
    │   │   └─► TurnManager.EndPlayerTurn()
    │
TURN MANAGER RESOLUTION PHASE
    │
    ├─► TurnManager.ResolveTeamAsync(queuedPlayer)
    │   ├─► For each queued Units:
    │   │   └─► Units.ExecuteMovementAnimated()
    │   │       ├─► MoveUnits.MoveUnitsAlongPath()
    │   │       │   ├─► Assigns kill steps (attrition schedule)
    │   │       │   │   └─► Units die 1 per tile as they travel
    │   │       │   │
    │   │       │   └─► For each waypoint in path:
    │   │       │       ├─► Apply potential fields
    │   │       │       │   ├─► Repulsion from other units
    │   │       │       │   └─► Attraction to waypoint
    │   │       │       │
    │   │       │       ├─► Update unit position/heading
    │   │       │       │
    │   │       │       └─► If at kill step:
    │   │       │           └─► Destroy unit (attrition)
    │   │       │
    │   │       └─► On arrival at destination:
    │   │           ├─► MoveEntity.CheckForCollisions()
    │   │           │   └─► Detects SpireConstruct
    │   │           │       └─► SpireConstruct.CaptureSpire()
    │   │           │           ├─► If same team: Reinforce
    │   │           │           │   └─► Add to remainingGarrison
    │   │           │           │
    │   │           │           └─► If different team: Combat
    │   │           │               ├─► Subtract from garrison
    │   │           │               │
    │   │           │               └─► If garrison <= 0:
    │   │           │                   ├─► Change teamID
    │   │           │                   └─► Update spire lists
    │   │           │
    │   │           └─► Destroy Units GameObject
    │   │
    │   └─► Clear queuedPlayer list
    │
    └─► Continue to AI Planning Phase
```

---

## AI Decision Making Flow

```
AI PLANNING PHASE
    │
    ├─► MinimaxAI.PlanAndQueueAIMoves()
    │   │
    │   ├─► MinimaxAlgorithm.FindBestMove()
    │   │   │
    │   │   ├─► 1. SNAPSHOT CURRENT STATE
    │   │   │   └─► AIState.Snapshot(allSpires)
    │   │   │       └─► Captures teamID, garrison, reserve for each spire
    │   │   │
    │   │   ├─► 2. GENERATE CANDIDATE MOVES
    │   │   │   └─► GetValidMoves(state, aiTeam)
    │   │   │       ├─► For each AI spire:
    │   │   │       │   └─► For each target (enemy/neutral/allied):
    │   │   │       │       ├─► Calculate distance
    │   │   │       │       ├─► Calculate arriving units (send - distance)
    │   │   │       │       └─► If arriving > 0: Valid move
    │   │   │       │
    │   │   │       ├─► Calculate heuristic score for each move
    │   │   │       │   ├─► If same team: reinforcementWeight * newUnits
    │   │   │       │   └─► If different: captureWeight * remainingUnits
    │   │   │       │
    │   │   │       └─► Sort by score, take top GlobalMoveCap moves
    │   │   │
    │   │   ├─► 3. NEGAMAX SEARCH WITH ALPHA-BETA PRUNING
    │   │   │   │
    │   │   │   └─► For each candidate move:
    │   │   │       ├─► Simulate move → new state
    │   │   │       │
    │   │   │       ├─► Recurse with Negamax()
    │   │   │       │   ├─► If depth = 0: Evaluate state
    │   │   │       │   │   └─► Score = spireWeight * spireDiff
    │   │   │       │   │            + unitWeight * unitDiff
    │   │   │       │   │            + expansion score
    │   │   │       │   │            + territorial control
    │   │   │       │   │            + positional value
    │   │   │       │   │
    │   │   │       │   ├─► Else: Generate opponent moves
    │   │   │       │   │   └─► Recurse again (depth-1)
    │   │   │       │   │
    │   │   │       │   └─► Alpha-Beta Pruning:
    │   │   │       │       ├─► Update alpha with best score
    │   │   │       │       └─► If alpha >= beta: PRUNE (stop searching)
    │   │   │       │
    │   │   │       └─► Return best score for this branch
    │   │   │
    │   │   └─► 4. SELECT BEST MOVE
    │   │       └─► Move with highest negamax score
    │   │
    │   ├─► 5. LOOP PREVENTION CHECK
    │   │   └─► If move repeated recently:
    │   │       └─► Find alternative move
    │   │
    │   └─► 6. EXECUTE MOVE VIA UIMGR
    │       └─► UiMgr.SelectCell(from)
    │           └─► UiMgr.SelectTargetCell(to)
    │               └─► (Same flow as player move)
    │
AI RESOLUTION PHASE
    │
    └─► (Same as player resolution, but uses queuedAi list)
```

---

## Data Flow: Unit Counting System

**⚠️ THIS IS CURRENTLY BROKEN - Three systems don't sync!**

```
┌─────────────────────────────────────────────────────────┐
│               UNIT COUNT DATA SOURCES                    │
└─────────────────────────────────────────────────────────┘

1. SpireConstruct.remainingGarrison (int)
   ├─► Represents "reserve" units available to spawn
   ├─► Decreased when spawning units (UiMgr.OnPathFound)
   └─► Used by: GameMgr.Update() → ScoreMgr

2. SpireConstruct.GetTotalGarrisonCount() (method)
   ├─► Sums unitCount from all stationed Units on this cell
   ├─► Represents "active" units defending the spire
   └─► Used by: UiMgr (to check if can send), TurnManager

3. Units.unitCount (int)
   ├─► Number of units in this specific Units GameObject
   ├─► Should decrease during travel (attrition)
   └─► Used by: Combat resolution, visual representation

┌─────────────────────────────────────────────────────────┐
│                  THE PROBLEM                             │
└─────────────────────────────────────────────────────────┘

When units spawn:
  remainingGarrison ↓     ← Correct
  GetTotalGarrisonCount() ↑  ← Correct (new Units added)
  Units.unitCount = 10   ← Correct

When units travel:
  remainingGarrison (unchanged) ← Correct (already spent)
  GetTotalGarrisonCount() ↓     ← NOT UPDATED (Units gone but not subtracted)
  Units.unitCount (unchanged)   ← WRONG! Should decrease per tile

On arrival:
  remainingGarrison += arriving ← Uses wrong value
  Units destroyed               ← Loses ALL visual units at once
  GetTotalGarrisonCount() ↑     ← Sudden jump in count

┌─────────────────────────────────────────────────────────┐
│                  THE SOLUTION                            │
└─────────────────────────────────────────────────────────┘

MAKE GameMgr.remainingPlayerUnits THE SINGLE SOURCE OF TRUTH:

  remainingGarrison = total available for this spire (reserve)
  GetTotalGarrisonCount() = defensive strength (stationed)
  Units.unitCount = individual group size (visual)

  Flow:
    1. Player commands units
       → Spawn from remainingGarrison
       → Move to Traversing state
    
    2. During travel
       → Units.unitCount-- per tile
       → Update visuals continuously
    
    3. On arrival
       → target.remainingGarrison += Units.unitCount
       → Or combat if enemy
    
    4. GameMgr.Update()
       → Sum all remainingGarrison for team
       → This becomes source of truth for victory
```

---

## Manager Responsibilities

```
GameMgr (Single Source of Truth)
├─► Tracks spire ownership lists
│   ├─► playerSpires: List<SpireConstruct>
│   ├─► aiSpires: List<SpireConstruct>
│   └─► neutralSpires: List<SpireConstruct>
│
├─► Calculates total unit counts
│   ├─► remainingPlayerUnits (sum of all player spire reserves)
│   └─► remainingAiUnits (sum of all AI spire reserves)
│
└─► Responds to events
    └─► SpireConstruct.OwnershipChanged → Update lists

TurnManager (Game Loop Controller)
├─► Manages turn phases
│   ├─► Init → PlayerPlanning → PlayerResolving
│   └─► AiPlanning → AiResolving → (back to PlayerPlanning)
│
├─► Queues unit movements
│   ├─► queuedPlayer: List<Units>
│   └─► queuedAi: List<Units>
│
├─► Resolves movements
│   └─► Calls Units.ExecuteMovementAnimated()
│
└─► Checks victory conditions
    └─► Delegates to ScoreMgr

ScoreMgr (Victory Tracker)
├─► Monitors game state
│   ├─► lastPlayerUnits (from GameMgr)
│   ├─► lastAiUnits (from GameMgr)
│   ├─► lastPlayerSpires (from GameMgr)
│   └─► lastAiSpires (from GameMgr)
│
├─► Checks end conditions
│   └─► Both sides out of units → Compare spires
│
└─► Records result
    └─► GameResult: None/PlayerWin/AiWin/Draw

UiMgr (Input Handler)
├─► Handles mouse clicks
│   ├─► Left click → Select spire
│   └─► Right click → Select target
│
├─► Requests paths
│   └─► PathRequestManager.RequestPath()
│
└─► Executes commands
    ├─► Spawns units at source
    ├─► Calls SpireConstruct.CommandUnits()
    └─► Calls TurnManager.EndPlayerTurn()

MinimaxAI (Strategic Intelligence)
├─► Plans AI moves
│   ├─► Snapshot current state
│   ├─► Generate candidate moves
│   ├─► Negamax search with pruning
│   └─► Select best move
│
└─► Executes via UiMgr
    └─► Simulates player clicks

LevelManager (Difficulty Scaling)
├─► Tracks current level
├─► Calculates difficulty (0-1)
├─► Applies to systems:
│   ├─► SpireGenerator.difficulty
│   ├─► MinimaxAI.SearchDepth
│   └─► MinimaxAI.GlobalMoveCap
└─► Handles level progression
```

---

## Critical Dependencies

```
Scene Load
    │
    ├─► HexGrid (Must exist first)
    │   └─► SpireGenerator (Depends on HexGrid)
    │       └─► TurnManager.StartGame()
    │
    ├─► GameMgr (Singleton)
    │   └─► Listens to SpireConstruct events
    │
    ├─► ScoreMgr (Singleton)
    │   └─► Reads GameMgr data
    │
    ├─► TurnManager (Singleton)
    │   └─► Coordinates everything
    │
    ├─► MinimaxAI (Singleton)
    │   └─► Called by TurnManager
    │
    └─► UiMgr (Singleton)
        └─► Controlled by TurnManager.PlayerInputEnabled
```

---

## Victory Condition Comparison

### Current (Buggy) - Spire Loss
```
Game Over if:
  playerSpires == 0 OR aiSpires == 0
  
Problems:
  - Triggers too early (one capture ends game)
  - Not strategic (no comeback possible)
  - Doesn't match presentation
```

### Recommended - Resource Exhaustion
```
Game Over if:
  remainingPlayerUnits == 0 AND remainingAiUnits == 0
  
Then compare:
  playerSpires vs aiSpires for winner
  
Benefits:
  - Matches presentation text
  - More strategic (units matter)
  - Allows comebacks
  - Longer, more engaging games
```

---

## File System Map

```
SpireStrife/
├── Assets/
│   ├── Scripts/
│   │   ├── AI/
│   │   │   └── MinimaxAI.cs ✅ (Core algorithm)
│   │   │
│   │   ├── HexGrid/
│   │   │   ├── HexGrid.cs (Grid manager)
│   │   │   ├── HexCell.cs (Individual cells)
│   │   │   ├── HexPathfinder.cs ✅ (A* implementation)
│   │   │   └── PathRequestManager.cs (Async paths)
│   │   │
│   │   ├── GridObjects/
│   │   │   ├── SpireConstruct.cs ⚠️ (Unit counting issues)
│   │   │   ├── Units.cs ⚠️ (Needs attrition method)
│   │   │   └── Tile.cs (Movement costs)
│   │   │
│   │   ├── Managers/
│   │   │   ├── TurnManager.cs ⚠️ (Turn skip logic buggy)
│   │   │   ├── GameMgr.cs (Spire lists & unit counts)
│   │   │   ├── ScoreMgr.cs (Victory conditions)
│   │   │   ├── LevelManager.cs ✅ (Difficulty scaling)
│   │   │   └── MapController.cs (Map regeneration)
│   │   │
│   │   └── UI/
│   │       ├── UiMgr.cs ⚠️ (Command issuing)
│   │       ├── PauseHandler.cs ✅ (Fixed cursor bug)
│   │       └── EndScreen.cs (Victory/Defeat display)
│   │
│   ├── MoveEntity.cs ⚠️ (Potential fields, needs attrition logging)
│   ├── SpireGenerator.cs ✅ (Map generation with difficulty)
│   └── Unit.cs (Individual unit visual)
│
├── REVIEW_SUMMARY.md ✅ (Read this first!)
├── CRITICAL_FIXES_NEEDED.md ✅ (Detailed analysis)
├── QUICK_FIXES.md ✅ (Copy-paste solutions)
├── CODE_QUALITY_GUIDE.md (Readability improvements)
└── ARCHITECTURE.md ← You are here!

Legend:
  ✅ = Working correctly
  ⚠️ = Has bugs, needs fixes
```

---

## Debugging Checklist

When something breaks, check in this order:

1. **Console Logs** - Look for errors/warnings
   - TurnManager logs turn phases
   - MinimaxAI logs move selection
   - SpireConstruct logs captures

2. **Inspector Values** (while game running)
   - GameMgr.remainingPlayerUnits
   - GameMgr.remainingAiUnits
   - TurnManager.CurrentPhase
   - SpireConstruct.remainingGarrison

3. **Manager States**
   - Is TurnManager.Instance != null?
   - Is GameMgr.Instance != null?
   - Is UiMgr.Instance != null?

4. **Event Subscription**
   - SpireConstruct.OwnershipChanged
   - SpireConstruct.SpirePlaced
   - Units.MovementResolved

5. **Common Issues**
   - Cursor locked → Check PauseHandler
   - Turn won't advance → Check PlayerInputEnabled
   - Units don't move → Check TurnManager.queuedPlayer
   - AI doesn't move → Check MinimaxAI.IsBusy
   - Game ends early → Check victory condition logic

---

This architecture diagram should help you understand how all the pieces fit together!
