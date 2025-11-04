using UnityEngine;
using UnityEngine.Tilemaps;

public class QueueRenderer : MonoBehaviour
{
    public SharedShapesQueue shapesQueue; // Changed to SharedShapesQueue
    public Tilemap tilemap { get; private set; }
    public TetrisBlockShapeData[] TetrisBlocks;
    public Vector3Int spawnPos;
    public Vector2Int boardBoundsSize = new Vector2Int(4, 20);
    public int queueRenderSize = 5;

    private bool isInitialized = false;
    private Board board;

    void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.board = FindObjectOfType<Board>();

        if (TetrisBlocks == null || TetrisBlocks.Length == 0)
        {
            Debug.LogError("QueueRenderer: TetrisBlocks array is null or empty! Please assign Tetris block data in the inspector.");
            return;
        }

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
        Debug.Log($"QueueRenderer Awake: queueRenderSize = {queueRenderSize}");
        Debug.Log($"QueueRenderer Awake: TetrisBlocks.Length = {TetrisBlocks.Length}");
        Debug.Log($"QueueRenderer Awake: tilemap = {(tilemap != null ? "Found" : "NULL")}");
    }

    void Start()
    {
        // Use the shared queue from the board
        if (board != null && board.shapesQueue != null)
        {
            shapesQueue = board.shapesQueue;
            Debug.Log($"QueueRenderer Start: Using shared queue from board");
        }
    }

    void Update()
    {
        if (isInitialized && shapesQueue != null)
        {
            RenderQueue();
        }
    }

    public void RenderQueue()
    {
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
            int shapeIndex = shapesQueue.PeekShape(y);

            if (shapeIndex >= 0 && shapeIndex < TetrisBlocks.Length)
            {
                TetrisBlockShapeData blockData = this.TetrisBlocks[shapeIndex];
                
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

    public void RefreshQueue()
    {
        if (isInitialized)
        {
            RenderQueue();
        }
    }

    public void SetShapesQueue(SharedShapesQueue newQueue)
    {
        if (newQueue != null)
        {
            shapesQueue = newQueue;
            RefreshQueue();
            Debug.Log("QueueRenderer: Shapes queue updated and refreshed");
        }
    }
}