using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CostText : MonoBehaviour
{
    public TextMeshPro costText;
    public int costValue;
    void Start()
    {
        if (costText == null)
        {
            costText = GetComponentInChildren<TextMeshPro>();
        }
    }

    void Update()
    {
        int newCostValue = GetComponentInParent<SpireConstruct>().costToClaim;
        if (costValue != newCostValue)
        {
            costValue = newCostValue;
            costText.text = costValue.ToString();
        }
        if (Camera.main != null)
        {
            costText.transform.LookAt(Camera.main.transform);
            costText.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        }
    }
}
