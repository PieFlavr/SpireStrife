using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ObstacleGenerator : MonoBehaviour
{
    [Header("Grid References")]
    public HexGrid grid; // Assign in Inspector

    [Header("Random Obstacles")]
    [Tooltip("How many random cells to elevate and mark as obstacles.")]
    public int cellsToElevateAsObstacles = 15;

    [Header("Obstacle Shape")]
    [Tooltip("Random height range for obstacle cells.")]
    public Vector2 heightRange = new Vector2(0.5f, 1f);

    [Header("Obstacle Appearance")]
    [Tooltip("Layer name to assign to obstacle cells.")]
    public string obstacleLayerName = "Obstacle";
    [Tooltip("Fallback base color restore if no renderer color exists on cell.")]
    public Color defaultBaseColor = Color.white;

    [Header("Saved Layout")]
    [Tooltip("Path to save/load obstacle layout JSON file.")]
    public string layoutJsonPath = "Assets/ObstacleLayout.json";
    [Tooltip("Max distance to match a saved cell to a live cell by position.")]
    public float matchEpsilon = 0.05f;

    [System.Serializable]
    public class ObstacleLayoutData
    {
        [System.Serializable]
        public struct CellRef
        {
            public Vector3 worldPos;   // primary key
            public int gridIndex;      // fallback if positions moved
        }

        public List<CellRef> cells = new List<CellRef>();
    }

    // --- Public API ---

    [ContextMenu("Create Random Obstacles")]
    public void CreateObstacles()
    {
        EnsureGrid();

        if (grid == null)
        {
            Debug.LogError("ObstacleGenerator: No HexGrid found in the scene.");
            return;
        }

        ModifyRandomCells();
    }

    [ContextMenu("Clear Obstacles")]
    public void ClearObstacles()
    {
        EnsureGrid();

        if (grid == null)
        {
            Debug.LogError("ObstacleGenerator: No HexGrid found in the scene.");
            return;
        }

        int restored = 0;
        foreach (var marker in grid.GetComponentsInChildren<ObstacleMarker>(true))
        {
            var cell = marker.GetComponent<HexCell>();
            if (cell == null) continue;

            RestoreCell(cell, marker);
            restored++;
        }

        Debug.Log($"ObstacleGenerator: Cleared {restored} obstacle cells.");
    }

    // --- Editor paint hook (called by custom editor) ---
    public void PaintObstacleAtRay(Ray worldRay)
    {
        if (!Physics.Raycast(worldRay, out var hit, Mathf.Infinity))
            return;

        var cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell == null) return;

        // Toggle only to "make obstacle" per request.
        if (cell.GetComponent<ObstacleMarker>() != null)
            return;

        ApplyObstacle(cell);
    }

    // --- Internal ---

    void EnsureGrid()
    {
        if (grid == null) grid = FindObjectOfType<HexGrid>();
    }

    void ModifyRandomCells()
    {
        List<HexCell> allCells = grid.GetAllCells().ToList();
        int modifiedCount = 0;

        for (int i = 0; i < cellsToElevateAsObstacles; i++)
        {
            if (allCells.Count == 0)
            {
                Debug.LogWarning("ObstacleGenerator: Ran out of cells to modify.");
                break;
            }

            int idx = Random.Range(0, allCells.Count);
            HexCell cell = allCells[idx];
            allCells.RemoveAt(idx);

            if (cell.GetComponent<ObstacleMarker>() != null)
                continue; // already obstacle

            ApplyObstacle(cell);
            modifiedCount++;
        }

        Debug.Log($"ObstacleGenerator: Modified {modifiedCount} cells to obstacles.");
    }

    void ApplyObstacle(HexCell cell)
    {
        // Cache originals
        var marker = cell.gameObject.AddComponent<ObstacleMarker>();
        marker.originalScale = cell.transform.localScale;
        marker.originalY = cell.transform.position.y;
        marker.originalLayer = cell.gameObject.layer;
        marker.originalWalkable = GetWalkable(cell);
        marker.originalColor = GetCellColor(cell, defaultBaseColor);

        // Random height
        float minH = Mathf.Min(heightRange.x, heightRange.y);
        float maxH = Mathf.Max(heightRange.x, heightRange.y);
        float h = Random.Range(minH, maxH);

        // Elevate
        var s = cell.transform.localScale;
        cell.transform.localScale = new Vector3(s.x, h, s.z);
        var p = cell.transform.position;
        cell.transform.position = new Vector3(p.x, h * 0.5f, p.z);

        // Layer + walkable flag
        int obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
        if (obstacleLayer >= 0) cell.gameObject.layer = obstacleLayer;
        SetWalkable(cell, false);

        // Random red
        float hue = (Random.value < 0.5f) ? Random.Range(0f, 0.05f) : Random.Range(0.95f, 1f);
        float sat = Random.Range(0.6f, 1f);
        float val = Random.Range(0.5f, 1f);
        Color randomRed = Color.HSVToRGB(hue, sat, val);
        SetCellColor(cell, randomRed);

        MarkDirty(cell);
    }

    void RestoreCell(HexCell cell, ObstacleMarker marker)
    {
        // Restore transform
        cell.transform.localScale = marker.originalScale;
        var p = cell.transform.position;
        cell.transform.position = new Vector3(p.x, marker.originalY, p.z);

        // Restore layer + walkable + color
        cell.gameObject.layer = marker.originalLayer;
        SetWalkable(cell, marker.originalWalkable);
        SetCellColor(cell, marker.originalColor);

#if UNITY_EDITOR
        Undo.DestroyObjectImmediate(marker);
#else
        Destroy(marker);
#endif

        MarkDirty(cell);
    }

    static bool GetWalkable(HexCell cell)
    {
        // If HexCell exposes isWalkable, use it. Otherwise assume walkable.
        var f = cell.GetType().GetField("isWalkable");
        if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(cell);
        return true;
    }

    static void SetWalkable(HexCell cell, bool value)
    {
        var f = cell.GetType().GetField("isWalkable");
        if (f != null && f.FieldType == typeof(bool)) f.SetValue(cell, value);
    }

    static Color GetCellColor(HexCell cell, Color fallback)
    {
        // Prefer a Renderer color if SetColor/GetColor are not available
        var mGet = cell.GetType().GetMethod("GetColor", System.Type.EmptyTypes);
        if (mGet != null && mGet.ReturnType == typeof(Color)) return (Color)mGet.Invoke(cell, null);

        var rend = cell.GetComponentInChildren<Renderer>();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color"))
            return rend.sharedMaterial.color;

        return fallback;
    }

    static void SetCellColor(HexCell cell, Color c)
    {
        var mSet = cell.GetType().GetMethod("SetColor", new[] { typeof(Color) });
        if (mSet != null) { mSet.Invoke(cell, new object[] { c }); return; }

        var rend = cell.GetComponentInChildren<Renderer>();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color"))
        {
#if UNITY_EDITOR
            Undo.RecordObject(rend.sharedMaterial, "Set Cell Color");
#endif
            rend.sharedMaterial.color = c;
        }
    }

    static void MarkDirty(Object o)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) EditorUtility.SetDirty(o);
