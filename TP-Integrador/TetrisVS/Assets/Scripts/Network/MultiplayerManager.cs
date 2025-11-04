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
        };
        _server.Start();
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

    // Event-driven methods with throttling
    private void TriggerBoardStateUpdate(bool forceImmediate = false)
    {
        if (currentRole == MultiplayerRole.None || _localAdapter == null) return;
        
        // Throttle updates unless forced
        if (!forceImmediate && Time.time - _lastSendTime < minSendInterval) return;

        var state = _localAdapter.CaptureBoardState();
        OnBoardStateChanged?.Invoke(state);
        _lastSendTime = Time.time;
    }

    private void TriggerGameStateUpdate()
    {
        OnGameStateChanged?.Invoke();
    }

    private void TriggerPlayerAction(string action)
    {
        OnPlayerAction?.Invoke(action);
    }

    // Event handlers
    private void HandleBoardStateChanged(BoardStateMessage state)
    {
        SendBoardState(state);
    }

    private void HandlePlayerAction(string action)
    {
        // Handle specific player actions that need immediate network sync
        string msg = NetMessageFactory.Wrap("player_action", new PlayerActionMessage { action = action, timestamp = Time.time });
        
        if (IsServer) _server?.Broadcast(msg);
        else if (IsClient) _client?.Send(msg);
    }

    private void HandleGameStateChanged()
    {
        // Send immediate board state when game state changes (like lines cleared)
        TriggerBoardStateUpdate(true);
    }

    public void SendImmediateBoardState()
    {
        if (_localAdapter == null)
        {
            _localAdapter = FindObjectOfType<BoardMultiplayerAdapter>();
            if (_localAdapter != null)
                Debug.Log("[MultiplayerManager] LocalAdapter encontrado tardíamente.");
        }
        if (_localAdapter == null) return;

        var state = _localAdapter.CaptureBoardState();
        state.owner = IsServer ? "host" : "client";
        SendBoardState(state);
    }

    private void SendBoardState(BoardStateMessage state)
    {
        state.owner = IsServer ? "host" : "client";
        string msg = NetMessageFactory.Wrap("board_state", state);

        if (IsServer) _server?.Broadcast(msg);
        else if (IsClient) _client?.Send(msg);
    }

    private void HandleIncomingServerSide(string raw)
    {
        ThreadDispatcher.Instance.Enqueue(() =>
        {
            lastMessage = raw;
            if (!NetMessageFactory.TryUnwrap(raw, out var env)) return;
            
            switch (env.type)
            {
                case "board_state":
                    var state = JsonUtility.FromJson<BoardStateMessage>(env.payload);
                    if (state.owner == "client")
                    {
                        EnsureRemoteView();
                        _remoteView?.ApplyBoardState(state);
                        DebugOverlay.LastRemoteUpdateTime = Time.time;
                    }
                    break;
                    
                case "player_action":
                    var actionData = JsonUtility.FromJson<PlayerActionMessage>(env.payload);
                    ProcessRemotePlayerAction(actionData.action);
                    break;
                    
                case "handshake":
                    var handshake = JsonUtility.FromJson<HandshakeMessage>(env.payload);
                    Debug.Log($"[Server] Handshake received from {handshake.role}");
                    break;
            }
        });
    }

    private void HandleIncomingClientSide(string raw)
    {
        ThreadDispatcher.Instance.Enqueue(() =>
        {
            lastMessage = raw;
            if (!NetMessageFactory.TryUnwrap(raw, out var env)) return;
            
            switch (env.type)
            {
                case "board_state":
                    var state = JsonUtility.FromJson<BoardStateMessage>(env.payload);
                    if (state.owner == "host")
                    {
                        EnsureRemoteView();
                        _remoteView?.ApplyBoardState(state);
                        DebugOverlay.LastRemoteUpdateTime = Time.time;
                    }
                    break;
                    
                case "player_action":
                    var actionData = JsonUtility.FromJson<PlayerActionMessage>(env.payload);
                    ProcessRemotePlayerAction(actionData.action);
                    break;
            }
        });
    }

    private void ProcessRemotePlayerAction(string action)
    {
        // Process incoming player actions from remote player
        Debug.Log($"[MultiplayerManager] Remote player action: {action}");
        // Add your specific action handling here
    }

    private void EnsureRemoteView()
    {
        if (_remoteView == null)
        {
            _remoteView = FindObjectOfType<RemoteBoardView>();
            if (_remoteView != null)
                Debug.Log("[MultiplayerManager] RemoteView encontrado tardíamente.");
        }
    }

    public void LeaveGame()
    {
        if (IsServer)
        {
            _server?.Stop();
            _server = null;
        }
        else if (IsClient)
        {
            _client?.Disconnect();
            _client = null;
        }
        currentRole = MultiplayerRole.None;
        Debug.Log("[MultiplayerManager] Juego abandonado.");
    }

    // Public methods for triggering network events from game logic
    public void NotifyPiecePlaced() => TriggerBoardStateUpdate(true); // Force immediate
    public void NotifyPieceMoved() => TriggerBoardStateUpdate(false); // Throttled
    public void NotifyPieceRotated() => TriggerBoardStateUpdate(false); // Throttled
    public void NotifyLinesCleared() => TriggerGameStateUpdate();
    public void NotifyPlayerAction(string action) => TriggerPlayerAction(action);
}