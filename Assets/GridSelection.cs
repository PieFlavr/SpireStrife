using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSelection : MonoBehaviour
{
    public HexGrid grid; // Assign in Inspector
    private HexCell currentlySelectedCell;
    private HexCell currentlyTargetCell;
    private Color previousSelectedColor;
    private Color previousTargetColor;
    private Color selectedColor = Color.blue;
    private Color targetColor = Color.yellow;

    void Start()
    {
        if (grid == null)
        {
            grid = FindObjectOfType<HexGrid>();
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                HexCell clickedCell = hit.collider.GetComponent<HexCell>();

                if (clickedCell != null && clickedCell.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
                {
                    SelectCell(clickedCell);
                }
            }
        }

        if (Input.GetMouseButtonDown(1)) // Right mouse button
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                HexCell clickedCell = hit.collider.GetComponent<HexCell>();

                if (clickedCell != null && clickedCell.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
                {
                    SelectTargetCell(clickedCell);
                }
            }
        }
    }

    private void SelectCell(HexCell cell)
    {
        // Prevent selecting the same cell as target
        if (cell == currentlyTargetCell)
        {
            return;
        }

        // Deselect previous cell
        if (currentlySelectedCell != null)
        {
            currentlySelectedCell.SetColor(previousSelectedColor);
        }

        // Select new cell
        currentlySelectedCell = cell;
        previousSelectedColor = currentlySelectedCell.GetColor();
        currentlySelectedCell.SetColor(selectedColor);
    }

    private void SelectTargetCell(HexCell cell)
    {
        // Prevent selecting the same cell as selected
        if (cell == currentlySelectedCell)
        {
            return;
        }

        // Deselect previous target
        if (currentlyTargetCell != null )
        {
            currentlyTargetCell.SetColor(previousTargetColor);
        }


        // Select new target
        currentlyTargetCell = cell;
        previousTargetColor = currentlyTargetCell.GetColor();
        currentlyTargetCell.SetColor(targetColor);
    }
}
