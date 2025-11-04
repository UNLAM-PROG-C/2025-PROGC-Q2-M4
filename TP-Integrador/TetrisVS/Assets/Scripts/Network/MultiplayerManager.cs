using System;
using UnityEngine;

public enum MultiplayerRole { None, Host, Client }

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    public int defaultPort = 7777;
    public bool autoImmediateSnapshotOnConnect = true;
    
    [Header("Network Throttling")]
    public float minSendInterval = 0.05f; // Minimum time between sends (20fps max)

    [SerializeField] private MultiplayerRole currentRole = MultiplayerRole.None;
    [SerializeField] private string lastMessage;

    private TcpServer _server;
    private TcpClientPeer _client;
    private BoardMultiplayerAdapter _localAdapter;
    private RemoteBoardView _remoteView;
    private float _lastSendTime;

    // Event-based networking events
    public static event System.Action<BoardStateMessage> OnBoardStateChanged;
    public static event System.Action<string> OnPlayerAction;
    public static event System.Action OnGameStateChanged;

    public bool IsServer => currentRole == MultiplayerRole.Host;
    public bool IsClient => currentRole == MultiplayerRole.Client;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        Debug.Log("[MultiplayerManager] Awake");

        // Subscribe to network events
        OnBoardStateChanged += HandleBoardStateChanged;
        OnPlayerAction += HandlePlayerAction;
        OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        OnBoardStateChanged -= HandleBoardStateChanged;
        OnPlayerAction -= HandlePlayerAction;
        OnGameStateChanged -= HandleGameStateChanged;

        if (_server != null) _server.Stop();
        if (_client != null) _client.Disconnect();
    }

    public void RegisterLocalAdapter(BoardMultiplayerAdapter adapter)
    {
        _localAdapter = adapter;
        
        // Subscribe to adapter events for immediate network updates
        if (_localAdapter != null)
        {
            _localAdapter.OnPiecePlaced += () => TriggerBoardStateUpdate(true); // Force immediate for piece placement
            _localAdapter.OnLinesCleared += (lines) => TriggerGameStateUpdate();
            _localAdapter.OnPieceRotated += () => TriggerBoardStateUpdate(false); // Throttled for rotation
            _localAdapter.OnPieceMoved += () => TriggerBoardStateUpdate(false); // Throttled for movement
        }
        
        Debug.Log("[MultiplayerManager] LocalAdapter registrado.");
    }

    public void RegisterBoardAdapter(BoardMultiplayerAdapter adapter) => RegisterLocalAdapter(adapter);

    public void RegisterRemoteView(RemoteBoardView view)
    {
        _remoteView = view;
        Debug.Log("[MultiplayerManager] RemoteView registrada.");
    }

    public void HostGame()
    {
        if (currentRole != MultiplayerRole.None)
        {
            Debug.LogWarning("[MultiplayerManager] Ya hay rol."); return;
        }
        currentRole = MultiplayerRole.Host;
        _server = new TcpServer(defaultPort);
        _server.OnRawMessage += HandleIncomingServerSide;
        _server.OnClientConnected += _ =>
        {
            Debug.Log("[Server] Cliente conectado -> snapshot inmediato host.");
            SendImmediateBoardState();
            BroadcastQueueUpdate(); // Send queue state to new client
        };
        _server.Start();
        
        // Initialize server queue
        InitializeServerQueue();
        
        Debug.Log("[MultiplayerManager] Host iniciado.");
    }

    public void JoinGame(string ip)
    {
        if (currentRole != MultiplayerRole.None)
        {
            Debug.LogWarning("[MultiplayerManager] Ya hay rol."); return;
        }
        currentRole = MultiplayerRole.Client;
        _client = new TcpClientPeer(ip, defaultPort);
        _client.OnRawMessage += HandleIncomingClientSide;
        _client.OnDisconnected += () =>
        {
            ThreadDispatcher.Instance.Enqueue(() =>
            {
                Debug.LogWarning("[MultiplayerManager] Desconectado.");
                currentRole = MultiplayerRole.None;
            });
        };
        _client.Connect();
        Debug.Log("[MultiplayerManager] Intentando conectar a " + ip);
        if (autoImmediateSnapshotOnConnect) SendImmediateBoardState();
    }

    public void LeaveGame()
    {
        if (_server != null) _server.Stop();
        if (_client != null) _client.Disconnect();
        currentRole = MultiplayerRole.None;
        Debug.Log("[MultiplayerManager] Juego abandonado.");
    }

    // Queue synchronization methods
    private void InitializeServerQueue()
    {
        if (_localAdapter?.board != null)
        {
            // Server initializes with a new random seed
            _localAdapter.board.shapesQueue = new SharedShapesQueue();
            Debug.Log($"[Server] Queue initialized with seed: {_localAdapter.board.shapesQueue.Seed}");
        }
    }

    public void BroadcastQueueUpdate()
    {
        if (!IsServer || _localAdapter?.board?.shapesQueue == null) return;
        
        var queueState = _localAdapter.board.shapesQueue.GetQueueState();
        var msg = NetMessageFactory.Wrap("queue_sync", new QueueSyncMessage
        {
            upcomingShapes = queueState.upcomingShapes,
            seed = queueState.seed
        });
        
        _server?.Broadcast(msg);
        Debug.Log($"[Server] Broadcasting queue update with {queueState.upcomingShapes.Length} shapes");
    }

    public void InitializeClientQueue(int serverSeed)
    {
        if (_localAdapter?.board != null)
        {
            _localAdapter.board.shapesQueue = new SharedShapesQueue(serverSeed);
            Debug.Log($"[Client] Queue synchronized with server seed: {serverSeed}");
            
            // Also update queue renderer if it exists
            var queueRenderer = FindObjectOfType<QueueRenderer>();
            if (queueRenderer != null)
            {
                queueRenderer.SetShapesQueue(_localAdapter.board.shapesQueue);
            }
        }
    }

    private void HandleIncomingServerSide(string raw)
    {
        if (!NetMessageFactory.TryUnwrap(raw, out var envelope)) return;
        lastMessage = envelope.type;

        ThreadDispatcher.Instance.Enqueue(() =>
        {
            switch (envelope.type)
            {
                case "handshake":
                    var handshake = JsonUtility.FromJson<HandshakeMessage>(envelope.payload);
                    if (handshake.role == "client")
                    {
                        Debug.Log("[Server] Client handshake received, sending queue state");
                        // Send initial queue state to new client
                        BroadcastQueueUpdate();
                    }
                    break;

                case "board_state":
                    var boardState = JsonUtility.FromJson<BoardStateMessage>(envelope.payload);
                    _remoteView?.ApplyBoardState(boardState);
                    OnBoardStateChanged?.Invoke(boardState);
                    break;

                case "player_action":
                    var action = JsonUtility.FromJson<PlayerActionMessage>(envelope.payload);
                    OnPlayerAction?.Invoke(action.action);
                    break;

                case "game_state":
                    OnGameStateChanged?.Invoke();
                    break;
            }
        });
    }

    private void HandleIncomingClientSide(string raw)
    {
        if (!NetMessageFactory.TryUnwrap(raw, out var envelope)) return;
        lastMessage = envelope.type;

        ThreadDispatcher.Instance.Enqueue(() =>
        {
            switch (envelope.type)
            {
                case "queue_sync":
                    var queueSync = JsonUtility.FromJson<QueueSyncMessage>(envelope.payload);
                    Debug.Log($"[Client] Received queue sync with seed: {queueSync.seed}");
                    
                    var queueState = new QueueStateMessage
                    {
                        upcomingShapes = queueSync.upcomingShapes,
                        seed = queueSync.seed
                    };
                    _localAdapter?.board?.SynchronizeQueue(queueState);
                    
                    // Update queue renderer
                    var queueRenderer = FindObjectOfType<QueueRenderer>();
                    if (queueRenderer != null)
                    {
                        queueRenderer.RefreshQueue();
                    }
                    break;

                case "board_state":
                    var boardState = JsonUtility.FromJson<BoardStateMessage>(envelope.payload);
                    _remoteView?.ApplyBoardState(boardState);
                    OnBoardStateChanged?.Invoke(boardState);
                    break;

                case "player_action":
                    var action = JsonUtility.FromJson<PlayerActionMessage>(envelope.payload);
                    OnPlayerAction?.Invoke(action.action);
                    break;

                case "game_state":
                    OnGameStateChanged?.Invoke();
                    break;
            }
        });
    }

    // Event handlers
    private void HandleBoardStateChanged(BoardStateMessage state)
    {
        Debug.Log($"[MultiplayerManager] Board state changed for {state.owner}");
    }

    private void HandlePlayerAction(string action)
    {
        Debug.Log($"[MultiplayerManager] Player action: {action}");
    }

    private void HandleGameStateChanged()
    {
        Debug.Log("[MultiplayerManager] Game state changed");
    }

    // Network update methods
    public void TriggerBoardStateUpdate(bool forceImmediate = false)
    {
        if (!forceImmediate && Time.time - _lastSendTime < minSendInterval)
            return;

        SendImmediateBoardState();
        _lastSendTime = Time.time;
    }

    public void TriggerGameStateUpdate()
    {
        var msg = NetMessageFactory.Wrap("game_state", new GameStateMessage
        {
            state = "updated",
            timestamp = Time.time
        });

        if (IsServer) _server?.Broadcast(msg);
        else if (IsClient) _client?.Send(msg);
    }

    public void SendImmediateBoardState()
    {
        if (_localAdapter == null) return;

        var state = _localAdapter.CaptureBoardState();
        state.owner = IsServer ? "server" : "client";
        
        var msg = NetMessageFactory.Wrap("board_state", state);

        if (IsServer) _server?.Broadcast(msg);
        else if (IsClient) _client?.Send(msg);
    }

    // Notification methods for game events
    public void NotifyPiecePlaced()
    {
        TriggerBoardStateUpdate(true);
        if (IsServer)
        {
            BroadcastQueueUpdate(); // Update queue when piece is placed
        }
    }

    public void NotifyPieceMoved()
    {
        TriggerBoardStateUpdate(false);
    }

    public void NotifyPieceRotated()
    {
        TriggerBoardStateUpdate(false);
    }

    public void NotifyLinesCleared(int count)
    {
        TriggerGameStateUpdate();
        TriggerBoardStateUpdate(true);
    }
}