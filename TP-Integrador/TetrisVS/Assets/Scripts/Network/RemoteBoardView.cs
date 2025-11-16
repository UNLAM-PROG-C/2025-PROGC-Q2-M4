using UnityEngine;
using UnityEngine.Tilemaps;
// View to display the remote player's board state
public class RemoteBoardView : MonoBehaviour
{
    public Tilemap tilemap;
    public TetrisBlockShapeData[] TetrisBlocks;
    public bool drawActivePiece = true;
// Initialize the remote board view
    private void Awake()
    {// Ensure tilemap and initialize Tetris blocks
        if (tilemap == null)
            tilemap = GetComponentInChildren<Tilemap>() ?? gameObject.AddComponent<Tilemap>();

        if (TetrisBlocks != null)
            for (int i = 0; i < TetrisBlocks.Length; i++)
                TetrisBlocks[i].Initialize();
    }
// Apply the received board state to the tilemap
    public void ApplyBoardState(BoardStateMessage state)
    {
        if (tilemap == null || TetrisBlocks == null || TetrisBlocks.Length == 0) return;

        tilemap.ClearAllTiles();

        for (int i = 0; i < state.lockedCount; i++)// Draw locked pieces
        {
            int shapeIdx = state.lockedShapeIndex[i];
            if (shapeIdx >= 0 && shapeIdx < TetrisBlocks.Length)
            {
                var tile = TetrisBlocks[shapeIdx].tile;
                tilemap.SetTile(new Vector3Int(state.lockedX[i], state.lockedY[i], 0), tile);
            }
        }
// Draw active piece if applicable
        if (drawActivePiece && state.hasActive && state.activeShapeIndex >= 0 && state.activeShapeIndex < TetrisBlocks.Length)
        {
            var shapeData = TetrisBlocks[state.activeShapeIndex];
            for (int i = 0; i < state.activeCellOffsetX.Length; i++)
            {// Draw each cell of the active piece
                var pos = new Vector3Int(state.activeCellOffsetX[i] + state.activePosX,
                                         state.activeCellOffsetY[i] + state.activePosY, 0);
                tilemap.SetTile(pos, shapeData.tile);
            }
        }
    }
}