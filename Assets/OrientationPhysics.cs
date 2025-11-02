using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrientedPhysics : MonoBehaviour
{
    public Unit unit;
    
    public virtual void Awake() {
        unit = GetComponentInParent<Unit>();
        unit.phx = this;
        unit.position = unit.transform.localPosition;
    }

    // Start is called before the first frame update
    void Start()
    {

    }




    // FixedUpdate is called once per frame
    public virtual void FixedUpdate()
    {
        // Speed update
        float speedChangeAmount = unit.acceleration * Time.fixedDeltaTime * Time.timeScale;
        unit.speed = Mathf.MoveTowards(unit.speed, unit.desiredSpeed, speedChangeAmount);
        unit.speed = Utils.Clamp(unit.speed, unit.minSpeed, unit.maxSpeed); // Ensure speed stays within defined limits

        // Heading update

        float angleChangeAmount = unit.turnRate * Time.fixedDeltaTime * Time.timeScale; // Adjust angle change based on turn rate and time scale
        unit.heading = Mathf.MoveTowardsAngle(unit.heading, unit.desiredHeading, angleChangeAmount);
        unit.heading = Utils.Degrees360(unit.heading); // Normalize heading to 0-360 range

        // Calculate velocity vector based on new speed and heading
        unit.velocity.x = Mathf.Sin(unit.heading * Mathf.Deg2Rad) * unit.speed;
        unit.velocity.y = 0; // Assuming 2D movement on XZ plane, so Y velocity is zero
        unit.velocity.z = Mathf.Cos(unit.heading * Mathf.Deg2Rad) * unit.speed;

        // Update position
        unit.position += unit.velocity * Time.fixedDeltaTime * Time.timeScale; // Use compound assignment
        unit.transform.localPosition = unit.position;

        // Update GameObject's rotation
        eulerRotation.y = unit.heading;
        unit.transform.localEulerAngles = eulerRotation;
    }

    public Vector3 eulerRotation = Vector3.zero;

}
