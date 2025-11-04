using System;

[Serializable]
public class NetworkMessageEnvelope
{
    public string type;
    public string payload;
}

[Serializable]
public class BoardStateMessage
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

[Serializable]
public class PlayerActionMessage
{
    public string action;
    public string data;
    public float timestamp;
}

[Serializable]
public class GameStateMessage
{
    public string state;
    public int score;
    public int level;
    public int lines;
    public float timestamp;
}

[Serializable]
public class HandshakeMessage
{
    public string role;
}

public static class NetMessageFactory
{
    public static string Wrap(string type, object payload)
    {
        var envelope = new NetworkMessageEnvelope
        {
            type = type,
            payload = UnityEngine.JsonUtility.ToJson(payload)
        };
        return UnityEngine.JsonUtility.ToJson(envelope) + "\n";
    }

    public static bool TryUnwrap(string raw, out NetworkMessageEnvelope envelope)
    {
        envelope = null;
        try
        {
            envelope = UnityEngine.JsonUtility.FromJson<NetworkMessageEnvelope>(raw.Trim());
            return envelope != null && !string.IsNullOrEmpty(envelope.type);
        }
        catch
        {
            return false;
        }
    }
}