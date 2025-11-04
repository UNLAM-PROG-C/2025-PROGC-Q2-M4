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

public class SharedShapesQueue 
{
    private Queue<int> shapes = new Queue<int>();
    private System.Random random;
    private const int TETRIS_PIECE_COUNT = 7;
    private const int QUEUE_SIZE = 14; // Keep more pieces in queue for smoother gameplay
    
    public int Seed { get; private set; }
    
    // For server: initialize with random seed
    public SharedShapesQueue()
    {
        Seed = Environment.TickCount;
        InitializeWithSeed(Seed);
    }
    
    // For client: initialize with server's seed
    public SharedShapesQueue(int seed)
    {
        InitializeWithSeed(seed);
    }
    
    private void InitializeWithSeed(int seed)
    {
        Seed = seed;
        random = new System.Random(seed);
        shapes.Clear();
        
        // Fill initial queue
        for (int i = 0; i < QUEUE_SIZE; i++)
        {
            SetShape();
        }
    }
    
    public int GetShape()
    {
        int nextShape = shapes.Dequeue();
        SetShape(); // Add new shape to maintain queue size
        return nextShape;
    }
    
    public int PeekShape(int position)
    {
        if (position < 0 || position >= shapes.Count)
            return 0;
        return shapes.ElementAt(position);
    }
    
    private void SetShape()
    {
        int shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);
        
        // Avoid consecutive same pieces
        if (shapes.Count > 0)
        {
            while (shapeIndex == shapes.Last())
            {
                shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);
            }
        }
        
        shapes.Enqueue(shapeIndex);
    }
    
    // Get current queue state for synchronization
    public QueueStateMessage GetQueueState()
    {
        return new QueueStateMessage
        {
            upcomingShapes = shapes.ToArray(),
            seed = Seed
        };
    }
    
    // Apply received queue state (for clients)
    public void ApplyQueueState(QueueStateMessage state)
    {
        if (state.seed != Seed)
        {
            // Reinitialize with new seed if needed
            InitializeWithSeed(state.seed);
        }
        
        // Sync the queue
        shapes.Clear();
        foreach (int shape in state.upcomingShapes)
        {
            shapes.Enqueue(shape);
        }
    }
}