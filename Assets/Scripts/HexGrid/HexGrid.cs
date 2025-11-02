using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class HexGrid : MonoBehaviour
{
    [Header("Grid Configuration")]
    [Min(0)]
    public Vector2Int gridSize = new Vector2Int(10, 10); // x used as radius

    [Min(0.0001f)]
    public float cellSpacing = 1.0f; // acts as hex size

    [Header("Cell Prefab")]
    public GameObject hexCellPrefab;
    [System.Serializable]
    public struct CellEntry
    {
        public Vector2Int key;
        public HexCell value;
    }

    [SerializeField]
    private SerializableDictionary<Vector2Int, HexCell> cells = new SerializableDictionary<Vector2Int, HexCell>();
    public int MaxSize => cells.Count;



    // axial directions for pointy-top hexes
    private static readonly Vector2Int[] axialDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1)
    };

    private const float Sqrt3 = 1.7320508075688772f;

    private void Awake()
    {
        ReindexFromChildren();
    }
    public void ReindexFromChildren()
{
    cells.Clear();
    foreach (var c in GetComponentsInChildren<HexCell>(true))
    {
        c.parentGrid = this;
        if (!cells.ContainsKey(c.axial_coords))
            cells[c.axial_coords] = c;
        else
            Debug.LogWarning($"Duplicate axial {c.axial_coords}");
    }
}

    private void InitializeGrid()
    {
        if (hexCellPrefab == null)
        {
            Debug.LogError("HexGrid: No hexCellPrefab assigned.");
            return;
        }

        CreateHexCells();
    }

    private void CreateHexCells()
    {
        int radius = Mathf.Max(0, gridSize.x);

        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);

            for (int r = r1; r <= r2; r++)
            {
                var axialCoord = new Vector2Int(q, r);
                var worldPos = AxialToWorldPosition(axialCoord);

                var cellObj = Application.isPlaying
                    ? Instantiate(hexCellPrefab, worldPos, Quaternion.identity, transform)
                    : InstantiatePrefabInEditor(hexCellPrefab, worldPos, transform);

                cellObj.name = $"HexCell_{q}_{r}";
                cellObj.SetActive(true);
                cellObj.transform.localScale = new Vector3(cellSpacing * 2f, 0.1f, cellSpacing * 2f);

                if (!cellObj.TryGetComponent<HexCell>(out var cell))
                {
                    Debug.LogError($"HexGrid: Prefab lacks HexCell component at {axialCoord}.");
                    continue;
                }

                
                if (cell == null)
                {
                    Debug.LogError($"HexGrid: No HexCell component found at {axialCoord}.");
                    continue;
                }
                cell.axial_coords = axialCoord;
                cell.parentGrid = this;
                cells[axialCoord] = cell;
                
                

                // Optional tint to visualize
                var rend = cell.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    float randomGreen = Random.Range(0.3f, 1.0f);
                    cell.cellColor = new Color(0f, randomGreen, 0f);
                    cell.parent = cell;
                    cell.SetColor(cell.cellColor);

                }
                
            }
        }
    }

    // Editor-safe instantiate
    private static GameObject InstantiatePrefabInEditor(GameObject prefab, Vector3 worldPos, Transform parent)
    {
        GameObject go = Instantiate(prefab, parent);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.identity;
        return go;
    }

    public Vector3 AxialToWorldPosition(Vector2Int axialCoord)
    {
        // pointy-top axial to world
        float x = cellSpacing * (1.5f * axialCoord.x);
        float z = cellSpacing * (Sqrt3 * (axialCoord.y + 0.5f * axialCoord.x));
        return new Vector3(x, 0f, z);
    }

    public HexCell GetCell(Vector2Int axialCoord)
    {
        cells.TryGetValue(axialCoord, out var cell);
        return cell;
    }

    public List<HexCell> GetNeighbors(Vector2Int axialCoord)
    {
        var neighbors = new List<HexCell>(6);
        foreach (var dir in axialDirections)
        {
            var n = GetCell(axialCoord + dir);
            if (n != null) neighbors.Add(n);
        }
        return neighbors;
    }

    public bool IsValidCoordinate(Vector2Int axialCoord) => cells.ContainsKey(axialCoord);

    public IEnumerable<HexCell> GetAllCells() => cells.Values;

   

    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        ClearGrid();
        InitializeGrid();
    }
    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        cells.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var childObj = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(childObj);
            else
                DestroyImmediate(childObj);
        }
    }
    public Vector2Int WorldToAxial(Vector3 pos)
    {
        // inverse of AxialToWorld for pointy-top
        float qf = (2f / 3f) * pos.x / cellSpacing;
        float rf = (-1f / 3f) * pos.x / cellSpacing + (1f / Sqrt3) * pos.z / cellSpacing;
        return CubeRound_Axial(qf, rf);
    }

    private static Vector2Int CubeRound_Axial(float qf, float rf)
    {
        float xf = qf;
        float zf = rf;
        float yf = -xf - zf;

        int xi = Mathf.RoundToInt(xf);
        int yi = Mathf.RoundToInt(yf);
        int zi = Mathf.RoundToInt(zf);

        float dx = Mathf.Abs(xi - xf);
        float dy = Mathf.Abs(yi - yf);
        float dz = Mathf.Abs(zi - zf);

        if (dx > dy && dx > dz) xi = -yi - zi;
        else if (dy > dz) yi = -xi - zi;
        else zi = -xi - yi;

        return new Vector2Int(xi, zi);
    }

    public List<HexCell> GetCellsInRadius(Vector2Int center, int radius)
    {
        var results = new List<HexCell>();
        for (int dq = -radius; dq <= radius; dq++)
        {
            int rMin = Mathf.Max(-radius, -dq - radius);
            int rMax = Mathf.Min(radius, -dq + radius);
            for (int dr = rMin; dr <= rMax; dr++)
            {
                var coord = new Vector2Int(center.x + dq, center.y + dr);
                var cell = GetCell(coord);
                if (cell != null) results.Add(cell);
            }
        }
        return results;
    }

    public HexCell GetCellFromWorldPosition(Vector3 worldPos)
    {
        // robust: convert position to axial and look up
        var axial = WorldToAxial(worldPos);
        var cell = GetCell(axial);
        if (cell == null)
            Debug.LogWarning($"HexGrid.GetCellFromWorldPosition: No cell at {worldPos} (axial {axial}).");
        return cell;
    }

    public List<HexCell> GetCellsAlongLine(HexCell start, HexCell end)
    {
        var results = new List<HexCell>();
        int N = GetDistance(start, end);

        Vector2 startF = new Vector2(start.axial_coords.x, start.axial_coords.y);
        Vector2 endF = new Vector2(end.axial_coords.x, end.axial_coords.y);

        for (int i = 0; i <= N; i++)
        {
            float t = N == 0 ? 0f : i / (float)N;
            Vector2 lerpedAxialF = Vector2.Lerp(startF, endF, t);
            Vector2Int roundedCoord = CubeRound_Axial(lerpedAxialF.x, lerpedAxialF.y);

            var cell = GetCell(roundedCoord);
            if (cell != null && !results.Contains(cell))
            {
                // respect walkability if the component exposes it
                bool walkable = true;
                // If HexCell has isWalkable, honor it
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
                walkable = !cell.TryGetComponent<HexCell>(out var _tmp) ? true : cell.isWalkable;
#endif
                if (walkable) results.Add(cell);
            }
        }
        return results;
    }

    public int GetDistance(HexCell a, HexCell b)
    {
        Vector2Int ac = a.axial_coords;
        Vector2Int bc = b.axial_coords;

        int aq = ac.x;
        int ar = ac.y;
        int as_ = -aq - ar;

        int bq = bc.x;
        int br = bc.y;
        int bs_ = -bq - br;

        return (Mathf.Abs(aq - bq) + Mathf.Abs(ar - br) + Mathf.Abs(as_ - bs_)) / 2;
    }
}

[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField]
    private List<TKey> keys = new();

    [SerializeField]
    private List<TValue> values = new();

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();

        if (keys.Count != values.Count)
            Debug.LogError("Mismatched key/value count in SerializableDictionary");

        for (int i = 0; i < keys.Count; i++)
        {
            this[keys[i]] = values[i];
        }
    }
}
