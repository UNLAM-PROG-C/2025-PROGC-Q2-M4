using System;
using UnityEngine;

public enum MultiplayerRole
{
  None,
  Host,
  Client
}

public enum GameState
{
  WaitingForPlayers,
  Ready,
  Playing,
  GameOver
}

/// <summary>
/// Central manager for multiplayer game sessions.
/// </summary>
public class MultiplayerManager : MonoBehaviour
{
  private const int DefaultPortNumber = 7777;
  private const float DefaultMinSendIntervalSeconds = 0.05f;

  private const int LinesForSingle = 1; // (unused mapping kept for clarity)
  private const int LinesForDouble = 2;
  private const int LinesForTriple = 3;
  private const int LinesForTetris = 4;

  private const int GarbageFromDouble = 1;
  private const int GarbageFromTriple = 2;
  private const int GarbageFromTetris = 4;

  public static MultiplayerManager Instance { get; private set; }

  [Header("Network Settings")]
  public int defaultPort = DefaultPortNumber;
  public bool autoImmediateSnapshotOnConnect = true;

  [Header("Network Throttling")]
  public float minSendInterval = DefaultMinSendIntervalSeconds;

  [Header("Game State")]
  public GameState currentGameState = GameState.WaitingForPlayers;
  public bool requireClientToStart = true;

  [SerializeField] public MultiplayerRole currentRole = MultiplayerRole.None;
  [SerializeField] private string lastMessage;
  [SerializeField] private int connectedClients = 0;

  private TcpServer _server;
  private TcpClientPeer _client;
  private BoardMultiplayerAdapter _localAdapter;
  private RemoteBoardView _remoteView;
  private float _lastSendTime;
  private bool _gameInitialized;

  public static event Action<BoardStateMessage> OnBoardStateChanged;
  public static event Action<string> OnPlayerAction;
  public static event Action OnGameStateChanged;
  public static event Action<GameState> OnGameStateUpdated;
  public static event Action OnClientConnected;
  public static event Action OnClientDisconnected;

  public bool IsServer => currentRole == MultiplayerRole.Host;
  public bool IsClient => currentRole == MultiplayerRole.Client;
  public bool CanStartGame => currentGameState == GameState.Playing;
  public bool IsGameInitialized => _gameInitialized;

  private void Start()
  {
    Debug.Log($"[MultiplayerManager] Start - Role: {currentRole}, RequireClient: {requireClientToStart}");
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  public void ForceStartGame()
  {
    Debug.Log("[MultiplayerManager] Force starting game (debug)");
    UpdateGameState(GameState.Playing);
    ResumeGame();
  }

  private void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    Instance = this;
    DontDestroyOnLoad(gameObject);
    Application.runInBackground = true;
    Debug.Log("[MultiplayerManager] Awake");

    OnBoardStateChanged += HandleBoardStateChanged;
    OnPlayerAction += HandlePlayerAction;
    OnGameStateChanged += HandleGameStateChanged;
  }

  private void OnDestroy()
  {
    OnBoardStateChanged -= HandleBoardStateChanged;
    OnPlayerAction -= HandlePlayerAction;
    OnGameStateChanged -= HandleGameStateChanged;

    _server?.Stop();
    _client?.Disconnect();
  }

  #region Notification Methods

  public void NotifyPiecePlaced()
  {
    if (currentGameState != GameState.Playing) return;
    TriggerBoardStateUpdate(true);
    if (IsServer)
    {
      BroadcastQueueUpdate();
    }
  }

  public void NotifyPieceMoved()
  {
    if (currentGameState != GameState.Playing) return;
    TriggerBoardStateUpdate(false);
  }

  public void NotifyPieceRotated()
  {
    if (currentGameState != GameState.Playing) return;
    TriggerBoardStateUpdate(false);
  }

  public void NotifyLinesCleared(int count)
  {
    if (currentGameState != GameState.Playing) return;

    TriggerGameStateUpdate();
    TriggerBoardStateUpdate(true);

    int garbageToSend = ComputeGarbageToSend(count);
    if (garbageToSend > 0)
    {
      SendGarbageToOpponent(garbageToSend);
    }
  }

  #endregion

  #region Registration

  public void RegisterLocalAdapter(BoardMultiplayerAdapter adapter)
  {
    _localAdapter = adapter;

    if (_localAdapter != null)
    {
      _localAdapter.OnPiecePlaced += () => TriggerBoardStateUpdate(true);
      _localAdapter.OnLinesCleared += _ => TriggerGameStateUpdate();
      _localAdapter.OnPieceRotated += () => TriggerBoardStateUpdate(false);
      _localAdapter.OnPieceMoved += () => TriggerBoardStateUpdate(false);
      _localAdapter.OnGarbageReceived += _ => TriggerBoardStateUpdate(true);
    }

    Debug.Log("[MultiplayerManager] LocalAdapter registered.");
    InitializeGameState();
  }

