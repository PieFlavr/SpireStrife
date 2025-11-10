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

        // Display the remaining garrison at this spire
        costText.text = _spire.remainingGarrison.ToString();

        // Billboard toward camera
        if (Camera.main != null)
        {
            costText.transform.LookAt(Camera.main.transform);
            costText.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        }
    }
}
