using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// View to display the remote player's board state.
/// </summary>
public class RemoteBoardView : MonoBehaviour
{
  public Tilemap tilemap;
  public TetrisBlockShapeData[] TetrisBlocks;
  public bool drawActivePiece = true;

  private void Awake()
  {
    if (tilemap == null)
    {
      tilemap = GetComponentInChildren<Tilemap>() ?? gameObject.AddComponent<Tilemap>();
    }

    if (TetrisBlocks != null)
    {
      for (int i = 0; i < TetrisBlocks.Length; i++)
      {
        TetrisBlocks[i].Initialize();
      }
    }
  }

  /// <summary>
  /// Apply the received board state to the tilemap.
  /// </summary>
  public void ApplyBoardState(BoardStateMessage state)
  {
    if (tilemap == null || TetrisBlocks == null || TetrisBlocks.Length == 0) return;

    tilemap.ClearAllTiles();

    for (int i = 0; i < state.lockedCount; i++)
    {
      int shapeIdx = state.lockedShapeIndex[i];
      if (shapeIdx >= 0 && shapeIdx < TetrisBlocks.Length)
      {
        var tile = TetrisBlocks[shapeIdx].tile;
        tilemap.SetTile(new Vector3Int(state.lockedX[i], state.lockedY[i], 0), tile);
      }
    }

    if (drawActivePiece &&
        state.hasActive &&
        state.activeShapeIndex >= 0 &&
        state.activeShapeIndex < TetrisBlocks.Length)
    {
      var shapeData = TetrisBlocks[state.activeShapeIndex];
      for (int i = 0; i < state.activeCellOffsetX.Length; i++)
      {
        var pos = new Vector3Int(
          state.activeCellOffsetX[i] + state.activePosX,
          state.activeCellOffsetY[i] + state.activePosY,
          0);
        tilemap.SetTile(pos, shapeData.tile);
      }
    }
  }
}