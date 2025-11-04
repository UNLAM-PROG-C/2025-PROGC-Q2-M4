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

    void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        for (int i = 0; i < this.TetrisBlocks.Length; i++)
        {
            this.TetrisBlocks[i].Initialize();
        }

        // Debug logging
        Debug.Log($"QueueRenderer Awake: queueRenderSize = {queueRenderSize}");
        Debug.Log($"QueueRenderer Awake: TetrisBlocks.Length = {TetrisBlocks.Length}");
        Debug.Log($"QueueRenderer Awake: tilemap = {(tilemap != null ? "Found" : "NULL")}");
    }

    void Start()
    {
        // Test the queue
        Debug.Log($"QueueRenderer Start: First piece in queue = {shapesQueue.peekShape(0)}");
    }

    void Update()
    {
        RenderQueue();
    }

    public void RenderQueue()
    {
        if (this.tilemap == null || this.TetrisBlocks == null || queueRenderSize <= 0)
        {
            return;
        }

        this.tilemap.ClearAllTiles();

        for (int y = 0; y < queueRenderSize; y++)
        {
            int shapeIndex = shapesQueue.peekShape(y);

            if (shapeIndex >= 0 && shapeIndex < TetrisBlocks.Length)
            {
                TetrisBlockShapeData blockData = this.TetrisBlocks[shapeIndex];
                for (int i = 0; i < blockData.cells.Length; i++)
                {
                    Vector3Int tilePosition = (Vector3Int)blockData.cells[i] + new Vector3Int(spawnPos[0], spawnPos[1] + 1 + y * 4, spawnPos[2]);
                    this.tilemap.SetTile(tilePosition, blockData.tile);
                }
            }
        }
    }
}