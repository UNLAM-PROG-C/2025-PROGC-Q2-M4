using UnityEngine;

public class BoardAdapterInitializer : MonoBehaviour
{
    private void Start()
    {
        var adapter = FindObjectOfType<BoardMultiplayerAdapter>();
        if (adapter != null && MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.RegisterBoardAdapter(adapter);
        }
    }
}