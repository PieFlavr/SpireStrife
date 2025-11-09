Spire Strife — CS481 Project
Claim the spires. Rule the realm.
By: Thomas Darnell, Yatharth Vohra, Lucas Pinto

🎮 Overview
Spire Strife is a turn-based strategy game where the player battles an AI opponent to conquer as many Spires on a hex-grid map as possible.
Each Spire contains a limited number of units that can be used to attack, capture, or reinforce others.
When both sides run out of units, the game ends—the side controlling the most Spires wins.

⚙️ Gameplay Mechanics
Spires: Command available units to
- Attack enemy Spires
- Capture neutral Spires
- Transfer units to allied Spires
- Hold position

Units:
- Move across tiles toward targets (unit count decreases per tile)
- Engage enemy units one-for-one until one side is depleted

Obstacles:
- Block paths and reduce Spire range
- Force alternate routefinding

🧠 AI System
Minimax AI (Clean Version Only):
Minimax AI:
1. Snapshot live spires (garrison + reserve separated).
2. Generate candidate moves (spawn from reserve up to cap; arrival = send - distance).
3. Negamax + alpha-beta search over plies (SearchDepth config).
4. Evaluation = SpireOwnershipWeight * (spire diff) + UnitWeight * (unit diff).
Inspector Settings (`MinimaxAI`):
* SearchDepth – number of half-turns (AI, Player, AI ...)
* GlobalMoveCap – limits branching for performance
* AllowReinforce – consider friendly spires as reinforcement targets
* k_self / k_attack – move ordering weights (defensive vs offensive potential)
* SpireOwnershipWeight / UnitWeight – leaf evaluation weights
Send amount is capped (MAX_SEND_AMOUNT = 20) and derived from reserve; travel attrition reduces arrival.

A Pathfinding + Potential Fields (PF):*
- Calculates optimal paths between Spires
- Uses PFs for unit movement and collision management

🎨 Visuals & Audio
Aesthetic:
- 2D fantasy visuals rendered in a 3D environment

Sound Design:
- Environmental ambience
- Short cues for battles, captures, and victory
- SFX for selections and UI interactions

🧩 Resources
Game Assets: Kenney Game Assets (models, sounds, music)
Custom Content:
- UI, grid visuals, and unique elements developed in-house

🧑‍💻 Team Roles
- Core gameplay logic and visuals
- MiniMax AI and evaluator tuning
- A* + PF adaptation for pathfinding and unit animation

🗓️ Development Timeline
Milestone | Date | Description
---|---|---
Alpha | 10/27 | Core mechanics, simple AI, placeholder visuals
Beta | 11/03 | Advanced AI, adaptive difficulty, polished assets
Final | 11/10 | Complete and playable build

🚀 How to Run
Clone the repository
```
git clone https://github.com/<your-repo>/SpireStrife.git
cd SpireStrife
```
Open the project in Unity (version 2022.3.20f or newer).
Press Play in the Unity Editor to start the match against AI.

🧠 AI Component
Add `MinimaxAI` to a scene GameObject. `TurnManager` will automatically use it for AI planning.

Tuning Tips:
* Lower GlobalMoveCap if frame hitching occurs on large maps.
* Reduce SearchDepth for faster turns; raise for deeper planning.
* Raise k_attack relative to k_self for more aggression.
* Slightly increase UnitWeight (1–3) to reward efficient captures.
