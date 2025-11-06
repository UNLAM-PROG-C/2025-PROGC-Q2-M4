// Legacy queue (kept if still referenced elsewhere). Not used directly now since SharedShapesQueue is primary.
// You can remove this file if it is no longer referenced.
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class ShapesQueue {
    private static Queue<int> Shapes = new Queue<int>();
    private static System.Random random = new System.Random();
    private const int TETRIS_PIECE_COUNT = 7;

    public ShapesQueue()
    {
        for (int i = 0; i < 7; i++)
        {
            setShape();
        }
    }

    public int getShape()
    {
        int nextShape = Shapes.Dequeue();
        setShape();
        return nextShape;
    }

    public int peekShape(int position)
    {
        if (position < 0 || position >= Shapes.Count)
            return 0;
        return Shapes.ElementAt(position);
    }

    private void setShape()
    {
        int shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);

        if (Shapes.Count > 0)
        {
            while (shapeIndex == Shapes.Last())
            {
                shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);
            }
        }

        Shapes.Enqueue(shapeIndex);
    }
}