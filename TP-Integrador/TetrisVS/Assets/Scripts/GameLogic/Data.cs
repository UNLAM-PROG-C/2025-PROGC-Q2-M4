using System.Collections.Generic;
using UnityEngine;

public static class Data
{
  private const float QuarterTurnRadians = Mathf.PI / 2f;

  public static readonly float cos = Mathf.Cos(QuarterTurnRadians);
  public static readonly float sin = Mathf.Sin(QuarterTurnRadians);
  public static readonly float[] RotationMatrix = { cos, sin, -sin, cos };

  public static readonly Dictionary<eTetrisBlockShapes, Vector2Int[]> Cells =
    new Dictionary<eTetrisBlockShapes, Vector2Int[]>
    {
      { eTetrisBlockShapes.I, new[] { new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) } },
      { eTetrisBlockShapes.J, new[] { new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) } },
      { eTetrisBlockShapes.L, new[] { new Vector2Int(1, 1), new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) } },
      { eTetrisBlockShapes.O, new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 0), new Vector2Int(1, 0) } },
      { eTetrisBlockShapes.S, new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 0), new Vector2Int(0, 0) } },
      { eTetrisBlockShapes.T, new[] { new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) } },
      { eTetrisBlockShapes.Z, new[] { new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(0, 0), new Vector2Int(1, 0) } },
    };

  private static readonly Vector2Int[,] WallKicksI =
  {
    { new Vector2Int(0, 0), new Vector2Int(-2, 0), new Vector2Int(1, 0), new Vector2Int(-2, -1), new Vector2Int(1, 2) },
    { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(-1, 0), new Vector2Int(2, 1), new Vector2Int(-1, -2) },
    { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(2, 0), new Vector2Int(-1, 2), new Vector2Int(2, -1) },
    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-2, 0), new Vector2Int(1, -2), new Vector2Int(-2, 1) },
    { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(-1, 0), new Vector2Int(2, 1), new Vector2Int(-1, -2) },
    { new Vector2Int(0, 0), new Vector2Int(-2, 0), new Vector2Int(1, 0), new Vector2Int(-2, -1), new Vector2Int(1, 2) },
    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-2, 0), new Vector2Int(1, -2), new Vector2Int(-2, 1) },
    { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(2, 0), new Vector2Int(-1, 2), new Vector2Int(2, -1) }
  };

  private static readonly Vector2Int[,] WallKicksJLOSTZ =
  {
    { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, -2), new Vector2Int(-1, -2) },
    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, 2), new Vector2Int(1, 2) },
    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, 2), new Vector2Int(1, 2) },
    { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, -2), new Vector2Int(-1, -2) },
    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, -2), new Vector2Int(1, -2) },
    { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, 2), new Vector2Int(-1, 2) },
    { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, 2), new Vector2Int(-1, 2) },
    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, -2), new Vector2Int(1, -2) }
  };

  public static readonly Dictionary<eTetrisBlockShapes, Vector2Int[,]> WallKicks =
    new Dictionary<eTetrisBlockShapes, Vector2Int[,]>
    {
      { eTetrisBlockShapes.I, WallKicksI },
      { eTetrisBlockShapes.J, WallKicksJLOSTZ },
      { eTetrisBlockShapes.L, WallKicksJLOSTZ },
      { eTetrisBlockShapes.O, WallKicksJLOSTZ },
      { eTetrisBlockShapes.S, WallKicksJLOSTZ },
      { eTetrisBlockShapes.T, WallKicksJLOSTZ },
      { eTetrisBlockShapes.Z, WallKicksJLOSTZ }
    };
}