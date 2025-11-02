using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public Vector3 position = Vector3.zero;
    public Vector3 velocity = Vector3.zero;
    public float speed;
    public float desiredSpeed;
    public float heading; 
    public float desiredHeading;
    [Header("Const values")]
    public float acceleration;
    public float turnRate;
    public float maxSpeed;
    public float minSpeed;
    public float cruiseSpeed;
    public float mass;
    
    public OrientedPhysics phx = null;
    

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
