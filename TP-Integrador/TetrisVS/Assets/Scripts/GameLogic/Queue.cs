using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ShapesQueue {
    private static Queue<int> Shapes = new Queue<int>();
    private static System.Random random = new System.Random();
    private const int TETRIS_PIECE_COUNT = 7; // I, O, T, S, Z, J, L

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
            return 0; // Return a safe default
            
        return Shapes.ElementAt(position);
    }

    private void setShape()
    {
        // Generate random index from 0 to 6 (for 7 tetris pieces)
        int shapeIndex = random.Next(0, TETRIS_PIECE_COUNT);

        // Avoid consecutive same pieces
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