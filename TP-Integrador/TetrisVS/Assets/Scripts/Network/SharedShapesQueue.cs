using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QueueStateMessage
{
  public int[] upcomingShapes;
  public int currentIndex;
  public int seed;
}

/// <summary>
/// Manages a shared queue of Tetris shapes for multiplayer synchronization.
/// </summary>
public class SharedShapesQueue
{
  private const int TetrisPieceCount = 7;
  private const int InitialQueueSize = 14;

  private readonly Queue<int> _shapes = new Queue<int>();
  private System.Random _random;

  public int Seed { get; private set; }

  public SharedShapesQueue()
  {
    Seed = Environment.TickCount;
    InitializeWithSeed(Seed);
  }

  public SharedShapesQueue(int seed)
  {
    InitializeWithSeed(seed);
  }

  private void InitializeWithSeed(int seed)
  {
    Seed = seed;
    _random = new System.Random(seed);
    _shapes.Clear();

    for (int i = 0; i < InitialQueueSize; i++)
    {
      SetShape();
    }
  }

  public int GetShape()
  {
    int nextShape = _shapes.Dequeue();
    SetShape();
    return nextShape;
  }

  public int PeekShape(int position)
  {
    if (position < 0 || position >= _shapes.Count) return 0;
    return _shapes.ElementAt(position);
  }

  private void SetShape()
  {
    int shapeIndex = _random.Next(0, TetrisPieceCount);

    if (_shapes.Count > 0)
    {
      while (shapeIndex == _shapes.Last())
      {
        shapeIndex = _random.Next(0, TetrisPieceCount);
      }
    }

    _shapes.Enqueue(shapeIndex);
  }

  public QueueStateMessage GetQueueState()
  {
    return new QueueStateMessage
    {
      upcomingShapes = _shapes.ToArray(),
      seed = Seed
    };
  }

  public void ApplyQueueState(QueueStateMessage state)
  {
    if (state.seed != Seed)
    {
      InitializeWithSeed(state.seed);
    }

    _shapes.Clear();
    foreach (int shape in state.upcomingShapes)
    {
      _shapes.Enqueue(shape);
    }
  }
}