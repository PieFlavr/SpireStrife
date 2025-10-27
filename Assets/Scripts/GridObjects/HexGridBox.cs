using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Draws a wireframe outline for every HexCell in the target HexGrid
/// using a single LineRenderer. This is visible at runtime.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class HexGridBox : MonoBehaviour 
{
    [Tooltip("The grid to draw outlines for. Will try to find one if not set.")]
    public HexGrid grid;
    
    [Tooltip("A small vertical offset to draw the lines, to avoid Z-fighting with the cell's surface.")]
    public float yOffset = 0.01f;
    
    private LineRenderer line;

    // A static array to hold vertex calculations for a single hex
    private static readonly Vector3[] hexVertices = new Vector3[6];

    void Start() 
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false; // We are drawing many individual segments, not one continuous loop
        line.positionCount = 0; // Start empty
        line.widthMultiplier = 0.05f;
        
        // Use a simple unlit material
        if (line.material == null || line.material.name.Contains("Default"))
        {
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.blue;
            line.endColor = Color.blue;
        }

        if (grid == null) {
            grid = GetComponentInParent<HexGrid>();
        }

        // We must wait a frame for the HexGrid to finish its Awake() and create all the cells.
        StartCoroutine(DrawOutlinesAfterInit());
    }

    /// <summary>
    /// Waits one frame for the grid to initialize, then draws the outlines.
    /// </summary>
    private IEnumerator DrawOutlinesAfterInit()
    {
        // Wait one frame for HexGrid.Awake() and HexGrid.CreateHexCells() to complete
        yield return null; 
        
        if (grid != null)
        {
            DrawCellOutlines();
        }
        else
        {
            Debug.LogError("HexGridBox: No HexGrid component found!");
        }
    }

    /// <summary>
    /// Calculates and draws the outlines for all cells in the grid.
    /// This can be called publicly to refresh the lines if the grid rebuilds.
    /// </summary>
    [ContextMenu("Redraw Cell Outlines")]
    public void DrawCellOutlines() 
    {
        if (grid == null || line == null) return;
        
        // Get all cells from the grid
        var cells = grid.GetAllCells().ToList();
        if (cells == null || cells.Count == 0) 
        {
            line.positionCount = 0;
            return;
        }

        // Each hex needs 6 lines, and each line needs 2 points (start and end).
        int pointCount = cells.Count * 6 * 2;
        line.positionCount = pointCount;

        Vector3[] allPoints = new Vector3[pointCount];
        
        // Use the cellSpacing from the grid
        float size = grid.cellSpacing; 
        Vector3 verticalOffset = new Vector3(0, yOffset, 0);

        // Pre-calculate the 6 vertex offsets for a flat-top hex
        // This is based on the logic from your grid's AxialToWorldPosition
        float sqrt3_2 = Mathf.Sqrt(3.0f) / 2.0f;
        Vector3 v0_offset = new Vector3(size, 0, 0);
        Vector3 v1_offset = new Vector3(size * 0.5f, 0, size * sqrt3_2);
        Vector3 v2_offset = new Vector3(size * -0.5f, 0, size * sqrt3_2);
        Vector3 v3_offset = new Vector3(-size, 0, 0);
        Vector3 v4_offset = new Vector3(size * -0.5f, 0, -size * sqrt3_2);
        Vector3 v5_offset = new Vector3(size * 0.5f, 0, -size * sqrt3_2);
        
        int pointIndex = 0;

        // Loop through every cell and calculate its 6 line segments
        foreach (var cell in cells) 
        {
            if (cell == null) continue;

            Vector3 center = cell.transform.position + verticalOffset;

            // Calculate the 6 world-space vertices for this cell
            hexVertices[0] = center + v0_offset;
            hexVertices[1] = center + v1_offset;
            hexVertices[2] = center + v2_offset;
            hexVertices[3] = center + v3_offset;
            hexVertices[4] = center + v4_offset;
            hexVertices[5] = center + v5_offset;

            // Add the 6 line segments (12 points) to the array
            for (int i = 0; i < 6; i++)
            {
                allPoints[pointIndex++] = hexVertices[i];
                allPoints[pointIndex++] = hexVertices[(i + 1) % 6];
            }
        }

        // Set all positions at once for performance
        line.SetPositions(allPoints);
    }
}