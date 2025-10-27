using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ShapesQueue {
    private static Queue<int> Shapes = new Queue<int>();

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
        return Shapes.ElementAt(position);
    }

    private void setShape()
    {
        Array values = Enum.GetValues(typeof(eTetrisBlockShapes));
        System.Random random = new System.Random();
        int shapeIndex = shapeIndex = random.Next(0, values.Length);

        if (Shapes.Count() > 0)
        {
            while (shapeIndex == Shapes.Last())
            {
                shapeIndex = random.Next(0, values.Length);
            }
        }

        Shapes.Enqueue(shapeIndex);
    }
}