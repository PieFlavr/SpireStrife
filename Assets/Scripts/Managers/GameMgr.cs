
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameMgr : MonoBehaviour
{
	public static GameMgr inst;

	[Header("Tracked Spires (auto-populated)")]
	public List<SpireConstruct> playerSpires = new List<SpireConstruct>();
	public List<SpireConstruct> aiSpires = new List<SpireConstruct>();
    public List<SpireConstruct> neutralSpires = new List<SpireConstruct>();
    public int remainingPlayerUnits = 0;
    public int remainingAiUnits = 0;

	private void Awake()
	{
		inst = this;
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
        remainingPlayerUnits = 0;
        remainingAiUnits = 0;

        foreach (var spire in playerSpires)
        {
            remainingPlayerUnits += spire.remainingGarrison;
        }
        foreach (var spire in aiSpires)
        {
            remainingAiUnits += spire.remainingGarrison;
        }
    }
}