  public void RegisterRemoteView(RemoteBoardView view)
  {
    _remoteView = view;
    Debug.Log("[MultiplayerManager] RemoteView registered.");
  }

  #endregion

  #region Host/Client

  public void HostGame()
  {
    if (currentRole != MultiplayerRole.None)
    {
      Debug.LogWarning("[MultiplayerManager] Role already assigned.");
      return;
    }

    currentRole = MultiplayerRole.Host;

    _server = new TcpServer(defaultPort);
    _server.OnRawMessage += HandleIncomingServerSide;
    _server.OnClientConnected += OnServerClientConnected;
    _server.OnClientDisconnected += OnServerClientDisconnected;
    _server.Start();

    InitializeServerQueue();

    Debug.Log("[MultiplayerManager] Host started");

    if (_gameInitialized)
    {
      InitializeGameState();
    }
  }

  public void InitializeClientQueue(int serverSeed)
  {
    if (_localAdapter?.board != null)
    {
      _localAdapter.board.shapesQueue = new SharedShapesQueue(serverSeed);
      Debug.Log($"[Client] Queue synchronized with server seed: {serverSeed}");

      var queueRenderer = FindObjectOfType<QueueRenderer>();
      queueRenderer?.SetShapesQueue(_localAdapter.board.shapesQueue);
    }
  }

  #endregion

  #region Join/Leave

  public void JoinGame(string ip)
  {
    if (currentRole != MultiplayerRole.None)
    {
      Debug.LogWarning("[MultiplayerManager] Role already assigned.");
      return;
    }

    currentRole = MultiplayerRole.Client;

    _client = new TcpClientPeer(ip, defaultPort);
    _client.OnRawMessage += HandleIncomingClientSide;
    _client.OnDisconnected += OnClientDisconnectedFromServer;
    _client.Connect();

    Debug.Log("[MultiplayerManager] Attempting to connect to " + ip);

    if (_gameInitialized)
    {
      UpdateGameState(GameState.Playing);
      ResumeGame();
    }

    if (autoImmediateSnapshotOnConnect) SendImmediateBoardState();
  }

  public void LeaveGame()
  {
    _server?.Stop();
    _client?.Disconnect();
    currentRole = MultiplayerRole.None;
    currentGameState = GameState.WaitingForPlayers;
    connectedClients = 0;
    _gameInitialized = false;
    Debug.Log("[MultiplayerManager] Game left.");
  }

  #endregion

  #region Server Client Events

  private void OnServerClientConnected(ClientConnection client)
  {
    connectedClients++;
    Debug.Log($"[Server] Client connected. Total clients: {connectedClients}");

    ThreadDispatcher.Instance.Enqueue(() =>
    {
      OnClientConnected?.Invoke();

      if (connectedClients >= 1 && currentGameState != GameState.Playing)
      {
        Debug.Log("[Server] Starting game - client connected");
        StartGame();
      }

      SendImmediateBoardState();
      BroadcastQueueUpdate();
    });
  }

  private void OnServerClientDisconnected(ClientConnection client)
  {
    connectedClients--;
    Debug.Log($"[Server] Client disconnected. Total clients: {connectedClients}");

    ThreadDispatcher.Instance.Enqueue(() =>
    {
      OnClientDisconnected?.Invoke();

      if (connectedClients < 1 && requireClientToStart && currentGameState == GameState.Playing)
      {
        Debug.Log("[Server] Pausing game - no clients connected");
        UpdateGameState(GameState.WaitingForPlayers);
        PauseGame();
      }
    });
  }

  #endregion

  #region Client Disconnection

  private void OnClientDisconnectedFromServer()
  {
    ThreadDispatcher.Instance.Enqueue(() =>
    {
      Debug.LogWarning("[MultiplayerManager] Disconnected from server.");
      currentRole = MultiplayerRole.None;
      currentGameState = GameState.WaitingForPlayers;
      OnClientDisconnected?.Invoke();
    });
  }

  #endregion

  #region Message Handling (Server Side)

  private const string MsgHandshake = "handshake";
  private const string MsgBoardState = "board_state";
  private const string MsgPlayerAction = "player_action";
  private const string MsgGameState = "game_state";
  private const string MsgGarbage = "garbage";
  private const string MsgQueueSync = "queue_sync";
  private const string MsgGameStateUpdate = "game_state_update";

