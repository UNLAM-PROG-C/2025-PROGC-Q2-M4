using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ShapesQueue {
    private Queue<int> Shapes = new Queue<int>();

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