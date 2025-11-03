using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Replaces the square-grid Pathfinding.cs
/// Finds paths on a HexGrid using A* and the provided Heap.
/// Integrates with PathRequestManager.
/// </summary>
public class HexPathfinder : MonoBehaviour
{
    public HexGrid grid; // Assign your HexGrid in the inspector

    void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<HexGrid>();
        }
    }

    /// <summary>
    /// Public entry point called by PathRequestManager.
    /// </summary>
    public void StartFindPath(Vector3 startPos, Vector3 targetPos, List<Vector3> waypoints)
    {
        StartCoroutine(FindPath(startPos, targetPos, waypoints));
    }

    /// <summary>
    /// The A* pathfinding coroutine.
    /// </summary>
    IEnumerator FindPath(Vector3 startPos, Vector3 targetPos, List<Vector3> waypoints)
    {
        Vector3[] path = new Vector3[0];
        bool pathSuccess = false;

        // 1. Get the start and end cells from the HexGrid
        HexCell startNode = grid.GetCell(grid.WorldToAxial(startPos));
        HexCell targetNode = grid.GetCell(grid.WorldToAxial(targetPos));

        // Create the list of waypoints to visit
        List<Vector3> totalWaypoints = new List<Vector3>();
        totalWaypoints.Add(startPos);
        totalWaypoints.AddRange(waypoints);
        totalWaypoints.Add(targetPos);

        Vector3 currentWaypoint = startPos;

        // Check if the main start and end are valid
        if (startNode != null && startNode.isWalkable && targetNode != null && targetNode.isWalkable)
        {
            List<Vector3> finalPathSegments = new List<Vector3>();

            // 2. Loop through each segment of the path (e.g., A to B, then B to C)
            for (int i = 0; i < totalWaypoints.Count - 1; i++)
            {
                HexCell startWaypointNode = grid.GetCell(grid.WorldToAxial(currentWaypoint));
                HexCell targetWaypointNode = grid.GetCell(grid.WorldToAxial(totalWaypoints[i + 1]));

                if (startWaypointNode == null || !startWaypointNode.isWalkable ||
                    targetWaypointNode == null || !targetWaypointNode.isWalkable)
                {
                    pathSuccess = false;
                    break; // Stop if any segment is invalid
                }

                // --- A* Algorithm Begins ---

                // Use the HexCell, which is now an IHeapItem
                Heap<HexCell> openSet = new Heap<HexCell>(grid.MaxSize);
                HashSet<HexCell> closedSet = new HashSet<HexCell>();

                // Reset all cell data for this path segment
                foreach (var cell in grid.GetAllCells())
                {
                    cell.ResetPathData();
                }

                openSet.Add(startWaypointNode);
                startWaypointNode.gCost = 0;
                startWaypointNode.hCost = GetHexDistance(startWaypointNode, targetWaypointNode);
                startWaypointNode.pathfindingState = HexCell.PathfindingState.Open;

                bool segmentSuccess = false;

                while (openSet.Count > 0)
                {
                    HexCell currentNode = openSet.RemoveFirst();
                    closedSet.Add(currentNode);
                    currentNode.pathfindingState = HexCell.PathfindingState.Closed;

                    // 3. Path segment found
                    if (currentNode == targetWaypointNode)
                    {
                        segmentSuccess = true;
                        break;
                    }

                    // 4. Check Neighbors (using HexGrid's neighbor logic)
                    foreach (HexCell neighbour in grid.GetNeighbors(currentNode.axial_coords))
                    {
                        if (!neighbour.isWalkable || closedSet.Contains(neighbour))
                        {
                            continue;
                        }

                        // 5. Calculate Cost
                        // We use a constant cost of 10 for adjacent hexes,
                        // matching the 10/14 system's base cost.
                        int newMovementCostToNeighbour = currentNode.gCost + 10;

                        if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                        {
                            neighbour.gCost = newMovementCostToNeighbour;

                            // Heuristic must also be scaled by 10
                            neighbour.hCost = GetHexDistance(neighbour, targetWaypointNode) * 10;
                            neighbour.parent = currentNode;

                            if (!openSet.Contains(neighbour))
                            {
                                openSet.Add(neighbour);
                                neighbour.pathfindingState = HexCell.PathfindingState.Open;
                            }
                            else
                                openSet.UpdateItem(neighbour); // Update its position in the heap
                        }
                    }
                }
                // --- A* Algorithm Ends ---

                if (segmentSuccess)
                {
                    pathSuccess = true; // At least one segment worked
                    Vector3[] partialPath = RetracePath(startWaypointNode, targetWaypointNode);
                    finalPathSegments.AddRange(partialPath);
                    currentWaypoint = totalWaypoints[i + 1];
                }
                else
                {
                    pathSuccess = false; // A segment failed, the whole path fails
                    break;
                }
            }

            if (pathSuccess)
            {
                path = finalPathSegments.ToArray();
            }
        }

        yield return null; // Wait one frame

        // 6. Report back to the PathRequestManager
        if (PathRequestManager.inst != null)
        {
            PathRequestManager.inst.FinishedProcessingPath(path, pathSuccess);
        }
    }

    /// <summary>
    /// Reconstructs the path from end to start.
    /// </summary>
    /// <summary>
    /// Reconstructs the path from end to start.
    /// NOW returns ALL cells, not a simplified path.
    /// </summary>
    Vector3[] RetracePath(HexCell startNode, HexCell endNode)
    {
        List<HexCell> path = new List<HexCell>();
        HexCell currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode.pathfindingState = HexCell.PathfindingState.Path; // Mark as part of path
            currentNode = currentNode.parent;
        }

        
        List<Vector3> waypoints = new List<Vector3>();

        // Add the start node's position FIRST
        waypoints.Add(grid.AxialToWorldPosition(startNode.axial_coords));

        // Add the rest of the path cells (which are in reverse order: end -> start)
        for (int i = path.Count - 1; i >= 0; i--)
        {
            waypoints.Add(grid.AxialToWorldPosition(path[i].axial_coords));
        }

        return waypoints.ToArray();
    }

    

    /// <summary>
    /// Gets the hexagonal distance (heuristic) between two cells.
    /// This is the "Manhattan distance" on a cube coordinate system.
    /// </summary>
    int GetHexDistance(HexCell nodeA, HexCell nodeB)
    {
        // Convert both axial coordinates to cube coordinates
        Vector3Int cubeA = AxialToCube(nodeA.axial_coords);
        Vector3Int cubeB = AxialToCube(nodeB.axial_coords);

        // Calculate Manhattan distance in cube coordinates
        int dx = Mathf.Abs(cubeA.x - cubeB.x);
        int dy = Mathf.Abs(cubeA.y - cubeB.y);
        int dz = Mathf.Abs(cubeA.z - cubeB.z);

        // The hex distance is half the cube Manhattan distance
        return (dx + dy + dz) / 2;
    }

    /// <summary>
    /// Helper to convert axial (q, r) to cube (x, y, z) coordinates.
    /// </summary>
    private Vector3Int AxialToCube(Vector2Int axial)
    {
        int x = axial.x; // q
        int z = axial.y; // r
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }
}