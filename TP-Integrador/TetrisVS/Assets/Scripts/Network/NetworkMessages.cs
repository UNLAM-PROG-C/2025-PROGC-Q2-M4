using System;
using UnityEngine;

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
public class QueueSyncMessage
{
  public int[] upcomingShapes;
  public int seed;
}

[Serializable]
public class GameStateUpdateMessage
{
  public string gameState;
  public int connectedClients;
  public float timestamp;
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

[Serializable]
public class GarbageMessage
{
  public int count;
}

public static class NetMessageFactory
{
  private const string NewLine = "\n";

  public static string Wrap(string type, object payload)
  {
    var envelope = new NetworkMessageEnvelope
    {
      type = type,
      payload = JsonUtility.ToJson(payload)
    };
    return JsonUtility.ToJson(envelope) + NewLine;
  }

  public static bool TryUnwrap(string raw, out NetworkMessageEnvelope envelope)
  {
    envelope = null;
    try
    {
      envelope = JsonUtility.FromJson<NetworkMessageEnvelope>(raw.Trim());
      return envelope != null && !string.IsNullOrEmpty(envelope.type);
    }
    catch
    {
      return false;
    }
  }
}