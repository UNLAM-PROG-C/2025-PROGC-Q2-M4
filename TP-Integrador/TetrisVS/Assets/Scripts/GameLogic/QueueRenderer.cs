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

    private bool isInitialized = false;

    void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();

        // Validate TetrisBlocks before initialization
        if (TetrisBlocks == null || TetrisBlocks.Length == 0)
        {
            Debug.LogError("QueueRenderer: TetrisBlocks array is null or empty! Please assign Tetris block data in the inspector.");
            return;
        }

        // Initialize TetrisBlocks safely
        for (int i = 0; i < this.TetrisBlocks.Length; i++)
        {
            if (TetrisBlocks[i].tile != null)
            {
                this.TetrisBlocks[i].Initialize();
            }
            else
            {
                Debug.LogError($"QueueRenderer: TetrisBlocks[{i}].tile is NULL!");
            }
        }

        isInitialized = true;

        // Debug logging
        Debug.Log($"QueueRenderer Awake: queueRenderSize = {queueRenderSize}");
        Debug.Log($"QueueRenderer Awake: TetrisBlocks.Length = {TetrisBlocks.Length}");
        Debug.Log($"QueueRenderer Awake: tilemap = {(tilemap != null ? "Found" : "NULL")}");
    }

    void Start()
    {
        if (isInitialized && shapesQueue != null)
        {
            // Test the queue
            Debug.Log($"QueueRenderer Start: First piece in queue = {shapesQueue.peekShape(0)}");
        }
    }

    void Update()
    {
        if (isInitialized)
        {
            RenderQueue();
        }
    }

    public void RenderQueue()
    {
        // Comprehensive validation before rendering
        if (!isInitialized || 
            this.tilemap == null || 
            this.TetrisBlocks == null || 
            this.TetrisBlocks.Length == 0 || 
            queueRenderSize <= 0 ||
            shapesQueue == null)
        {
            return;
        }

        this.tilemap.ClearAllTiles();

        for (int y = 0; y < queueRenderSize; y++)
        {
            int shapeIndex = shapesQueue.peekShape(y);

            // Validate shape index
            if (shapeIndex >= 0 && shapeIndex < TetrisBlocks.Length)
            {
                TetrisBlockShapeData blockData = this.TetrisBlocks[shapeIndex];
                
                // Validate block data
                if (blockData.tile == null || blockData.cells == null)
                {
                    Debug.LogError($"QueueRenderer: Invalid block data at index {shapeIndex}");
                    continue;
                }

                for (int i = 0; i < blockData.cells.Length; i++)
                {
                    Vector3Int tilePosition = (Vector3Int)blockData.cells[i] + new Vector3Int(spawnPos[0], spawnPos[1] + 1 + y * 4, spawnPos[2]);
                    this.tilemap.SetTile(tilePosition, blockData.tile);
                }
            }
            else
            {
                Debug.LogWarning($"QueueRenderer: Invalid shape index {shapeIndex} for queue position {y}");
            }
        }
    }

    // Method to refresh the queue renderer (useful for multiplayer synchronization)
    public void RefreshQueue()
    {
        if (isInitialized)
        {
            RenderQueue();
        }
    }

    // Method to set a new shapes queue (useful for multiplayer)
    public void SetShapesQueue(ShapesQueue newQueue)
    {
        if (newQueue != null)
        {
            shapesQueue = newQueue;
            RefreshQueue();
        }
    }
}