using System.Collections.Generic;
using UnityEngine;

public class UnitMgr : MonoBehaviour
{
    public static UnitMgr inst;
    public List<Unit> units = new List<Unit>();
    public GameObject unitPrefab;

    [Header("In-cell hex packing")]
    public float spacing = 0.35f;   // lattice neighbor distance
    public float apothem = 0.5f;    // center-to-edge distance of your hex in world units
    public float yHeight = 0f;   // unit Y placement
    public float edgeMargin = 0.05f;

    void Start() { inst = this; }

    struct DHDS
    {
        public float dhDegrees;
        public float targetSpeed;
        public DHDS(float d, float s) { dhDegrees = d; targetSpeed = s; }
    }

    // =========================
    // Hex packing helpers
    // =========================

    public List<Vector2> GetHexOffsets(int count, float s)
    {
        var result = new List<Vector2>(count);
        if (count <= 0) return result;

        Vector2 AxialToXZ(int q, int r)
        {
            float x = s * (Mathf.Sqrt(3f) * (q + r * 0.5f));
            float z = s * (1.5f * r);
            return new Vector2(x, z);
        }

        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1)
        };

        result.Add(Vector2.zero);
        if (result.Count >= count) return result;

        int ring = 1;
        while (result.Count < count)
        {
            int q = -ring;
            int r = ring; // start corner
            for (int side = 0; side < 6 && result.Count < count; side++)
            {
                for (int step = 0; step < ring && result.Count < count; step++)
                {
                    result.Add(AxialToXZ(q, r));
                    q += dirs[side].x;
                    r += dirs[side].y;
                }
            }
            ring++;
        }
        return result;
    }

    public void FitOffsetsInApothem(List<Vector2> offsets, float targetApothem)
    {
        float maxR = 0f;
        for (int i = 0; i < offsets.Count; i++)
            maxR = Mathf.Max(maxR, offsets[i].magnitude);

        if (maxR <= targetApothem || maxR <= 0f) return;

        float scale = targetApothem / maxR;
        for (int i = 0; i < offsets.Count; i++)
            offsets[i] *= scale;
    }

    // =========================
    // Cleanup helpers
    // =========================

    public void DestroyUnit(Unit unit)
    {
        units.Remove(unit);
        if (unit != null) Destroy(unit.gameObject);
    }
}
