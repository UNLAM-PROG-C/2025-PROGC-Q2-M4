using System;
using UnityEngine;

public enum MultiplayerRole
{
    None,
    Host,
    Client
}

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    [Header("Network Settings")]
    public int defaultPort = 7777;
    public float broadcastInterval = 0.2f;

    [Header("Runtime")]
    [SerializeField] private MultiplayerRole currentRole = MultiplayerRole.None;
    [SerializeField] private string lastMessage;

    // Optional player identifier (not strictly needed for host-only control)
    public string playerId = Guid.NewGuid().ToString();

    private TcpServer _server;
    private TcpClientPeer _client;
    private float _broadcastTimer;
    private BoardMultiplayerAdapter _boardAdapter;

    public bool IsServer => currentRole == MultiplayerRole.Host;
    public bool IsClient => currentRole == MultiplayerRole.Client;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (IsServer)
        {
            _broadcastTimer += Time.deltaTime;
        }
    }

    public bool ShouldBroadcastThisFrame()
    {
        if (!IsServer) return false;
        if (_broadcastTimer >= broadcastInterval)
        {
            _broadcastTimer = 0f;
            return true;
        }
        return false;
    }

    public void RegisterBoardAdapter(BoardMultiplayerAdapter adapter)
    {
        _boardAdapter = adapter;
    }

    public void HostGame()
    {
        if (currentRole != MultiplayerRole.None) return;

        currentRole = MultiplayerRole.Host;
        _server = new TcpServer(defaultPort);
        _server.OnRawMessage += HandleIncomingMessageServer;
        _server.OnClientConnected += conn =>
        {
            if (_boardAdapter != null)
            {
                var state = _boardAdapter.CaptureBoardState();
                string msg = NetMessageFactory.Wrap("board_state", state);
                _server.SendTo(conn, msg);
            }
        };
        _server.Start();
        Debug.Log("[MultiplayerManager] Hosting game...");
    }

    public void JoinGame(string ip)
    {
        if (currentRole != MultiplayerRole.None) return;

        currentRole = MultiplayerRole.Client;
        _client = new TcpClientPeer(ip, defaultPort);
        _client.OnRawMessage += HandleIncomingMessageClient;
        _client.OnDisconnected += () =>
        {
            ThreadDispatcher.Instance.Enqueue(() =>
            {
                Debug.LogWarning("[MultiplayerManager] Lost connection to server.");
                currentRole = MultiplayerRole.None;
            });
        };
        _client.Connect();
        Debug.Log("[MultiplayerManager] Joined game at " + ip);
    }

    public void BroadcastBoardState(BoardStateMessage state)
    {
        if (!IsServer || _server == null) return;
        string msg = NetMessageFactory.Wrap("board_state", state);
        _server.Broadcast(msg);
    }

    // OPTIONAL: If you want to keep RequestMove calls, implement a simple host-only passthrough.
    public void RequestMove(string action)
    {
        // In host authoritative mode, this is a no-op for clients.
        if (IsClient)
        {
            Debug.LogWarning("Client RequestMove ignored in host-only mode.");
            return;
        }
        // Could trigger piece movement here if you pass more detailed info.
    }

    private void HandleIncomingMessageServer(string raw)
    {
        ThreadDispatcher.Instance.Enqueue(() =>
        {
            lastMessage = raw;
            if (!NetMessageFactory.TryUnwrap(raw, out var env)) return;

            switch (env.type)
            {
                case "handshake":
                    break;
                case "input_request":
                    // Future: process client input
                    break;
                default:
                    Debug.Log("[Server] Unknown message type: " + env.type);
                    break;
            }
        });
    }

    private void HandleIncomingMessageClient(string raw)
    {
        ThreadDispatcher.Instance.Enqueue(() =>
        {
            lastMessage = raw;
            if (!NetMessageFactory.TryUnwrap(raw, out var env)) return;

            switch (env.type)
            {
                case "board_state":
                    var state = JsonUtility.FromJson<BoardStateMessage>(env.payload);
                    if (_boardAdapter == null)
                    {
                        _boardAdapter = FindObjectOfType<BoardMultiplayerAdapter>();
                        if (_boardAdapter != null) RegisterBoardAdapter(_boardAdapter);
                    }
                    _boardAdapter?.ApplyBoardState(state);
                    break;
                case "handshake":
                    break;
                default:
                    Debug.Log("[Client] Unknown message type: " + env.type);
                    break;
            }
        });
    }

    private void OnDestroy()
    {
        if (_server != null) _server.Stop();
        if (_client != null) _client.Disconnect();
    }
}