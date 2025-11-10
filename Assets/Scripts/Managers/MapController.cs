using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages map generation, cleanup, and regeneration between matches.
/// Coordinates with GameMgr, TurnManager, and ScoreMgr for state resets.
/// </summary>
public class MapController : MonoBehaviour
{
    public static MapController inst;

    [Header("Generation")]
    public SpireGenerator spireGenerator;
    public ObstacleGenerator obstacleGenerator;

    [Header("Cleanup")]
    public bool clearUnitsOnRegenerate = true;
    public bool clearObstaclesOnRegenerate = false;

    private void Awake()
    {
        inst = this;
        if (spireGenerator == null) spireGenerator = FindObjectOfType<SpireGenerator>();
        if (obstacleGenerator == null) obstacleGenerator = FindObjectOfType<ObstacleGenerator>();
    }

    /// <summary>
    /// Regenerates the map with current difficulty settings.
    /// </summary>
    public void RegenerateMap()
    {
        StartCoroutine(RegenerateMapCoroutine());
    }

    public IEnumerator RegenerateMapCoroutine()
    {
        Debug.Log("MapController: Starting map regeneration...");

        // Clear existing entities
        ClearSpires();
        if (clearUnitsOnRegenerate) ClearUnits();
        if (clearObstaclesOnRegenerate && obstacleGenerator != null)
            obstacleGenerator.ClearObstacles();

        yield return null; // Wait one frame for cleanup

        // Regenerate spires with current difficulty
        if (spireGenerator != null)
        {
            yield return spireGenerator.GenerateSpiresCoroutine();
        }

        // Regenerate obstacles if desired
        if (obstacleGenerator != null && !clearObstaclesOnRegenerate)
        {
            // Keep existing obstacles or regenerate based on your needs
        }

        // Reset game state
        ResetGameState();

        Debug.Log("MapController: Map regeneration complete.");
    }

    private void ClearSpires()
    {
        foreach (var spire in FindObjectsOfType<SpireConstruct>())
        {
            Destroy(spire.gameObject);
        }
    }

    private void ClearUnits()
    {
        foreach (var units in FindObjectsOfType<Units>())
        {
            Destroy(units.gameObject);
        }
    }

    private void ResetGameState()
    {
        // Reset managers to initial state
        if (GameMgr.inst != null)
        {
            GameMgr.inst.RebuildSpireLists();
        }

        if (ScoreMgr.inst != null)
        {
            ScoreMgr.inst.ResetResult();
        }

        if (TurnManager.inst != null)
        {
            TurnManager.inst.StartGame();
            Debug.Log("MapController: Reset TurnManager.");
        }

        if (LevelManager.inst != null && LevelManager.inst.performanceStats != null)
        {
            LevelManager.inst.performanceStats.BeginMatchTracking();
            Debug.Log("MapController: Reset PerformanceStats tracking.");
        }
    }

    /// <summary>
    /// Simple scene reload (nukes everything, easier but slower).
    /// </summary>
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}