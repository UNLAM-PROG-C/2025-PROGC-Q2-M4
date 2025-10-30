using System;
using UnityEngine;

[Serializable] public class NetworkEnvelope { public string type; public string payload; }
[Serializable] public class HandshakeMessage { public string role; public string version = "tetris_vs_v1"; }
[Serializable] public class BoardStateMessage
{
    public string owner;
    public int width;
    public int height;
    public int lockedCount;
    public int[] lockedX;
    public int[] lockedY;
    public int[] lockedShapeIndex;
    public bool hasActive;
    public int activeShapeIndex;
    public int activePosX;
    public int activePosY;
    public int[] activeCellOffsetX;
    public int[] activeCellOffsetY;
    public int queueLength;
    public int[] upcomingShapes;
    public bool gameOver;
}

public static class NetMessageFactory
{
    public static string Wrap(string type, object obj)
    {
        var env = new NetworkEnvelope { type = type, payload = JsonUtility.ToJson(obj) };
        return JsonUtility.ToJson(env) + "\n";
    }
    public static bool TryUnwrap(string raw, out NetworkEnvelope env)
    {
        env = null;
        try { env = JsonUtility.FromJson<NetworkEnvelope>(raw); return env != null; }
        catch { return false; }
    }
}