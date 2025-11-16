using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
// TCP client peer for multiplayer communication
public class TcpClientPeer
{
    public string Host { get; }
    public int Port { get; }
    private TcpClient _client;
    private Thread _recvThread;
    private StreamReader _reader;
    private StreamWriter _writer;
    private volatile bool _connected;

    public event Action<string> OnRawMessage;
    public event Action OnDisconnected;
// Constructor to set host and port
    public TcpClientPeer(string host, int port)
    {
        Host = host;
        Port = port;
    }
// Connect to the server and start the receive loop
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
// Set up streams and start receiving messages
        var ns = _client.GetStream();
        _reader = new StreamReader(ns);
        _writer = new StreamWriter(ns) { AutoFlush = true };
        _connected = true;
// Start the receive thread
        _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
        _recvThread.Start();
// Send initial handshake
        Send(NetMessageFactory.Wrap("handshake", new HandshakeMessage { role = "client" }));
        Debug.Log("[Client] Connected to " + Host + ":" + Port);
    }
// Loop to receive messages from the server
    private void ReceiveLoop()
    {
        try
        {
            while (_connected)
            {// Read each line as a message
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
        {// Handle disconnection
            _connected = false;
            OnDisconnected?.Invoke();
        }
    }
// Send a raw message to the server
    public void Send(string data)
    {
        try { _writer.Write(data); } 
        catch (Exception ex)
        {
            Debug.LogWarning("[Client] Send exception: " + ex.Message);
        }
    }
// Disconnect from the server
    public void Disconnect()
    {// Stop receiving and close the connection
        _connected = false;
        try { _client.Close(); } catch { }
        Debug.Log("[Client] Disconnected");
    }
}