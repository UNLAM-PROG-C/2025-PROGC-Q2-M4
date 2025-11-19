using UnityEngine;

/// <summary>
/// Helper component to integrate with existing game logic and trigger network events.
/// Add this to objects (e.g., Piece prefab) that need to notify multiplayer system.
/// </summary>
public class EventBasedNetworkTrigger : MonoBehaviour
{
  private BoardMultiplayerAdapter _adapter;

  private void Start()
  {
    _adapter = FindObjectOfType<BoardMultiplayerAdapter>();
  }

  public void OnPiecePlaced() => _adapter?.NotifyPiecePlaced();

  public void OnPieceMoved() => _adapter?.NotifyPieceMoved();

  public void OnPieceRotated() => _adapter?.NotifyPieceRotated();

  public void OnLinesCleared(int count) => _adapter?.NotifyLinesCleared(count);
}