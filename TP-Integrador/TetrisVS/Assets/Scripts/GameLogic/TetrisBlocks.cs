using UnityEngine;
using UnityEngine.Tilemaps;

public enum eTetrisBlockShapes
{
    I, // Line shape
    O, // Square shape
    T,
    J,
    L, // Mirrored J shape
    S,
    Z, // Mirrored S shape
}

[System.Serializable]
public struct TetrisBlockShapeData
{
    public eTetrisBlockShapes Shape;
    public Tile tile;

    public Vector2Int[] cells { get; private set; }

    public void Initialize()
    {
        this.cells = Data.Cells[this.Shape];
    }
}