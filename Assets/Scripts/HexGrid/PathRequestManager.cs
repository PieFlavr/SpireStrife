using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class PathRequestManager : MonoBehaviour {

	Queue<PathRequest> pathRequestQueue = new Queue<PathRequest>();
	PathRequest currentPathRequest;
	
	public static PathRequestManager inst;
    
    // --- CHANGE THIS LINE ---
	// Pathfinding pathfinding;
    HexPathfinder pathfinding; // Use the new HexPathfinder
    
	bool isProcessingPath;

	void Awake() {
		inst = this;
        
        // --- AND CHANGE THIS LINE ---
		// pathfinding = GetComponent<Pathfinding>();
        pathfinding = GetComponent<HexPathfinder>(); // Get the new component
	}

    // ... (The rest of the file is identical and does not need to be changed) ...
	public static void RequestPath(Vector3 pathStart, Vector3 pathEnd, List<Vector3> waypoints, Action<Vector3[], bool> callback) {
    if (inst == null) {
        inst = FindObjectOfType<PathRequestManager>();
        if (inst == null) {
            var go = new GameObject("PathRequestManager");
            inst = go.AddComponent<PathRequestManager>();
        }
    }
    if (inst.pathfinding == null) {
        // This line will now correctly add/get HexPathfinder
        inst.pathfinding = inst.GetComponent<HexPathfinder>() ?? inst.gameObject.AddComponent<HexPathfinder>();
    }

    var newRequest = new PathRequest(pathStart, pathEnd, waypoints, callback);
    inst.pathRequestQueue.Enqueue(newRequest);
    inst.TryProcessNext();
}

void TryProcessNext() {
    if (!isProcessingPath && pathRequestQueue.Count > 0) {
        currentPathRequest = pathRequestQueue.Dequeue();
        isProcessingPath = true;
        pathfinding.StartFindPath(currentPathRequest.pathStart, currentPathRequest.pathEnd, currentPathRequest.waypoints);
		}
	}

	public void FinishedProcessingPath(Vector3[] path, bool success) {
		currentPathRequest.callback(path, success);
		isProcessingPath = false;
		TryProcessNext();
	}

	struct PathRequest {
		public Vector3 pathStart;
		public Vector3 pathEnd;
		public List<Vector3> waypoints;
		public Action<Vector3[], bool> callback;

		public PathRequest(Vector3 _start, Vector3 _end, List<Vector3> _waypoints, Action<Vector3[], bool> _callback) {
			pathStart = _start;
			pathEnd = _end;
			waypoints = _waypoints;
			callback = _callback;
		}
	}
}