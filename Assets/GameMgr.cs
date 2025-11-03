// GameMgr.cs
// Manages the game state and cell modification.

using System.Collections;
using System.Collections.Generic;
using System.Linq; // Needed for .ToList()
using UnityEngine;

public class GameMgr : MonoBehaviour
{
    [Header("Grid References")]
    public HexGrid grid; // Assign your HexGrid from the scene in the Inspector
    
    [Header("Terrain Modification")]
    [Tooltip("The number of random cells to elevate and turn red (to act as obstacles).")]
    public int cellsToElevateAsObstacles = 15; // This is your "x amount"
    
    [Tooltip("How much to multiply the coordinate by for height. (e.g., 0.25)")]
    public float elevationAmount = 0.25f; 

    void Start()
    {
        if (grid == null)
        {
            // Try to find the grid automatically if not assigned
            grid = FindObjectOfType<HexGrid>();
        }

        if (grid != null)
        {
            // Modify the cells to create obstacles
            ModifyRandomCells();
        }
        else
        {
            Debug.LogError("GameMgr: No HexGrid found in the scene!");
        }
    }

    /// <summary>
    /// Finds a random number of cells and changes their height and color.
    /// These modified cells will act as obstacles.
    /// </summary>
    public void ModifyRandomCells()
    {
        // Get all available cells from the grid
        List<HexCell> allCells = grid.GetAllCells().ToList();
        int modifiedCount = 0;

        // Loop for the "x amount" of cells you want to modify
        for (int i = 0; i < cellsToElevateAsObstacles; i++)
        {
            // Stop if we run out of cells
            if (allCells.Count == 0)
            {
                Debug.LogWarning("Ran out of cells to modify.");
                break; 
            }

            // 1. Pick a random cell
            int randomIndex = Random.Range(0, allCells.Count);
            HexCell cell = allCells[randomIndex];

            // Remove cell from list so we don't pick it twice
            allCells.RemoveAt(randomIndex); 

            // Set Y (height) to a random value between 1 and the current X scale
            
            cell.transform.localPosition = new Vector3(
                cell.transform.localPosition.x,
                -0.2f,
                cell.transform.localPosition.z
            );
            float minHeight = .5f;
            float maxHeight = 1f;
            if (maxHeight < minHeight) maxHeight = minHeight; // ensure valid range
            float randomHeight = Random.Range(minHeight, maxHeight);
            cell.transform.localScale = new Vector3(
                cell.transform.localScale.x,
                randomHeight,
                cell.transform.localScale.z
            );
            cell.gameObject.layer = LayerMask.NameToLayer("Obstacle");
            cell.isWalkable = false;
            // --- Action 2: Change Color ---
            // pick a red hue near 0 (or wrap-around near 1) and randomize saturation/value for different shades
            float hue = (Random.value < 0.5f) ? Random.Range(0f, 0.05f) : Random.Range(0.95f, 1f);
            float saturation = Random.Range(0.6f, 1f);
            float value = Random.Range(0.5f, 1f);
            Color randomRed = Color.HSVToRGB(hue, saturation, value);
            cell.SetColor(randomRed);
            
            // Note: You will later need to add logic (e.g., in a pathfinding script)
            // to check if a cell's color is red or if its height > 0
            // to treat it as an impassable obstacle.
            
            modifiedCount++;
        }
        
        Debug.Log($"Successfully modified {modifiedCount} cells to be obstacles.");
    }
}