using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CostText : MonoBehaviour
{
    [Tooltip("TMP component used to display remaining claim cost (general, not team-specific).")]
    public TextMeshPro costText;

    [Tooltip("Deprecated: previously showed remaining for a specific team; now ignored.")]
    public int displayTeamID = 0;

    private SpireConstruct _spire;
    private int _lastDisplayed = int.MinValue;

    void Start()
    {
        if (costText == null)
        {
            costText = GetComponentInChildren<TextMeshPro>();
        }
        _spire = GetComponentInParent<SpireConstruct>();
    }

    void Update()
    {
        if (_spire == null || costText == null) return;

        // General remaining to capture, independent of team:
        // remaining = max(0, costToClaim - max(claimProgress across all teams))
        int remaining = 0;
        costText.text = _spire.remainingGarrison.ToString();

        // Billboard toward camera
        if (Camera.main != null)
        {
            costText.transform.LookAt(Camera.main.transform);
            costText.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        }
    }
}
