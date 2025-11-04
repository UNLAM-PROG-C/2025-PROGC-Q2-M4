using UnityEngine;

/// <summary>
/// Helper component to integrate with existing game logic and trigger network events
/// Add this to your Piece prefab or game objects that need to trigger network updates
/// </summary>
public class EventBasedNetworkTrigger : MonoBehaviour
{
    private BoardMultiplayerAdapter adapter;

    private void Start()
    {
        adapter = FindObjectOfType<BoardMultiplayerAdapter>();
    }

    // Call these methods from your existing game logic (Board.cs, Piece.cs, etc.)
    public void OnPiecePlaced()
    {
        adapter?.NotifyPiecePlaced();
    }

    public void OnPieceMoved()
    {
        adapter?.NotifyPieceMoved(); 
    }

    public void OnPieceRotated()
    {
        adapter?.NotifyPieceRotated();
    }

    public void OnLinesCleared(int count)
    {
        adapter?.NotifyLinesCleared(count);
    }
}