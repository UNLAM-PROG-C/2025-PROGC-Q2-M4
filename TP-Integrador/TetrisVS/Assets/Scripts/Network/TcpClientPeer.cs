using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP client peer for multiplayer communication.
/// </summary>
public class TcpClientPeer
{
  private const string ClientHandshakeRole = "client";

  public string Host { get; }
  public int Port { get; }

  private TcpClient _client;
  private Thread _recvThread;
  private StreamReader _reader;
  private StreamWriter _writer;
  private volatile bool _connected;

  public event Action<string> OnRawMessage;
  public event Action OnDisconnected;

  public TcpClientPeer(string host, int port)
  {
    Host = host;
    Port = port;
  }

  public void Connect()
  {
    if (_connected) return;

    _client = new TcpClient { NoDelay = true };
    try
    {
      _client.Connect(Host, Port);
    }
    catch (Exception ex)
    {
      Debug.LogError($"[Client] Connect failed {Host}:{Port} - {ex.Message}");
      return;
    }

    var ns = _client.GetStream();
    _reader = new StreamReader(ns);
    _writer = new StreamWriter(ns) { AutoFlush = true };
    _connected = true;

    _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
    _recvThread.Start();

    Send(NetMessageFactory.Wrap("handshake", new HandshakeMessage { role = ClientHandshakeRole }));
    Debug.Log("[Client] Connected to " + Host + ":" + Port);
  }

  private void ReceiveLoop()
  {
    try
    {
      while (_connected)
      {
        var line = _reader.ReadLine();
        if (line == null) break;
        OnRawMessage?.Invoke(line);
      }
    }
    catch (Exception ex)
    {
      Debug.LogWarning("[Client] Receive exception: " + ex.Message);
    }
    finally
    {
      _connected = false;
      OnDisconnected?.Invoke();
    }
  }

  public void Send(string data)
  {
    try
    {
      _writer.Write(data);
    }
    catch (Exception ex)
    {
      Debug.LogWarning("[Client] Send exception: " + ex.Message);
    }
  }

  public void Disconnect()
  {
    _connected = false;
    try
    {
      _client.Close();
    }
    catch
    {
      // ignored
    }
    Debug.Log("[Client] Disconnected");
  }
}