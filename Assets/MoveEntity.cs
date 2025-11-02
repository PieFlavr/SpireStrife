using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveUnits : MonoBehaviour
{
    public static MoveUnits inst;

    public List<Unit> units = new List<Unit>(); // List of units to manage movement and avoid collisions

    public float potentialDistanceThreshold = 5.0f; // Threshold for repulsive potential
    public float repulsiveCoefficient = 1.0f; // Coefficient for repulsive force
    public float repulsiveExponent = 2.0f; // Exponent for repulsive force calculation
    public float attractionCoefficient = 1.0f; // Coefficient for attractive force
    public float attractiveExponent = 1.0f; // Exponent for attractive force calculation

    private void Awake()
    {
        inst = this;
    }

    public bool MoveUnitsAlongPath(List<Unit> units, Vector3[] path, float groupSpeed = 0f, bool useMaxSpeedMovement = false)
    {
        if (path == null || path.Length < 2)
        {
            Debug.LogError("Path is null or too short.");
            return false;
        }
        this.units = units;
        foreach (var unit in units)
        {
            if (unit == null) continue;
            StopCoroutine(MoveUnitAlongPath(unit, path, groupSpeed, useMaxSpeedMovement, null));
            StartCoroutine(MoveUnitAlongPath(unit, path, groupSpeed, useMaxSpeedMovement, null));
        }

        return true;
    }

    public IEnumerator MoveUnitAlongPath(Unit unit, Vector3[] path, float groupSpeed, bool useMaxSpeedMovement, Action onComplete)
    {
        const float arriveDist = 0.5f;

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 waypoint = path[i];

            while (Vector3.Distance(unit.position, waypoint) > arriveDist)
            {
                // Use ComputePotentialDHDS to calculate desired heading and speed
                var dhdsTuple = ComputeDHDS(unit, movePosition: waypoint, useMaxSpeedMovement);
                DHDS dhds = new DHDS(dhdsTuple.dhDegrees, dhdsTuple.targetSpeed);
                unit.desiredHeading = dhds.dhDegrees;
                unit.desiredSpeed = dhds.targetSpeed;

                // Physics step occurs elsewhere (e.g., FixedUpdate)
                yield return null;

                // Keep transform in sync
                unit.transform.localPosition = unit.position;
                unit.transform.localEulerAngles = new Vector3(0f, unit.heading, 0f);
            }

            // Snap to waypoint
            unit.position = waypoint;
            unit.transform.localPosition = unit.position;
        }

        // Stop on final arrival
        unit.desiredSpeed = 0f;
        unit.speed = 0f;
        onComplete?.Invoke();
        units.Remove(unit);
        UnitMgr.inst.DestroyUnit(unit);
        UiMgr.inst.ClearSelection();
    }

    private (float dhDegrees, float targetSpeed) ComputeDHDS(Unit entity, Vector3 movePosition,  bool useMaxSpeedMovement)
    {
        Vector3 diffToMovePosition = movePosition - entity.position; // use physics position
        float dhRadians = Mathf.Atan2(diffToMovePosition.x, diffToMovePosition.z);
        float dhDegrees = Utils.Degrees360(Mathf.Rad2Deg * dhRadians);
        float targetSpeed = useMaxSpeedMovement ? entity.maxSpeed : entity.cruiseSpeed;
        return (dhDegrees, targetSpeed);
    }

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

    public DHDS ComputePotentialDHDS(Unit entity, Vector3 movePosition, bool useMaxSpeedMovement, bool isWaypoint)
    {
        Vector3 diffToMovePosition = movePosition - entity.position;
        Vector3 potentialSum = Vector3.zero;
        Vector3 repulsivePotential = Vector3.zero;
        Vector3 attractivePotential = Vector3.zero;

        // Calculate repulsive potential from other units
        foreach (Unit otherUnit in units)
        {
            if (otherUnit == entity) continue;

            Vector3 diff = entity.position - otherUnit.position;
            float distance = diff.magnitude;

            if (distance < potentialDistanceThreshold && distance > 0f)
            {
                repulsivePotential += diff.normalized * otherUnit.mass *
                    repulsiveCoefficient * Mathf.Pow(distance, repulsiveExponent);
            }
        }

        // Calculate attractive potential towards the target position
        attractivePotential = diffToMovePosition.normalized *
            attractionCoefficient * Mathf.Pow(diffToMovePosition.magnitude, attractiveExponent);

        // Combine potentials
        potentialSum = attractivePotential - repulsivePotential;

        // Calculate desired heading and speed
        float dh = Utils.Degrees360(Mathf.Rad2Deg * Mathf.Atan2(potentialSum.x, potentialSum.z));
        float angleDiff = Utils.Degrees360(Utils.AngleDiffPosNeg(dh, entity.heading));
        float cosValue = (Mathf.Cos(angleDiff * Mathf.Deg2Rad) + 1) / 2.0f;
        float baseSpeed = useMaxSpeedMovement ? entity.maxSpeed : entity.cruiseSpeed;
        float ds = isWaypoint ? baseSpeed : baseSpeed * cosValue;

        return new DHDS(dh, ds);
    }
}