using UnityEngine;
using UnityEngine.Tilemaps;

public class QueueRenderer : MonoBehaviour
{
    public ShapesQueue shapesQueue = new ShapesQueue();
    public Tilemap tilemap { get; private set; }
    public TetrisBlockShapeData[] TetrisBlocks;
    public Vector3Int spawnPos;
    public Vector2Int boardBoundsSize = new Vector2Int(4, 20);
    public int queueRenderSize;

    void Awake() //unity method that gets called when the component, in this case Board gets initialized
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        for (int i = 0; i < this.TetrisBlocks.Length; i++)
        {
            this.TetrisBlocks[i].Initialize();
        }
    }

    void Update()
    {
        RenderQueue();
    }

    public void RenderQueue()
    {
        this.tilemap.ClearAllTiles();

        for (int y = 0; y < queueRenderSize; y++)
        {
            int shapeIndex = shapesQueue.peekShape(y);
            TetrisBlockShapeData blockData = this.TetrisBlocks[shapeIndex];
            for (int i = 0; i < blockData.cells.Length; i++)
            {
                Vector3Int tilePosition = (Vector3Int)blockData.cells[i] + new Vector3Int(spawnPos[0], spawnPos[1] + 1 + y * 4, spawnPos[2]); // Offset each piece vertically
                this.tilemap.SetTile(tilePosition, blockData.tile);
            }
        }
    }


}
