
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameMgr : MonoBehaviour
{
	/// <summary>
	/// Singleton instance of GameMgr. Manages spire ownership lists and unit counts.
	/// This is the single source of truth for remaining units and spire ownership.
	/// </summary>
	public static GameMgr Instance { get; private set; }
	
	// Legacy compatibility - will be removed in future
	public static GameMgr inst => Instance;

	[Header("Tracked Spires (auto-populated)")]
	public List<SpireConstruct> playerSpires = new List<SpireConstruct>();
	public List<SpireConstruct> aiSpires = new List<SpireConstruct>();
    public List<SpireConstruct> neutralSpires = new List<SpireConstruct>();
    
    [Header("Unit Counts (single source of truth)")]
    [Tooltip("Total remaining units for player (sum of all player spire reserves)")]
    public int remainingPlayerUnits = 0;
    
    [Tooltip("Total remaining units for AI (sum of all AI spire reserves)")]
    public int remainingAiUnits = 0;

	private void Awake()
	{
		// Enforce singleton pattern - destroy duplicates
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning($"[GameMgr] Duplicate instance detected, destroying {gameObject.name}");
			Destroy(gameObject);
			return;
		}
		
		Instance = this;
	}
	
	private void OnDestroy()
	{
		// Clean up singleton reference
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void OnEnable()
	{
		SpireConstruct.OwnershipChanged += OnSpireOwnershipChanged;
		SpireConstruct.SpirePlaced += OnSpirePlaced;
		SpireConstruct.SpireRemoved += OnSpireRemoved;
	}

	private void OnDisable()
	{
		SpireConstruct.OwnershipChanged -= OnSpireOwnershipChanged;
		SpireConstruct.SpirePlaced -= OnSpirePlaced;
		SpireConstruct.SpireRemoved -= OnSpireRemoved;
	}

	private void Start()
	{
		RebuildSpireLists();
	}

	public void RebuildSpireLists()
	{
		playerSpires.Clear();
		aiSpires.Clear();
		neutralSpires.Clear();

		var all = FindObjectsOfType<SpireConstruct>();
		foreach (var s in all)
		{
			AddSpireToListByTeam(s, s.teamID);
		}
	}

	private void OnSpireOwnershipChanged(SpireConstruct spire, int oldTeam, int newTeam)
	{
		if (spire == null) return;
		RemoveFromAll(spire);
		AddSpireToListByTeam(spire, newTeam);
		// Optional: debug
		// Debug.Log($"GameMgr: Spire changed from team {oldTeam} to {newTeam}; lists updated.");
	}

	private void OnSpirePlaced(SpireConstruct spire)
	{
		if (spire == null) return;
		AddSpireToListByTeam(spire, spire.teamID);
	}

	private void OnSpireRemoved(SpireConstruct spire)
	{
		if (spire == null) return;
		RemoveFromAll(spire);
	}

	private void RemoveFromAll(SpireConstruct s)
	{
		playerSpires.Remove(s);
		aiSpires.Remove(s);
		neutralSpires.Remove(s);
	}

    private void AddSpireToListByTeam(SpireConstruct s, int team)
    {
        if (s == null) return;
        if (team == (int)SpireConstruct.OwnerType.Player)
        {
            if (!playerSpires.Contains(s)) playerSpires.Add(s);
        }
        else if (team == (int)SpireConstruct.OwnerType.AI)
        {
            if (!aiSpires.Contains(s)) aiSpires.Add(s);
        }
        else
        {
            if (!neutralSpires.Contains(s)) neutralSpires.Add(s);
        }
    }
    public void Update()
    {
        // Update unit counts (single source of truth for victory conditions)
        remainingPlayerUnits = 0;
        remainingAiUnits = 0;

        foreach (var spire in playerSpires)
        {
            if (spire != null)
                remainingPlayerUnits += spire.remainingGarrison;
        }
        foreach (var spire in aiSpires)
        {
            if (spire != null)
                remainingAiUnits += spire.remainingGarrison;
        }
        
        // Optional: Log when unit counts change significantly (uncomment for debugging)
        // if (Time.frameCount % 60 == 0) // Log once per second
        // {
        //     Debug.Log($"[GameMgr] Player: {remainingPlayerUnits} units at {playerSpires.Count} spires | AI: {remainingAiUnits} units at {aiSpires.Count} spires");
        // }
    }
}