  private void HandleIncomingServerSide(string raw)
  {
    if (!NetMessageFactory.TryUnwrap(raw, out var envelope)) return;
    lastMessage = envelope.type;

    ThreadDispatcher.Instance.Enqueue(() =>
    {
      switch (envelope.type)
      {
        case MsgHandshake:
          var handshake = JsonUtility.FromJson<HandshakeMessage>(envelope.payload);
          if (handshake.role == "client")
          {
            Debug.Log("[Server] Client handshake received, sending initial data");
            BroadcastQueueUpdate();
            BroadcastGameState();
          }
          break;

        case MsgBoardState:
          var boardState = JsonUtility.FromJson<BoardStateMessage>(envelope.payload);
          _remoteView?.ApplyBoardState(boardState);
          OnBoardStateChanged?.Invoke(boardState);
          break;

        case MsgPlayerAction:
          var action = JsonUtility.FromJson<PlayerActionMessage>(envelope.payload);
          OnPlayerAction?.Invoke(action.action);
          break;

        case MsgGameState:
          OnGameStateChanged?.Invoke();
          break;

        case MsgGarbage:
          var garbageMsgS = JsonUtility.FromJson<GarbageMessage>(envelope.payload);
          Debug.Log($"[Server] Received garbage request: {garbageMsgS.count}");
          _localAdapter?.ApplyIncomingGarbage(garbageMsgS.count);
          var forward = NetMessageFactory.Wrap(MsgGarbage, garbageMsgS);
          _server?.Broadcast(forward);
          break;
      }
    });
  }

  #endregion

  #region Message Handling (Client Side)

  private void HandleIncomingClientSide(string raw)
  {
    if (!NetMessageFactory.TryUnwrap(raw, out var envelope)) return;
    lastMessage = envelope.type;

    ThreadDispatcher.Instance.Enqueue(() =>
    {
      switch (envelope.type)
      {
        case MsgQueueSync:
          var queueSync = JsonUtility.FromJson<QueueSyncMessage>(envelope.payload);
          Debug.Log($"[Client] Received queue sync with seed: {queueSync.seed}");

          var queueState = new QueueStateMessage
          {
            upcomingShapes = queueSync.upcomingShapes,
            seed = queueSync.seed
          };
          _localAdapter?.board?.SynchronizeQueue(queueState);

          var queueRenderer = FindObjectOfType<QueueRenderer>();
          queueRenderer?.RefreshQueue();
          break;

        case MsgGameStateUpdate:
          var gameStateUpdate = JsonUtility.FromJson<GameStateUpdateMessage>(envelope.payload);
            Debug.Log($"[Client] Received game state update: {gameStateUpdate.gameState}");

          if (Enum.TryParse(gameStateUpdate.gameState, out GameState newState))
          {
            currentGameState = newState;
            OnGameStateUpdated?.Invoke(newState);

            if (newState == GameState.Playing)
            {
              ResumeGame();
            }
            else if (newState == GameState.WaitingForPlayers)
            {
              PauseGame();
            }
          }
          break;

        case MsgBoardState:
          var boardState = JsonUtility.FromJson<BoardStateMessage>(envelope.payload);
          _remoteView?.ApplyBoardState(boardState);
          OnBoardStateChanged?.Invoke(boardState);
          break;

        case MsgPlayerAction:
          var action = JsonUtility.FromJson<PlayerActionMessage>(envelope.payload);
          OnPlayerAction?.Invoke(action.action);
          break;

        case MsgGameState:
          OnGameStateChanged?.Invoke();
          break;

        case MsgGarbage:
          var garbageMsgC = JsonUtility.FromJson<GarbageMessage>(envelope.payload);
          Debug.Log($"[Client] Received garbage lines: {garbageMsgC.count}");
          _localAdapter?.ApplyIncomingGarbage(garbageMsgC.count);
          break;
      }
    });
  }

  #endregion

  #region Event Handlers

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

  #endregion

  #region Initialization

  private void InitializeServerQueue()
  {
    if (_localAdapter?.board != null)
    {
      _localAdapter.board.shapesQueue = new SharedShapesQueue();
      Debug.Log($"[Server] Queue initialized with seed: {_localAdapter.board.shapesQueue.Seed}");
    }
  }

