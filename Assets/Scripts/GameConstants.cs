/// <summary>
/// Central location for all game balance constants and magic numbers.
/// Modify these values to tune gameplay without searching through code.
/// </summary>
public static class GameConstants
{
    // ==================== UNIT GENERATION ====================
    
    /// <summary>
    /// Default number of units spawned when a spire issues a command.
    /// Used by UiMgr.generateCountPerCommand.
    /// </summary>
    public const int DEFAULT_UNITS_PER_COMMAND = 10;
    
    /// <summary>
    /// Maximum units that can be sent in a single command.
    /// Used by MinimaxAI to cap send amounts.
    /// </summary>
    public const int MAX_UNITS_PER_SEND = 20;
    
    /// <summary>
    /// Minimum units required to issue a command.
    /// </summary>
    public const int MIN_UNITS_FOR_ACTION = 1;
    
    // ==================== ATTRITION ====================
    
    /// <summary>
    /// Units lost per tile traversed during movement.
    /// 1.0 = one unit per tile, 0.5 = one unit per two tiles.
    /// </summary>
    public const float ATTRITION_RATE = 1.0f;
    
    // ==================== AI SETTINGS ====================
    
    /// <summary>
    /// Minimum search depth for AI Minimax algorithm.
    /// </summary>
    public const int MIN_AI_SEARCH_DEPTH = 1;
    
    /// <summary>
    /// Maximum search depth for AI Minimax algorithm.
    /// Higher = smarter AI but slower turns.
    /// </summary>
    public const int MAX_AI_SEARCH_DEPTH = 7;
    
    /// <summary>
    /// Default search depth for AI at medium difficulty.
    /// </summary>
    public const int DEFAULT_AI_SEARCH_DEPTH = 3;
    
    /// <summary>
    /// Default move cap for AI move generation (limits branching factor).
    /// </summary>
    public const int DEFAULT_AI_MOVE_CAP = 20;
    
    // ==================== SPIRE CONFIGURATION ====================
    
    /// <summary>
    /// Default initial garrison for player/AI spires.
    /// Lower values = longer, more strategic games.
    /// </summary>
    public const int DEFAULT_SPIRE_GARRISON = 50;
    
    /// <summary>
    /// Default initial garrison for neutral spires.
    /// Should be challenging but worth capturing.
    /// </summary>
    public const int NEUTRAL_SPIRE_GARRISON = 20;
    
    /// <summary>
    /// Minimum hex cell distance between spires during generation.
    /// </summary>
    public const int MIN_SPIRE_DISTANCE = 2;
    
    /// <summary>
    /// Default cost to claim a neutral spire (units required).
    /// </summary>
    public const int DEFAULT_CLAIM_COST = 10;
    
    // ==================== VICTORY CONDITIONS ====================
    
    /// <summary>
    /// Minimum spires a team needs to continue playing.
    /// If a team has 0 spires, they cannot generate more units.
    /// </summary>
    public const int MIN_SPIRES_TO_CONTINUE = 1;
    
    // ==================== TURN MANAGEMENT ====================
    
    /// <summary>
    /// Maximum frames to wait for AI planning before timeout.
    /// </summary>
    public const int AI_PLANNING_TIMEOUT_FRAMES = 300;
    
    /// <summary>
    /// Maximum frames to wait for unit movement completion.
    /// </summary>
    public const int MOVEMENT_TIMEOUT_FRAMES = 240;
    
    /// <summary>
    /// Delay in seconds before restarting game after game over.
    /// </summary>
    public const float GAME_OVER_RESTART_DELAY = 3f;
    
    // ==================== VISUAL SETTINGS ====================
    
    /// <summary>
    /// Distance units must be from waypoint to be considered "arrived".
    /// </summary>
    public const float WAYPOINT_ARRIVAL_DISTANCE = 0.5f;
    
    /// <summary>
    /// Default spacing between unit visuals in hex packing.
    /// </summary>
    public const float UNIT_VISUAL_SPACING = 0.35f;
    
    /// <summary>
    /// Default apothem (center-to-edge distance) for hex cells.
    /// </summary>
    public const float HEX_APOTHEM = 0.5f;
}
