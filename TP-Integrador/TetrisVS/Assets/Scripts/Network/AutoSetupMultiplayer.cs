using UnityEngine;
using UnityEngine.Tilemaps;
// Automatically sets up multiplayer components in the scene
public class AutoSetupMultiplayer : MonoBehaviour
{
    public GameObject remoteBoardPrefab;
    public Vector3 remoteBoardOffset = new Vector3(14f, 0f, 0f);
    public TetrisBlockShapeData[] tetrisBlocksForRemote;

    private void Start()
    {
        // Create MultiplayerManager if it doesn't exist
        if (MultiplayerManager.Instance == null)
        {
            var mm = new GameObject("MultiplayerManager");
            mm.AddComponent<MultiplayerManager>(); // Unity native function
        }
        
        // Setup local board and remote view
        Board localBoard = FindObjectOfType<Board>(); // Unity native function
        if (localBoard == null)
        {
            Debug.LogError("[AutoSetupMultiplayer] No Board encontrado.");
            return;
        }

        // Adapter
        var adapter = localBoard.GetComponent<BoardMultiplayerAdapter>();  // Unity native function
        if (adapter == null)
        {
            adapter = localBoard.gameObject.AddComponent<BoardMultiplayerAdapter>(); // Unity native function
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
            dbg.AddComponent<DebugOverlay>(); // Unity native function
        }
    }

    // Create a remote board view at the specified position
    private RemoteBoardView CreateRemote(Vector3 pos)
    {
        GameObject root;
        if (remoteBoardPrefab != null)
            root = Instantiate(remoteBoardPrefab, pos, Quaternion.identity); // Unity native function
        else // Create basic remote board structure
        {
            root = new GameObject("OpponentBoardRoot");
            root.transform.position = pos;
            root.AddComponent<Grid>(); // Unity native function
            var tgo = new GameObject("Tilemap");
            tgo.transform.SetParent(root.transform, false);
            tgo.AddComponent<Tilemap>(); // Unity native function
            tgo.AddComponent<TilemapRenderer>(); // Unity native function
        }
        var rv = root.GetComponent<RemoteBoardView>(); // Unity native function
        if (rv == null) rv = root.AddComponent<RemoteBoardView>(); // Unity native function
        return rv;
    }
}