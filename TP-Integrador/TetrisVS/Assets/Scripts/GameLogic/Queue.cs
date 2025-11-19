using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Legacy queue (kept if still referenced). SharedShapesQueue is primary now.
public class ShapesQueue
{
  private const int TetrisPieceCount = 7;

  private static readonly Queue<int> Shapes = new Queue<int>();
  private static readonly System.Random Random = new System.Random();

  public ShapesQueue()
  {
    for (int i = 0; i < TetrisPieceCount; i++)
    {
      EnqueueShape();
    }
  }

  public int GetShape()
  {
    int nextShape = Shapes.Dequeue();
    EnqueueShape();
    return nextShape;
  }

  public int PeekShape(int position)
  {
    if (position < 0 || position >= Shapes.Count)
    {
      return 0;
    }
    return Shapes.ElementAt(position);
  }

  private void EnqueueShape()
  {
    int shapeIndex = Random.Next(0, TetrisPieceCount);

    if (Shapes.Count > 0)
    {
      while (shapeIndex == Shapes.Last())
      {
        shapeIndex = Random.Next(0, TetrisPieceCount);
      }
    }

    Shapes.Enqueue(shapeIndex);
  }
}