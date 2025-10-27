using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleTile : Tile
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    protected override void InitializeGridObject()
    {
        base.InitializeGridObject();
        teamID = 0; // Neutral team
        baseTraversalCost = float.NegativeInfinity; // Impassable
        traversible = false; // Not traversible
    }

    protected override void SetObjectType()
    {
        objectType = GridObjectType.Tile;
    }
}
