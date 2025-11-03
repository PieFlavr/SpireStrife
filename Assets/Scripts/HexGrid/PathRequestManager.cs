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

    // ------------------- ADD: safe empty list -------------------
    static readonly List<Vector3> s_EmptyWaypoints = new List<Vector3>(0);
    static List<Vector3> SafeWaypoints(List<Vector3> w) => (w != null ? new List<Vector3>(w) : new List<Vector3>(0));
    // ------------------------------------------------------------

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

        // ------------------- CHANGE: normalize waypoints -------------------
        var newRequest = new PathRequest(pathStart, pathEnd, SafeWaypoints(waypoints), callback);
        // ------------------------------------------------------------------
        inst.pathRequestQueue.Enqueue(newRequest);
        inst.TryProcessNext();
    }

    void TryProcessNext() {
        if (!isProcessingPath && pathRequestQueue.Count > 0) {
            currentPathRequest = pathRequestQueue.Dequeue();
            isProcessingPath = true;
            // ------------------- CHANGE: normalize again (defensive) --------
            pathfinding.StartFindPath(currentPathRequest.pathStart,
                                      currentPathRequest.pathEnd,
                                      SafeWaypoints(currentPathRequest.waypoints));
            // ----------------------------------------------------------------
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

    // ------------------- ADD: helpers for spire generator -------------------

    // Convert shortest path to step count (nodes-1). Calls your existing RequestPath.
    public static void RequestShortestPathLength(
        Vector3 start,
        Vector3 end,
        Action<int, bool> callback,
        List<Vector3> waypoints = null)
    {
        RequestPath(start, end, waypoints, (path, success) => {
            if (!success || path == null || path.Length == 0) {
                callback(int.MaxValue, false);
                return;
            }
            int steps = Mathf.Max(0, path.Length - 1);
            callback(steps, true);
        });
    }

    // True if a valid path exists with steps < maxStepsExclusive. Returns (within, steps).
    public static void RequestPathUnder(
        Vector3 start,
        Vector3 end,
        int maxStepsExclusive,
        Action<bool, int> callback,
        List<Vector3> waypoints = null)
    {
        RequestShortestPathLength(start, end, (steps, ok) => {
            bool within = ok && steps < maxStepsExclusive;
            callback(within, steps);
        }, waypoints);
    }
    // -----------------------------------------------------------------------
}
