using UnityEngine;
using UnityEngine.Tilemaps;

public class AutoSetupMultiplayer : MonoBehaviour
{
    public GameObject remoteBoardPrefab;
    public Vector3 remoteBoardOffset = new Vector3(14f, 0f, 0f);
    public TetrisBlockShapeData[] tetrisBlocksForRemote;

    private void Start()
    {
        if (MultiplayerManager.Instance == null)
        {
            var mm = new GameObject("MultiplayerManager");
            mm.AddComponent<MultiplayerManager>();
        }

        Board localBoard = FindObjectOfType<Board>();
        if (localBoard == null)
        {
            Debug.LogError("[AutoSetupMultiplayer] No Board encontrado.");
            return;
        }

        // Adapter
        var adapter = localBoard.GetComponent<BoardMultiplayerAdapter>();
        if (adapter == null)
        {
            adapter = localBoard.gameObject.AddComponent<BoardMultiplayerAdapter>();
        }
        MultiplayerManager.Instance.RegisterLocalAdapter(adapter);

        // Remote view
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

        // Debug overlay
        if (FindObjectOfType<DebugOverlay>() == null)
        {
            var dbg = new GameObject("DebugOverlay");
            dbg.AddComponent<DebugOverlay>();
        }
    }

    private RemoteBoardView CreateRemote(Vector3 pos)
    {
        GameObject root;
        if (remoteBoardPrefab != null)
            root = Instantiate(remoteBoardPrefab, pos, Quaternion.identity);
        else
        {
            root = new GameObject("OpponentBoardRoot");
            root.transform.position = pos;
            root.AddComponent<Grid>();
            var tgo = new GameObject("Tilemap");
            tgo.transform.SetParent(root.transform, false);
            tgo.AddComponent<Tilemap>();
            tgo.AddComponent<TilemapRenderer>();
        }
        var rv = root.GetComponent<RemoteBoardView>();
        if (rv == null) rv = root.AddComponent<RemoteBoardView>();
        return rv;
    }
}