  private void InitializeGameState()
  {
    _gameInitialized = true;

    if (IsServer && requireClientToStart && connectedClients == 0)
    {
      Debug.Log("[MultiplayerManager] Server waiting for clients...");
      UpdateGameState(GameState.WaitingForPlayers);
      PauseGame();
    }
    else if (IsClient || !requireClientToStart)
    {
      Debug.Log("[MultiplayerManager] Starting game immediately");
      UpdateGameState(GameState.Playing);
      ResumeGame();
    }
    else
    {
      Debug.Log("[MultiplayerManager] Game ready to start");
      UpdateGameState(GameState.Ready);
      ResumeGame();
    }
  }

  #endregion

  #region Garbage

  private void SendGarbageToOpponent(int count)
  {
    var msg = NetMessageFactory.Wrap(MsgGarbage, new GarbageMessage { count = count });

    if (IsServer)
    {
      _server?.Broadcast(msg);
      Debug.Log($"[Server] Sent garbage {count} to clients");
    }
    else if (IsClient)
    {
      _client?.Send(msg);
      Debug.Log($"[Client] Sent garbage {count} request to server");
    }
  }

  private int ComputeGarbageToSend(int linesCleared)
  {
    return linesCleared switch
    {
      LinesForDouble => GarbageFromDouble,
      LinesForTriple => GarbageFromTriple,
      LinesForTetris => GarbageFromTetris,
      _ => 0
    };
  }

  #endregion

  #region Start/Pause/Resume

  private void StartGame()
  {
    Debug.Log("[MultiplayerManager] Starting game...");
    UpdateGameState(GameState.Playing);
    ResumeGame();
  }

  private void PauseGame()
  {
    Debug.Log("[MultiplayerManager] Game paused - waiting for players");

    var pieces = FindObjectsOfType<Piece>();
    foreach (var piece in pieces)
    {
      if (piece != null)
      {
        piece.enabled = false;
        Debug.Log($"[MultiplayerManager] Disabled piece: {piece.name}");
      }
    }
  }

  private void ResumeGame()
  {
    Debug.Log("[MultiplayerManager] Game resumed");

    var pieces = FindObjectsOfType<Piece>();
    foreach (var piece in pieces)
    {
      if (piece != null)
      {
        piece.enabled = true;
        Debug.Log($"[MultiplayerManager] Enabled piece: {piece.name}");
      }
    }
  }

  #endregion

  #region Game State

  public void TriggerGameStateUpdate()
  {
    var msg = NetMessageFactory.Wrap(MsgGameState, new GameStateMessage
    {
      state = "updated",
      timestamp = Time.time
    });

    if (IsServer)
    {
      _server?.Broadcast(msg);
    }
    else if (IsClient)
    {
      _client?.Send(msg);
    }
  }

  private void UpdateGameState(GameState newState)
  {
    if (currentGameState == newState) return;

    var oldState = currentGameState;
    currentGameState = newState;
    OnGameStateUpdated?.Invoke(newState);
    Debug.Log($"[MultiplayerManager] Game state changed from {oldState} to {newState}");

    if (IsServer)
    {
      BroadcastGameState();
    }
  }

  private void BroadcastGameState()
  {
    if (!IsServer) return;

    var msg = NetMessageFactory.Wrap(MsgGameStateUpdate, new GameStateUpdateMessage
    {
      gameState = currentGameState.ToString(),
      connectedClients = connectedClients,
      timestamp = Time.time
    });

    _server?.Broadcast(msg);
    Debug.Log($"[Server] Broadcasting game state: {currentGameState}");
  }

  #endregion

  #region Board State

  public void TriggerBoardStateUpdate(bool forceImmediate = false)
  {
    if (currentGameState != GameState.Playing) return;
    if (!forceImmediate && Time.time - _lastSendTime < minSendInterval) return;

    SendImmediateBoardState();
    _lastSendTime = Time.time;
  }

  public void SendImmediateBoardState()
  {
    if (_localAdapter == null) return;

    var state = _localAdapter.CaptureBoardState();
    state.owner = IsServer ? "server" : "client";

    var msg = NetMessageFactory.Wrap(MsgBoardState, state);

    if (IsServer)
    {
      _server?.Broadcast(msg);
    }
    else if (IsClient)
    {
      _client?.Send(msg);
    }
  }

  #endregion

  #region Queue

  public void BroadcastQueueUpdate()
  {
    if (!IsServer || _localAdapter?.board?.shapesQueue == null) return;

    var queueState = _localAdapter.board.shapesQueue.GetQueueState();
    var msg = NetMessageFactory.Wrap(MsgQueueSync, new QueueSyncMessage
    {
      upcomingShapes = queueState.upcomingShapes,
      seed = queueState.seed
    });

    _server?.Broadcast(msg);
    Debug.Log($"[Server] Broadcasting queue update with {queueState.upcomingShapes.Length} shapes");
  }

  #endregion
}