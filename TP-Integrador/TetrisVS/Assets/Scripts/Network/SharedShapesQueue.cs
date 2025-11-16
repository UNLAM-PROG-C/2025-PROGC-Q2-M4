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
// Manages a shared queue of Tetris shapes for multiplayer synchronization
public class SharedShapesQueue
{
    private Queue<int> shapes = new Queue<int>();
    private System.Random random;
    private const int TETRIS_PIECE_COUNT = 7;
    private const int QUEUE_SIZE = 14;

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
// Initialize the queue with a specific seed
    private void InitializeWithSeed(int seed)
    {
        Seed = seed;
        random = new System.Random(seed);
        shapes.Clear();

        for (int i = 0; i < QUEUE_SIZE; i++)
        {
            SetShape();
        }
    }

    public int GetShape()
    {
        int nextShape = shapes.Dequeue();
        SetShape();
        return nextShape;
    }

    public int PeekShape(int position)
    {
        if (position < 0 || position >= shapes.Count)
            return 0;
        return shapes.ElementAt(position);
    }
// Add a new shape to the queue ensuring no immediate repeats
    private void SetShape()
    {
        int shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);

        if (shapes.Count > 0)
        {
            while (shapeIndex == shapes.Last())
            {
                shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);
            }
        }

        shapes.Enqueue(shapeIndex);
    }
// Get the current state of the shape queue
    public QueueStateMessage GetQueueState()
    {
        return new QueueStateMessage
        {// Serialize upcoming shapes and seed
            upcomingShapes = shapes.ToArray(),
            seed = Seed
        };
    }
// Apply a received queue state to synchronize shapes
    public void ApplyQueueState(QueueStateMessage state)
    {
        if (state.seed != Seed)
        {
            InitializeWithSeed(state.seed);
        }

        shapes.Clear();
        foreach (int shape in state.upcomingShapes)
        {// Rebuild the queue from the received state
            shapes.Enqueue(shape);
        }
    }
}