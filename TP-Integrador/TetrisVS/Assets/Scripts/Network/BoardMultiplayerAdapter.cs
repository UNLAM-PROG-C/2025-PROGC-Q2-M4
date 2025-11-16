using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
// Adapter to interface the local Board with the multiplayer system
[RequireComponent(typeof(Board))]
public class BoardMultiplayerAdapter : MonoBehaviour
{
    public event System.Action OnPiecePlaced;
    public event System.Action OnPieceRotated;
    public event System.Action OnPieceMoved;
    public event System.Action<int> OnLinesCleared;
    public event System.Action OnGameStateChanged;
    public event System.Action<int> OnGarbageReceived; // NEW

    public Board board;
    public int queuePreviewCount = 5;

    private Tilemap _tilemap;
    private Dictionary<Tile, int> _tileToShapeIndex;
// Initialize the adapter and build tile lookup
    private void Awake()
    {// Ensure board and tilemap references
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
    {// Build a mapping from Tile to shape index for quick lookup
        _tileToShapeIndex = new Dictionary<Tile, int>();
        for (int i = 0; i < board.TetrisBlocks.Length; i++)
        {
            var t = board.TetrisBlocks[i].tile;
            if (t != null && !_tileToShapeIndex.ContainsKey(t))
                _tileToShapeIndex.Add(t, i);
        }
    }

    public BoardStateMessage CaptureBoardState()
    {// Capture the current state of the board for multiplayer synchronization
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
                {// Tile is locked in place
                    var tile = _tilemap.GetTile(pos) as Tile;
                    int shapeIdx = -1;
                    if (tile != null && _tileToShapeIndex.TryGetValue(tile, out var idx))
                        shapeIdx = idx;
                    lockedX.Add(x);
                    lockedY.Add(y);
                    lockedShapeIdx.Add(shapeIdx);
                }
            }
        }
// Capture active piece info
        bool hasActive = board.activePiece != null && board.activePiece.cells != null;
        int[] offX = new int[hasActive ? board.activePiece.cells.Length : 0];
        int[] offY = new int[hasActive ? board.activePiece.cells.Length : 0];
        int activeShapeIndex = -1;
        if (hasActive)
        {
            for (int i = 0; i < board.activePiece.cells.Length; i++)
            {
                offX[i] = board.activePiece.cells[i].x;
                offY[i] = board.activePiece.cells[i].y;
            }
            var t = board.activePiece.TBSData.tile;
            if (t != null && _tileToShapeIndex.TryGetValue(t, out var idx))
                activeShapeIndex = idx;
        }
// Capture upcoming shapes in the queue
        int qLen = queuePreviewCount;
        int[] upcoming = new int[qLen];
        for (int i = 0; i < qLen; i++)
            upcoming[i] = board.shapesQueue.PeekShape(i);

        return new BoardStateMessage
        {// Fill in the board state message
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
// Notification methods for various game events
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

    // NEW: Apply incoming garbage lines
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