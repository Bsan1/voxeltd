using System;
using UnityEngine;

[Serializable]
public class GridCellData
{
    public Vector2Int coord;
    public string placeableId;
    public Vector3 worldPos;

    [SerializeField] private string label;
    [SerializeField] private GameObject occupant;

    public Vector2Int Coord
    {
        get => coord;
        set => coord = value;
    }

    public string PlaceableId
    {
        get => placeableId;
        set => placeableId = value;
    }

    public Vector3 WorldPos
    {
        get => worldPos;
        set => worldPos = value;
    }

    public string Label
    {
        get => label;
        set => label = value;
    }

    public GameObject Occupant
    {
        get => occupant;
        set => occupant = value;
    }

    public bool IsOccupied => occupant != null
        || !string.IsNullOrWhiteSpace(label)
        || !string.IsNullOrWhiteSpace(placeableId);

    public GridCellData()
    {
        coord = Vector2Int.zero;
        placeableId = string.Empty;
        worldPos = Vector3.zero;
        label = string.Empty;
        occupant = null;
    }

    public GridCellData(string label, GameObject occupant)
    {
        coord = Vector2Int.zero;
        placeableId = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
        worldPos = occupant != null ? occupant.transform.position : Vector3.zero;
        this.label = label;
        this.occupant = occupant;
    }

    public GridCellData(Vector2Int coord, string placeableId, Vector3 worldPos)
    {
        this.coord = coord;
        this.placeableId = string.IsNullOrWhiteSpace(placeableId) ? "cube" : placeableId;
        this.worldPos = worldPos;
        label = this.placeableId;
        occupant = null;
    }

    public void Clear()
    {
        placeableId = string.Empty;
        label = string.Empty;
        occupant = null;
    }

    public GridCellData Clone()
    {
        return new GridCellData
        {
            coord = coord,
            placeableId = placeableId,
            worldPos = worldPos,
            label = label,
            occupant = occupant
        };
    }
}
