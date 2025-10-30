using System;
using UnityEngine;

public enum MultiplayerRole { None, Host, Client }

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    public int defaultPort = 7777;
    public float sendInterval = 0.25f;
    public bool autoImmediateSnapshotOnConnect = true;

    [SerializeField] private MultiplayerRole currentRole = MultiplayerRole.None;
    [SerializeField] private string lastMessage;

    private float _sendTimer;
    private TcpServer _server;
    private TcpClientPeer _client;
    private BoardMultiplayerAdapter _localAdapter;
    private RemoteBoardView _remoteView;

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
    }

    private void Update()
    {
        if (currentRole == MultiplayerRole.None) return;
        _sendTimer += Time.deltaTime;
        if (_sendTimer >= sendInterval)
        {
            _sendTimer = 0f;
            SendLocalBoardState();
        }
    }

    public void RegisterLocalAdapter(BoardMultiplayerAdapter adapter)
    {
        _localAdapter = adapter;
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
            ForceImmediateSend();
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
        if (autoImmediateSnapshotOnConnect) ForceImmediateSend();
    }

    public void ForceImmediateSend()
    {
        _sendTimer = sendInterval;
        SendLocalBoardState();
    }

    private void SendLocalBoardState()
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
            if (env.type == "board_state")
            {
                var state = JsonUtility.FromJson<BoardStateMessage>(env.payload);
                if (state.owner == "client")
                {
                    EnsureRemoteView();
                    _remoteView?.ApplyBoardState(state);
                    DebugOverlay.LastRemoteUpdateTime = Time.time;
                }
            }
        });
    }

    private void HandleIncomingClientSide(string raw)
    {
        ThreadDispatcher.Instance.Enqueue(() =>
        {
            lastMessage = raw;
            if (!NetMessageFactory.TryUnwrap(raw, out var env)) return;
            if (env.type == "board_state")
            {
                var state = JsonUtility.FromJson<BoardStateMessage>(env.payload);
                if (state.owner == "host")
                {
                    EnsureRemoteView();
                    _remoteView?.ApplyBoardState(state);
                    DebugOverlay.LastRemoteUpdateTime = Time.time;
                }
            }
        });
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

    private void OnDestroy()
    {
        if (_server != null) _server.Stop();
        if (_client != null) _client.Disconnect();
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

}