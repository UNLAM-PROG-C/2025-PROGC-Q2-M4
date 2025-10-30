using System;
using UnityEngine;

[Serializable]
public class NetworkEnvelope
{
    public string type;
    public string payload;
}

[Serializable]
public class HandshakeMessage
{
    public string role; // "server" or "client"
    public string version = "tetris_v1";
}

[Serializable]
public class BoardStateMessage
{
    public int width;
    public int height;

    // Locked tiles: parallel arrays to minimize allocations
    public int lockedCount;
    public int[] lockedX;
    public int[] lockedY;
    public int[] lockedShapeIndex; // index into TetrisBlocks array

    // Active piece
    public bool hasActive;
    public int activeShapeIndex;
    public int activePosX;
    public int activePosY;
    public int[] activeCellOffsetX;
    public int[] activeCellOffsetY;

    // Queue preview (first N upcoming shapes from Board.shapesQueue)
    public int queueLength;
    public int[] upcomingShapes;

    public bool gameOver;
}

public static class NetMessageFactory
{
    public static string Wrap(string type, object inner)
    {
        var env = new NetworkEnvelope
        {
            type = type,
            payload = JsonUtility.ToJson(inner)
        };
        return JsonUtility.ToJson(env) + "\n";
    }

    public static bool TryUnwrap(string raw, out NetworkEnvelope env)
    {
        env = null;
        try
        {
            env = JsonUtility.FromJson<NetworkEnvelope>(raw);
            return env != null;
        }
        catch
        {
            return false;
        }
    }
}