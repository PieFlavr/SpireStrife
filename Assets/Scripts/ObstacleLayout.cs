using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleLayout", menuName = "HexGrid/Obstacle Layout")]
public class ObstacleLayout : ScriptableObject
{
    [Serializable]
    public struct CellRef
    {
        public Vector3 worldPos;   // primary key
        public int gridIndex;      // fallback if positions moved
    }

    public List<CellRef> cells = new List<CellRef>();
}