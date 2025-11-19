using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Automatically sets up multiplayer components in the scene.
/// </summary>
public class AutoSetupMultiplayer : MonoBehaviour
{
  private const string MultiplayerManagerObjectName = "MultiplayerManager";
  private const string OpponentBoardRootName = "OpponentBoardRoot";
  private const string TilemapObjectName = "Tilemap";
  private const string DebugOverlayObjectName = "DebugOverlay";

  private static readonly Vector3 DefaultRemoteBoardOffset = new Vector3(14f, 0f, 0f);

  [Header("Remote Board")]
  public GameObject remoteBoardPrefab;
  public Vector3 remoteBoardOffset = DefaultRemoteBoardOffset;
  public TetrisBlockShapeData[] tetrisBlocksForRemote;

  private void Start()
  {
    if (MultiplayerManager.Instance == null)
    {
      var mm = new GameObject(MultiplayerManagerObjectName);
      mm.AddComponent<MultiplayerManager>();
    }

    var localBoard = FindObjectOfType<Board>();
    if (localBoard == null)
    {
      Debug.LogError("[AutoSetupMultiplayer] No Board encontrado.");
      return;
    }

    var adapter = localBoard.GetComponent<BoardMultiplayerAdapter>();
    if (adapter == null)
    {
      adapter = localBoard.gameObject.AddComponent<BoardMultiplayerAdapter>();
    }
    MultiplayerManager.Instance.RegisterLocalAdapter(adapter);

    var remoteView = FindObjectOfType<RemoteBoardView>();
    if (remoteView == null)
    {
      remoteView = CreateRemote(remoteBoardOffset + localBoard.transform.position);
      Debug.Log("[AutoSetupMultiplayer] RemoteBoardView creado.");
    }

    if (remoteView.TetrisBlocks == null || remoteView.TetrisBlocks.Length == 0)
    {
      remoteView.TetrisBlocks = (tetrisBlocksForRemote != null && tetrisBlocksForRemote.Length > 0)
        ? tetrisBlocksForRemote
        : localBoard.TetrisBlocks;
    }

    MultiplayerManager.Instance.RegisterRemoteView(remoteView);

    if (FindObjectOfType<DebugOverlay>() == null)
    {
      var dbg = new GameObject(DebugOverlayObjectName);
      dbg.AddComponent<DebugOverlay>();
    }
  }

  /// <summary>
  /// Creates a remote board view at the specified position.
  /// </summary>
  private RemoteBoardView CreateRemote(Vector3 position)
  {
    GameObject root;
    if (remoteBoardPrefab != null)
    {
      root = Instantiate(remoteBoardPrefab, position, Quaternion.identity);
    }
    else
    {
      root = new GameObject(OpponentBoardRootName);
      root.transform.position = position;
      root.AddComponent<Grid>();

      var tgo = new GameObject(TilemapObjectName);
      tgo.transform.SetParent(root.transform, false);
      tgo.AddComponent<Tilemap>();
      tgo.AddComponent<TilemapRenderer>();
    }

    var rv = root.GetComponent<RemoteBoardView>();
    if (rv == null)
    {
      rv = root.AddComponent<RemoteBoardView>();
    }
    return rv;
  }
}