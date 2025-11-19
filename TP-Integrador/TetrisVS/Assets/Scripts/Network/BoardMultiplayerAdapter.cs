using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Adapter to interface the local Board with the multiplayer system.
/// </summary>
[RequireComponent(typeof(Board))]
public class BoardMultiplayerAdapter : MonoBehaviour
{
  private const int UnknownShapeIndex = -1;
  private const int DefaultQueuePreviewCount = 5;

  public event System.Action OnPiecePlaced;
  public event System.Action OnPieceRotated;
  public event System.Action OnPieceMoved;
  public event System.Action<int> OnLinesCleared;
  public event System.Action OnGameStateChanged;
  public event System.Action<int> OnGarbageReceived;

  public Board board;
  public int queuePreviewCount = DefaultQueuePreviewCount;

  private Tilemap _tilemap;
  private Dictionary<Tile, int> _tileToShapeIndex;

  private void Awake()
  {
    board ??= GetComponent<Board>();
    _tilemap = board.tilemap;
    BuildLookup();
  }

  private void Start()
  {
    MultiplayerManager.Instance?.RegisterLocalAdapter(this);
    SubscribeToGameEvents();
  }

  private void SubscribeToGameEvents()
  {
    // Extend as needed
  }

  private void BuildLookup()
  {
    _tileToShapeIndex = new Dictionary<Tile, int>();
    for (int i = 0; i < board.TetrisBlocks.Length; i++)
    {
      var t = board.TetrisBlocks[i].tile;
      if (t != null && !_tileToShapeIndex.ContainsKey(t))
      {
        _tileToShapeIndex.Add(t, i);
      }
    }
  }

  /// <summary>
  /// Capture the current board state for network synchronization.
  /// </summary>
  public BoardStateMessage CaptureBoardState()
  {
    var b = board.Bounds;
    var lockedX = new List<int>();
    var lockedY = new List<int>();
    var lockedShapeIdx = new List<int>();

    for (int y = b.yMin; y < b.yMax; y++)
    {
      for (int x = b.xMin; x < b.xMax; x++)
      {
        var pos = new Vector3Int(x, y, 0);
        if (_tilemap.HasTile(pos))
        {
          var tile = _tilemap.GetTile(pos) as Tile;
          int shapeIdx = UnknownShapeIndex;
          if (tile != null && _tileToShapeIndex.TryGetValue(tile, out var idx))
          {
            shapeIdx = idx;
          }
          lockedX.Add(x);
            lockedY.Add(y);
          lockedShapeIdx.Add(shapeIdx);
        }
      }
    }

    bool hasActive = board.activePiece != null && board.activePiece.cells != null;
    int[] offX = new int[hasActive ? board.activePiece.cells.Length : 0];
    int[] offY = new int[hasActive ? board.activePiece.cells.Length : 0];
    int activeShapeIndex = UnknownShapeIndex;

    if (hasActive)
    {
      for (int i = 0; i < board.activePiece.cells.Length; i++)
      {
        offX[i] = board.activePiece.cells[i].x;
        offY[i] = board.activePiece.cells[i].y;
      }

      var t = board.activePiece.TBSData.tile;
      if (t != null && _tileToShapeIndex.TryGetValue(t, out var idx))
      {
        activeShapeIndex = idx;
      }
    }

    int qLen = queuePreviewCount;
    int[] upcoming = new int[qLen];
    for (int i = 0; i < qLen; i++)
    {
      upcoming[i] = board.shapesQueue.PeekShape(i);
    }

    return new BoardStateMessage
    {
      width = b.width,
      height = b.height,
      lockedCount = lockedX.Count,
      lockedX = lockedX.ToArray(),
      lockedY = lockedY.ToArray(),
      lockedShapeIndex = lockedShapeIdx.ToArray(),
      hasActive = hasActive,
      activeShapeIndex = activeShapeIndex,
      activePosX = hasActive ? board.activePiece.position.x : 0,
      activePosY = hasActive ? board.activePiece.position.y : 0,
      activeCellOffsetX = offX,
      activeCellOffsetY = offY,
      queueLength = qLen,
      upcomingShapes = upcoming,
      gameOver = false
    };
  }

  public void NotifyPiecePlaced()
  {
    OnPiecePlaced?.Invoke();
    Debug.Log("[BoardMultiplayerAdapter] Piece placed notification");
  }

  public void NotifyPieceMoved()
  {
    OnPieceMoved?.Invoke();
    Debug.Log("[BoardMultiplayerAdapter] Piece moved notification");
  }

  public void NotifyPieceRotated()
  {
    OnPieceRotated?.Invoke();
    Debug.Log("[BoardMultiplayerAdapter] Piece rotated notification");
  }

  public void NotifyLinesCleared(int count)
  {
    OnLinesCleared?.Invoke(count);
    MultiplayerManager.Instance?.NotifyLinesCleared(count);
    Debug.Log($"[BoardMultiplayerAdapter] Lines cleared notification: {count}");
  }

  public void NotifyGameStateChanged()
  {
    OnGameStateChanged?.Invoke();
    Debug.Log("[BoardMultiplayerAdapter] Game state changed notification");
  }

  /// <summary>
  /// Apply incoming garbage lines to the local board.
  /// </summary>
  public void ApplyIncomingGarbage(int count)
  {
    if (count <= 0) return;
    board.EnqueueGarbage(count);
    OnGarbageReceived?.Invoke(count);
    Debug.Log($"[BoardMultiplayerAdapter] Received {count} garbage lines");
  }

  public void ApplyRemoteBoardState(BoardStateMessage state)
  {
    Debug.Log($"[BoardMultiplayerAdapter] Received remote board state from {state.owner}");
  }
}