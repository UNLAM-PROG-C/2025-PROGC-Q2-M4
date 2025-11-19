using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP server for multiplayer communication.
/// </summary>
public class TcpServer
{
  private const int AcceptLoopSleepMilliseconds = 40;

  public int Port { get; }
  private TcpListener _listener;
  private Thread _acceptThread;
  private volatile bool _running;
  private readonly List<ClientConnection> _clients = new();

  public event Action<string> OnRawMessage;
  public event Action<ClientConnection> OnClientConnected;
  public event Action<ClientConnection> OnClientDisconnected;

  public TcpServer(int port)
  {
    Port = port;
  }

  public void Start()
  {
    if (_running) return;
    _running = true;
    _listener = new TcpListener(IPAddress.Any, Port);
    _listener.Start();
    _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
    _acceptThread.Start();
    Debug.Log($"[Server] Listening on port {Port}");
  }

  private void AcceptLoop()
  {
    try
    {
      while (_running)
      {
        if (!_listener.Pending())
        {
          Thread.Sleep(AcceptLoopSleepMilliseconds);
          continue;
        }

        var client = _listener.AcceptTcpClient();
        Debug.Log("[Server] Accepted connection from " + client.Client.RemoteEndPoint);
        var conn = new ClientConnection(client);
        lock (_clients)
        {
          _clients.Add(conn);
        }
        OnClientConnected?.Invoke(conn);
        conn.StartReceiving(
          line => OnRawMessage?.Invoke(line),
          () =>
          {
            lock (_clients)
            {
              _clients.Remove(conn);
            }
            OnClientDisconnected?.Invoke(conn);
          });
      }
    }
    catch (Exception ex)
    {
      Debug.LogError("[Server] AcceptLoop exception: " + ex);
    }
  }

  public void Broadcast(string msg)
  {
    lock (_clients)
    {
      foreach (var c in _clients)
      {
        c.Send(msg);
      }
    }
  }

  public void Stop()
  {
    _running = false;
    try
    {
      _listener?.Stop();
    }
    catch
    {
      // ignored
    }

    lock (_clients)
    {
      foreach (var c in _clients)
      {
        c.Close();
      }
      _clients.Clear();
    }

    Debug.Log("[Server] Stopped");
  }
}

/// <summary>
/// Represents a connected client on the server side.
/// </summary>
public class ClientConnection
{
  private readonly TcpClient _client;
  private Thread _recvThread;
  private volatile bool _open;
  private StreamReader _reader;
  private StreamWriter _writer;

  public ClientConnection(TcpClient client)
  {
    _client = client;
    _client.NoDelay = true;
    var ns = _client.GetStream();
    _reader = new StreamReader(ns);
    _writer = new StreamWriter(ns) { AutoFlush = true };
    _open = true;
  }

  public void StartReceiving(Action<string> onLine, Action onClosed)
  {
    _recvThread = new Thread(() =>
    {
      try
      {
        while (_open)
        {
          var line = _reader.ReadLine();
          if (line == null) break;
          onLine?.Invoke(line);
        }
      }
      catch (Exception ex)
      {
        Debug.LogWarning("[Server ClientConnection] recv exception: " + ex.Message);
      }
      finally
      {
        _open = false;
        onClosed?.Invoke();
      }
    })
    {
      IsBackground = true
    };
    _recvThread.Start();
  }

  public void Send(string data)
  {
    try
    {
      _writer.Write(data);
    }
    catch (Exception ex)
    {
      Debug.LogWarning("[Server ClientConnection] send exception: " + ex.Message);
    }
  }

  public void Close()
  {
    _open = false;
    try
    {
      _client.Close();
    }
    catch
    {
      // ignored
    }
  }
}