using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Board))]
public class BoardMultiplayerAdapter : MonoBehaviour
{
    public Board board;
    public MultiplayerManager multiplayer;
    public int queuePreviewCount = 5;

    private Tilemap _tilemap;
    private Dictionary<Tile, int> _tileToShapeIndex;

    private void Awake()
    {
        board ??= GetComponent<Board>();
        _tilemap = board.tilemap;
        multiplayer = MultiplayerManager.Instance;
        BuildTileLookup();
        multiplayer?.RegisterBoardAdapter(this);
    }

    private void BuildTileLookup()
    {
        _tileToShapeIndex = new Dictionary<Tile, int>();
        for (int i = 0; i < board.TetrisBlocks.Length; i++)
        {
            var t = board.TetrisBlocks[i].tile;
            if (t != null && !_tileToShapeIndex.ContainsKey(t))
                _tileToShapeIndex.Add(t, i);
        }
    }

    private void LateUpdate()
    {
        if (multiplayer == null) return;
        if (multiplayer.IsServer && multiplayer.ShouldBroadcastThisFrame())
        {
            var msg = CaptureBoardState();
            multiplayer.BroadcastBoardState(msg);
        }
    }

    public BoardStateMessage CaptureBoardState()
    {
        var bounds = board.Bounds;
        var lockedX = new List<int>();
        var lockedY = new List<int>();
        var lockedShapeIndex = new List<int>();

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
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
                    lockedShapeIndex.Add(shapeIdx);
                }
            }
        }

        bool hasActive = board.activePiece != null && board.activePiece.cells != null;
        int[] activeOffsetX = new int[hasActive ? board.activePiece.cells.Length : 0];
        int[] activeOffsetY = new int[hasActive ? board.activePiece.cells.Length : 0];
        int activeShapeIndex = -1;

        if (hasActive)
        {
            for (int i = 0; i < board.activePiece.cells.Length; i++)
            {
                activeOffsetX[i] = board.activePiece.cells[i].x;
                activeOffsetY[i] = board.activePiece.cells[i].y;
            }
            var tile = board.activePiece.TBSData.tile;
            if (tile != null && _tileToShapeIndex.TryGetValue(tile, out var idx))
                activeShapeIndex = idx;
        }

        int qLen = queuePreviewCount;
        int[] upcoming = new int[qLen];
        for (int i = 0; i < qLen; i++)
        {
            upcoming[i] = board.shapesQueue.peekShape(i);
        }

        return new BoardStateMessage
        {
            width = bounds.width,
            height = bounds.height,
            lockedCount = lockedX.Count,
            lockedX = lockedX.ToArray(),
            lockedY = lockedY.ToArray(),
            lockedShapeIndex = lockedShapeIndex.ToArray(),
            hasActive = hasActive,
            activeShapeIndex = activeShapeIndex,
            activePosX = hasActive ? board.activePiece.position.x : 0,
            activePosY = hasActive ? board.activePiece.position.y : 0,
            activeCellOffsetX = activeOffsetX,
            activeCellOffsetY = activeOffsetY,
            queueLength = qLen,
            upcomingShapes = upcoming,
            gameOver = false
        };
    }

    public void ApplyBoardState(BoardStateMessage state)
    {
        board.tilemap.ClearAllTiles();

        for (int i = 0; i < state.lockedCount; i++)
        {
            int sx = state.lockedX[i];
            int sy = state.lockedY[i];
            int shapeIdx = state.lockedShapeIndex[i];
            if (shapeIdx >= 0 && shapeIdx < board.TetrisBlocks.Length)
            {
                var tile = board.TetrisBlocks[shapeIdx].tile;
                board.tilemap.SetTile(new Vector3Int(sx, sy, 0), tile);
            }
        }

        if (state.hasActive && state.activeShapeIndex >= 0 && state.activeShapeIndex < board.TetrisBlocks.Length)
        {
            var shapeData = board.TetrisBlocks[state.activeShapeIndex];
            var newCells = new Vector3Int[state.activeCellOffsetX.Length];
            for (int i = 0; i < newCells.Length; i++)
            {
                newCells[i] = new Vector3Int(state.activeCellOffsetX[i], state.activeCellOffsetY[i], 0);
            }
            board.activePiece.ApplyNetworkState(
                new Vector3Int(state.activePosX, state.activePosY, 0),
                newCells,
                shapeData
            );
            board.Set(board.activePiece);
        }
    }
}