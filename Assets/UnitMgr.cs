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

    public bool generateUnits(HexCell cell, int count = 10)
    {
        if (cell == null || unitPrefab == null) return false;

        var offsets = GetHexOffsets(count, spacing);
        FitOffsetsInApothem(offsets, apothem - edgeMargin);

        var basePos = cell.transform.position;
        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2 o = offsets[i];
            Vector3 pos = new Vector3(basePos.x + o.x, yHeight, basePos.z + o.y);
            GameObject unitObj = Instantiate(unitPrefab, pos, Quaternion.identity, transform);

            var unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.position = unitObj.transform.localPosition; // keep physics in sync
                unit.speed = 0f;
                unit.desiredSpeed = 0f;
                units.Add(unit);
            }
            else
            {
                Debug.LogError("unitPrefab is missing Unit component.");
            }
        }
        return true;
    }

    // =========================
    // Movement API
    // =========================

    public bool moveUnitsAlongPath(Vector3[] path, float groupSpeed = 0f, bool useMaxSpeedMovement = false)
    {
        var moveUnits = MoveUnits.inst;
        if (moveUnits == null)
        {
            Debug.LogError("MoveUnits instance is not initialized.");
            return false;
        }

        return moveUnits.MoveUnitsAlongPath(units, path, groupSpeed, useMaxSpeedMovement);
    }

    // =========================
    // DH/DS helper
    // =========================

    struct DHDS
    {
        public float dhDegrees;
        public float targetSpeed;
        public DHDS(float d, float s) { dhDegrees = d; targetSpeed = s; }
    }

    static DHDS ComputeDHDS(Unit entity, Vector3 movePosition, float groupSpeed, bool useMaxSpeedMovement)
    {
        Vector3 diffToMovePosition = movePosition - entity.position; // use physics position
        float dhRadians = Mathf.Atan2(diffToMovePosition.x, diffToMovePosition.z);
        float dhDegrees = Utils.Degrees360(Mathf.Rad2Deg * dhRadians);

        // reserved for potential fields if you add them later
        Vector3 potentialSum = Vector3.zero;
        Vector3 repulsivePotential = Vector3.zero;
        Vector3 attractivePotential = Vector3.zero;
        _ = potentialSum; _ = repulsivePotential; _ = attractivePotential;

        float targetSpeed = (groupSpeed > 0f)
            ? groupSpeed
            : (useMaxSpeedMovement ? entity.maxSpeed : entity.cruiseSpeed);

        return new DHDS(dhDegrees, targetSpeed);
    }

    // =========================
    // Hex packing helpers
    // =========================

    List<Vector2> GetHexOffsets(int count, float s)
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

    void FitOffsetsInApothem(List<Vector2> offsets, float targetApothem)
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

    public void DestroyAllUnits()
    {
        foreach (var unit in units)
            if (unit != null) Destroy(unit.gameObject);
        units.Clear();
    }

    public void DestroyUnits(List<Unit> unitsToDestroy)
    {
        foreach (var unit in unitsToDestroy)
        {
            units.Remove(unit);
            if (unit != null) Destroy(unit.gameObject);
        }
    }
}
