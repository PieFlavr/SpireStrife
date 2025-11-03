using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveUnits : MonoBehaviour
{
    public static MoveUnits inst;

    [Header("Units managed")]
    public List<Unit> units = new();

    [Header("Potential field params")]
    public float potentialDistanceThreshold = 5.0f;
    public float repulsiveCoefficient = 1.0f;   // stronger push away
    public float repulsiveExponent = 2.0f;      // faster decay with distance
    public float attractionCoefficient = 1.0f;  // stronger pull to target
    public float attractiveExponent = 1.0f;     // 1 = linear with distance

    private readonly Dictionary<Unit, Coroutine> _routines = new();
    private readonly Dictionary<Unit, int> _killStep = new(); // waypoint index at which to destroy this unit

    private void Awake() => inst = this;

    // Cost: 1 per segment between waypoints/tiles
    public static int GetPathCost(Vector3[] path)
    {
        if (path == null || path.Length < 2) return 0;
        return path.Length - 1;
    }

    // Assign kill steps:
    // - At most one unit is destroyed at each intermediate waypoint.
    // - Any leftover units are destroyed at the final waypoint.
    private void AssignKillSteps(List<Unit> group, Vector3[] path)
    {
        _killStep.Clear();
        int edges = Mathf.Max(0, path.Length - 1);
        if (edges == 0) return;

        int idx = 0;
        // Destroy one per intermediate waypoint 1..edges-1
        for (int step = 1; step <= edges - 1 && idx < group.Count; step++, idx++)
            _killStep[group[idx]] = step;

        // Remaining units get destroyed at final step == edges
        for (; idx < group.Count; idx++)
            _killStep[group[idx]] = edges;
    }

    public bool MoveUnitsAlongPath(List<Unit> group, Vector3[] path, float groupSpeed = 0f, bool useMaxSpeedMovement = false)
    {
        if (path == null || path.Length < 2)
        {
            Debug.LogError("Path is null or too short.");
            return false;
        }

        return MoveUnitsAlongPath(group, path, groupSpeed, useMaxSpeedMovement, null);
    }

    // Overload that accepts a callback invoked once when the entire group finishes/destroys
    public bool MoveUnitsAlongPath(List<Unit> group, Vector3[] path, float groupSpeed, bool useMaxSpeedMovement, Action onComplete)
    {
        if (path == null || path.Length < 2)
        {
            Debug.LogError("Path is null or too short.");
            return false;
        }

        units = group;
        AssignKillSteps(group, path);

        int remaining = 0;
        foreach (var u in group)
        {
            if (u == null) continue;
            remaining++;
        }

        void UnitDone()
        {
            remaining = Mathf.Max(remaining - 1, 0);
            if (remaining == 0)
            {
                onComplete?.Invoke();
            }
        }

        foreach (var u in group)
        {
            if (u == null) continue;

            if (_routines.TryGetValue(u, out var running) && running != null)
                StopCoroutine(running);

            var co = StartCoroutine(MoveUnitAlongPath(u, path, groupSpeed, useMaxSpeedMovement, UnitDone));
            _routines[u] = co;
        }

        // Edge case: if group had no valid units, invoke immediately
        if (remaining == 0)
        {
            onComplete?.Invoke();
        }

        return true;
    }

    public IEnumerator MoveUnitAlongPath(Unit unit, Vector3[] path, float groupSpeed, bool useMaxSpeedMovement, Action onComplete)
    {
        const float arriveDist = 0.5f;
        int edges = path.Length - 1;

        // Safety: if no kill step assigned, default to final
        if (!_killStep.TryGetValue(unit, out int killAtStep))
            killAtStep = edges;

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 waypoint = path[i];

            // Compare in world space against world-space waypoint
            while (Vector3.Distance(unit.transform.position, waypoint) > arriveDist)
            {
                float baseSpeed =
                    groupSpeed > 0f
                        ? Mathf.Min(unit.maxSpeed, groupSpeed)
                        : (useMaxSpeedMovement ? unit.maxSpeed : unit.cruiseSpeed);

                // Use world-space toward waypoint; heading measured in world yaw
                var dhds = ComputeDHDSWorld(unit, waypoint, useMaxSpeedMovement);

                unit.desiredHeading = dhds.dhDegrees;
                unit.desiredSpeed = dhds.targetSpeed;

                // physics integration occurs elsewhere
                yield return null;

                unit.transform.localPosition = unit.position; // keep in sync with physics-local
                unit.transform.localEulerAngles = new Vector3(0f, unit.heading, 0f);
            }

            // Snap to the reached waypoint
            // Convert world waypoint to local for position storage
            unit.transform.position = waypoint;
            unit.position = unit.transform.localPosition;

            // Waypoint reached: destroy if this is the unit’s assigned kill step
            if (i == killAtStep && i < edges)
            {
                CleanupAndDestroy(unit, clearSelection: false);
                if (units.Count == 0)
                {
                    UiMgr.inst.ClearSelection();
                }
                onComplete?.Invoke();
                yield break; // stop this coroutine, unit is gone
            }
        }
        
        bool shouldDestroy = CheckForCollisions(unit);
        // Final arrival. If not already destroyed, destroy now.
        if (shouldDestroy)
        {
            CleanupAndDestroy(unit, clearSelection: true);
        }
        else
        {
            // Unit remains as a garrison/idle at the spire; stop its movement and clear coroutine tracking
            unit.desiredSpeed = 0f;
            unit.speed = 0f;
            if (_routines.TryGetValue(unit, out var co) && co != null) StopCoroutine(co);
            _routines.Remove(unit);
            _killStep.Remove(unit);
        }
        onComplete?.Invoke();
    }
    private bool CheckForCollisions(Unit unit)
    {
    float radius = 0.5f; // or use unit.collider.bounds.extents
    // Use world position for physics queries
    Collider[] hits = Physics.OverlapSphere(unit.transform.position, radius);

    foreach (var hit in hits)
    {
        if (hit.transform == unit.transform) continue;
        // Consider any collider that belongs to a SpireConstruct (tag is optional)
        var spire = hit.transform.GetComponentInParent<SpireConstruct>();
        if (spire != null)
        {
            int team = unit.teamID; // 0 = Player, 1 = AI
            spire.CaptureSpire(1, team); 

            
        }
       
    }
    // No relevant collision; default behavior is to destroy at path end
    return true;
}
    private void CleanupAndDestroy(Unit unit, bool clearSelection)
    {
        unit.desiredSpeed = 0f;
        unit.speed = 0f;

        if (_routines.TryGetValue(unit, out var co) && co != null) StopCoroutine(co);
        _routines.Remove(unit);
        _killStep.Remove(unit);
        units.Remove(unit);

        // Your project-specific teardown
        UnitMgr.inst.DestroyUnit(unit);
        if (clearSelection) UiMgr.inst.ClearSelection();

    }

    // ---- Potential-field steering -------------------------------------------

    public struct DHDS
    {
        public float dhDegrees;
        public float targetSpeed;

        public DHDS(float dh, float speed)
        {
            dhDegrees = dh;
            targetSpeed = speed;
        }
    }

    public DHDS ComputePotentialDHDS(Unit entity, Vector3 movePosition, float baseSpeed, bool isWaypoint)
    {
        Vector3 toTarget = movePosition - entity.position;
        float dTarget = toTarget.magnitude;
        if (dTarget < 1e-4f) return new DHDS(entity.heading, 0f);

        Vector3 repulsive = Vector3.zero;
        const float eps = 0.1f;

        foreach (Unit other in units)
        {
            if (other == null || other == entity) continue;

            Vector3 diff = entity.position - other.position;
            float dist = diff.magnitude;
            if (dist <= 1e-4f) continue;

            if (dist < potentialDistanceThreshold)
            {
                float mag = repulsiveCoefficient / Mathf.Pow(dist + eps, repulsiveExponent);
                repulsive += diff.normalized * mag * other.mass;
            }
        }

        Vector3 attractive = toTarget.normalized * (attractionCoefficient * Mathf.Pow(dTarget, attractiveExponent));
        Vector3 steer = attractive + repulsive;
        if (steer.sqrMagnitude < 1e-8f) return new DHDS(entity.heading, 0f);

        float dh = Utils.Degrees360(Mathf.Rad2Deg * Mathf.Atan2(steer.x, steer.z));

        float angleDiff = Utils.Degrees360(Utils.AngleDiffPosNeg(dh, entity.heading));
        float cosValue = (Mathf.Cos(angleDiff * Mathf.Deg2Rad) + 1f) * 0.5f;

        float targetSpeed = isWaypoint ? baseSpeed : baseSpeed * cosValue;
        targetSpeed = Mathf.Clamp(targetSpeed, 0f, entity.maxSpeed);

        return new DHDS(dh, targetSpeed);
    }
    private (float dhDegrees, float targetSpeed) ComputeDHDS(Unit entity, Vector3 movePosition, bool useMaxSpeedMovement)
    {
        Vector3 diffToMovePosition = movePosition - entity.position; // use physics position
        float dhRadians = Mathf.Atan2(diffToMovePosition.x, diffToMovePosition.z);
        float dhDegrees = Utils.Degrees360(Mathf.Rad2Deg * dhRadians);
        float targetSpeed = useMaxSpeedMovement ? entity.maxSpeed : entity.cruiseSpeed;
        return (dhDegrees, targetSpeed);
    }

    private (float dhDegrees, float targetSpeed) ComputeDHDSWorld(Unit entity, Vector3 worldTarget, bool useMaxSpeedMovement)
    {
        Vector3 diffToMovePosition = worldTarget - entity.transform.position; // world-space diff
        float dhRadians = Mathf.Atan2(diffToMovePosition.x, diffToMovePosition.z);
        float dhDegrees = Utils.Degrees360(Mathf.Rad2Deg * dhRadians);
        float targetSpeed = useMaxSpeedMovement ? entity.maxSpeed : entity.cruiseSpeed;
        return (dhDegrees, targetSpeed);
    }

    public void Update()
    {
    }
}
