
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class HexCell : MonoBehaviour, IHeapItem<HexCell>
{
    [Header("Grid Position")]
    public Vector2Int axial_coords;

    [Header("Grid Objects")]
    [Tooltip("All GridObjects currently placed on this cell")]
    private List<GridObject> gridObjects;

    [Header("Grid Reference")]
    public HexGrid parentGrid;

    [Header("Pathfinding")]
    public bool isWalkable = true;

    private Renderer cellRenderer;
    
    [Header("Cell Visuals")]
    [SerializeField]
    public Color cellColor = Color.white;

    public int gCost;
    public int hCost;
    public HexCell parent;

    public enum PathfindingState { None, Open, Closed, Path }
    public PathfindingState pathfindingState = PathfindingState.None;

    public int fCost
    {
        get { return gCost + hCost; }
    }

    private int heapIndex;

    public int HeapIndex
    {
        get { return heapIndex; }
        set { heapIndex = value; }
    }

    public int CompareTo(HexCell cellToCompare)
    {
        int compare = fCost.CompareTo(cellToCompare.fCost);
        if (compare == 0)
        {
            compare = hCost.CompareTo(cellToCompare.hCost);
        }
       return -compare;
    }

    private void Awake()
    {
        InitializeObjectStorage();
        
        cellRenderer = GetComponent<Renderer>(); 
        if (cellRenderer == null)
        {
            cellRenderer = GetComponentInChildren<Renderer>();
        }
        
        cellColor = cellRenderer != null ? cellRenderer.material.color : Color.white;
    }

    public void ResetPathData()
    {
        gCost = int.MaxValue;
        hCost = 0;
        parent = null;
        pathfindingState = PathfindingState.None;
    }

    private void InitializeObjectStorage()
    {
        gridObjects = new List<GridObject>();
    }

    public bool TryAddGridObject(GridObject obj)
    {
        if (obj.CanBePlacedOn(this))
        {
            gridObjects.Add(obj);
            obj.OnPlacedOnGrid(this);
            return true;
        }
        return false;
    }

    public void RemoveGridObject(GridObject obj)
    {
        if (gridObjects.Remove(obj))
        {
            obj.OnRemovedFromGrid();
        }
    }

    public List<GridObject> GetObjectsByType(GridObjectType type)
    {
        return gridObjects.Where(obj => obj.objectType == type).ToList();
    }

    public List<HexCell> GetNeighbors()
    {
        return parentGrid?.GetNeighbors(axial_coords) ?? new List<HexCell>();
    }

    public bool HasObjectOfType(GridObjectType type)
    {
        return gridObjects.Any(obj => obj.objectType == type);
    }

    public void SetColor(Color color)
    {

        if (cellRenderer != null)
        {
            cellRenderer.material.color = color;
        }
        else
        {
            cellRenderer = GetComponent<Renderer>(); 
            if (cellRenderer != null)
            {
                cellRenderer.material.color = color;
            }
        }
    }
    public Color GetColor()
    {
        return cellColor;
    }
}
