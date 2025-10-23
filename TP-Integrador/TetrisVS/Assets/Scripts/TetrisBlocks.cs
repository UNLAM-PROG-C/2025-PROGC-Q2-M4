using UnityEngine;
using UnityEngine.Tilemaps;

public enum eTetrisBlocksShapes 
{
    I,
    O,
    T,
    J,
    L,
    S,
    Z,
}

[System.Serializable]
public struct TetrisBlockShapeData
{
    public eTetrisBlocksShapes Shape;
    public Tile tile;

    public Vector2Int[] cells { get; private set; } //hack for Serializable to not show up in unity

    public void Initialize()
    {
        this.cells = Data.Cells[this.Shape];
    }
}