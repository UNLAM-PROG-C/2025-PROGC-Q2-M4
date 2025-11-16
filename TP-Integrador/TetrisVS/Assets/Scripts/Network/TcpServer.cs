using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
// TCP server for multiplayer communication
public class TcpServer
{
    public int Port { get; }
    private TcpListener _listener;
    private Thread _acceptThread;
    private volatile bool _running;
    private readonly List<ClientConnection> _clients = new();

    public event Action<string> OnRawMessage;
    public event Action<ClientConnection> OnClientConnected;
    public event Action<ClientConnection> OnClientDisconnected;

    public TcpServer(int port) => Port = port; // Constructor to set listening port
// Start the server and begin accepting client connections
    public void Start()
    {
        if (_running) return;
        _running = true; // Initialize and start the TCP listener
        _listener = new TcpListener(IPAddress.Any, Port); // Listen on all interfaces
        _listener.Start();
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true }; // Start the accept loop in a background thread
        _acceptThread.Start();
        Debug.Log($"[Server] Listening on port {Port}");
    }
// Loop to accept incoming client connections
    private void AcceptLoop()
    {
        try
        {
            while (_running)
            {// Check for pending connections
                if (!_listener.Pending())
                {// No pending connections, wait briefly
                    Thread.Sleep(40);
                    continue;
                }
                // Accept a new client connection
                var client = _listener.AcceptTcpClient();
                Debug.Log("[Server] Accepted connection from " + client.Client.RemoteEndPoint);
                var conn = new ClientConnection(client); // Wrap the TcpClient
                lock (_clients) _clients.Add(conn); // Add to the client list
                OnClientConnected?.Invoke(conn); // Notify about new connection
                conn.StartReceiving( 
                    line => OnRawMessage?.Invoke(line),
                    () =>
                    {// Handle client disconnection
                        lock (_clients) _clients.Remove(conn);
                        OnClientDisconnected?.Invoke(conn);
                    });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[Server] AcceptLoop exception: " + ex);
        }
    }
// Broadcast a raw message to all connected clients
    public void Broadcast(string msg)
    {
        lock (_clients)
        {
            foreach (var c in _clients) c.Send(msg);
        }
    }
// Stop the server and disconnect all clients
    public void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        lock (_clients)
        {// Close all client connections
            foreach (var c in _clients) c.Close();
            _clients.Clear();
        }
        Debug.Log("[Server] Stopped");
    }
}
// Represents a connected client on the server side
public class ClientConnection
{
    private readonly TcpClient _client;
    private Thread _recvThread;
    private volatile bool _open;
    private StreamReader _reader;
    private StreamWriter _writer;
// Constructor to initialize the client connection
    public ClientConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;
        var ns = _client.GetStream();
        _reader = new StreamReader(ns);
        _writer = new StreamWriter(ns) { AutoFlush = true };
        _open = true;
    }
// Start receiving messages from the client
    public void StartReceiving(Action<string> onLine, Action onClosed)
    {
        _recvThread = new Thread(() =>
        {
            try
            {
                while (_open)
                {// Read each line as a message
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
            {// Handle closure
                _open = false;
                onClosed?.Invoke();
            }
        })
        { IsBackground = true }; 
        _recvThread.Start(); // Start the receive thread
    }
// Send a raw message to the client
    public void Send(string data)
    {
        try { _writer.Write(data); }
        catch (Exception ex)
        {
            Debug.LogWarning("[Server ClientConnection] send exception: " + ex.Message);
        }
    }
// Close the client connection
    public void Close()
    {
        _open = false;
        try { _client.Close(); } catch { }
    }
}