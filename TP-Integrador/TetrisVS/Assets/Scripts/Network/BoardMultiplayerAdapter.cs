using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Board))]
public class BoardMultiplayerAdapter : MonoBehaviour
{
    // Events for triggering network updates
    public event System.Action OnPiecePlaced;
    public event System.Action OnPieceRotated;
    public event System.Action OnPieceMoved;
    public event System.Action<int> OnLinesCleared;
    public event System.Action OnGameStateChanged;

    public Board board;
    public int queuePreviewCount = 5;

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
        
        // Subscribe to board events if they exist
        SubscribeToGameEvents();
    }

    private void SubscribeToGameEvents()
    {
        // Subscribe to board events - you'll need to add these events to your Board class
        // For now, we'll provide methods that can be called manually from your game logic
        
        // Example of how you could hook into existing board events:
        // if (board != null)
        // {
        //     board.OnLineClear += (lines) => OnLinesCleared?.Invoke(lines);
        //     board.OnPieceLocked += () => OnPiecePlaced?.Invoke();
        // }
    }

    private void BuildLookup()
    {
        _tileToShapeIndex = new Dictionary<Tile, int>();
        for (int i = 0; i < board.TetrisBlocks.Length; i++)
        {
            var t = board.TetrisBlocks[i].tile;
            if (t != null && !_tileToShapeIndex.ContainsKey(t))
                _tileToShapeIndex.Add(t, i);
        }
    }

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
                    int shapeIdx = -1;
                    if (tile != null && _tileToShapeIndex.TryGetValue(tile, out var idx))
                        shapeIdx = idx;
                    lockedX.Add(x);
                    lockedY.Add(y);
                    lockedShapeIdx.Add(shapeIdx);
                }
            }
        }

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

        int qLen = queuePreviewCount;
        int[] upcoming = new int[qLen];
        for (int i = 0; i < qLen; i++)
            upcoming[i] = board.shapesQueue.peekShape(i);

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

    // Methods to be called by game logic to trigger network events
    public void NotifyPiecePlaced()
    {
        OnPiecePlaced?.Invoke();
        MultiplayerManager.Instance?.NotifyPiecePlaced();
    }

    public void NotifyPieceMoved()
    {
        OnPieceMoved?.Invoke();
        MultiplayerManager.Instance?.NotifyPieceMoved();
    }

    public void NotifyPieceRotated()
    {
        OnPieceRotated?.Invoke();
        MultiplayerManager.Instance?.NotifyPieceRotated();
    }

    public void NotifyLinesCleared(int linesCount)
    {
        OnLinesCleared?.Invoke(linesCount);
        MultiplayerManager.Instance?.NotifyLinesCleared();
    }

    public void NotifyGameStateChanged()
    {
        OnGameStateChanged?.Invoke();
        MultiplayerManager.Instance?.NotifyPlayerAction("game_state_changed");
    }
}