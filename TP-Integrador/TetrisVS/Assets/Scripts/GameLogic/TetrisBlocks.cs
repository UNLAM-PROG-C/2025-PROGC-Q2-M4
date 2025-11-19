using UnityEngine;
using UnityEngine.Tilemaps;

public enum eTetrisBlockShapes
{
  I,
  O,
  T,
  J,
  L,
  S,
  Z
}

[System.Serializable]
public struct TetrisBlockShapeData
{
  public eTetrisBlockShapes Shape;
  public Tile tile;

  public Vector2Int[] cells { get; private set; }
  public Vector2Int[,] wallKicks { get; private set; }

  public void Initialize()
  {
    cells = Data.Cells[Shape];
    wallKicks = Data.WallKicks[Shape];
  }
}