#endif
    }

    // --- Save current obstacles into JSON file ---
    [ContextMenu("Save Obstacles -> JSON File")]
    public void SaveCurrentObstaclesToJson()
    {
#if UNITY_EDITOR
        EnsureGrid(); if (grid == null) { Debug.LogError("No HexGrid."); return; }

        var layoutData = new ObstacleLayoutData();
        var all = grid.GetAllCells().ToList();

        for (int i = 0; i < all.Count; i++)
        {
            var cell = all[i];
            if (cell.GetComponent<ObstacleMarker>() == null) continue;

            layoutData.cells.Add(new ObstacleLayoutData.CellRef
            {
                worldPos = cell.transform.position,
                gridIndex = i
            });
        }

        string json = JsonUtility.ToJson(layoutData, true);
        System.IO.File.WriteAllText(layoutJsonPath, json);
        Debug.Log($"Saved {layoutData.cells.Count} obstacle cells to JSON file at '{layoutJsonPath}'.");
#else
        Debug.LogWarning("Saving requires the Unity Editor.");
#endif
    }

    // --- Save current obstacles into JSON file with custom name ---
    [ContextMenu("Save Obstacles As Custom JSON File...")]
    public void SaveCurrentObstaclesToCustomJson()
    {
#if UNITY_EDITOR
        EnsureGrid(); if (grid == null) { Debug.LogError("No HexGrid."); return; }

        string path = UnityEditor.EditorUtility.SaveFilePanel(
            "Save Obstacle Layout", "Assets", "ObstacleLayout", "json");
        if (string.IsNullOrEmpty(path)) return;

        var layoutData = new ObstacleLayoutData();
        var all = grid.GetAllCells().ToList();

        for (int i = 0; i < all.Count; i++)
        {
            var cell = all[i];
            if (cell.GetComponent<ObstacleMarker>() == null) continue;

            layoutData.cells.Add(new ObstacleLayoutData.CellRef
            {
                worldPos = cell.transform.position,
                gridIndex = i
            });
        }

        string json = JsonUtility.ToJson(layoutData, true);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"Saved {layoutData.cells.Count} obstacle cells to JSON file at '{path}'.");
#else
        Debug.LogWarning("Saving requires the Unity Editor.");
#endif
    }

    // --- Load obstacles from JSON file ---
    [ContextMenu("Load Obstacles <- JSON File")]
    public void LoadObstaclesFromJson()
    {
        EnsureGrid(); if (grid == null) { Debug.LogError("No HexGrid."); return; }

        if (!System.IO.File.Exists(layoutJsonPath))
        {
            Debug.LogError($"JSON file not found at '{layoutJsonPath}'.");
            return;
        }

        string json = System.IO.File.ReadAllText(layoutJsonPath);
        var layoutData = JsonUtility.FromJson<ObstacleLayoutData>(json);

        // Start from clean state
        ClearObstacles();

        // Build lookup of live cells
        var all = grid.GetAllCells().ToList();

        int applied = 0;
        foreach (var cellRef in layoutData.cells)
        {
            HexCell cell = null;

            // 1) match by position within epsilon
            for (int i = 0; i < all.Count; i++)
            {
                if (Vector3.Distance(all[i].transform.position, cellRef.worldPos) <= matchEpsilon)
                {
                    cell = all[i];
                    break;
                }
            }

            // 2) fallback by saved index if valid
            if (cell == null && cellRef.gridIndex >= 0 && cellRef.gridIndex < all.Count)
                cell = all[cellRef.gridIndex];

            if (cell == null) continue;
            if (cell.GetComponent<ObstacleMarker>() != null) continue;

            ApplyObstacle(cell);
            applied++;
        }

        Debug.Log($"Loaded layout from JSON file. Obstacles placed: {applied}.");
    }
}

public class ObstacleMarker : MonoBehaviour
{
    public Vector3 originalScale;
    public float originalY;
    public int originalLayer;
    public bool originalWalkable;
    public Color originalColor;
}
