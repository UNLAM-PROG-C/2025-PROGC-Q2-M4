using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

public class Board : MonoBehaviour
{
    public Tilemap tilemap { get; private set; }
    public TetrisBlockShapeData[] TetrisBlocks;
    public Piece activePiece { get; private set; }
    public Vector3Int spawnPos;
    public Vector2Int boardBoundsSize = new Vector2Int(10, 20);
    public SharedShapesQueue shapesQueue; // Changed to SharedShapesQueue
    public MultiplayerManager multiplayerManager;

    // Game state tracking
    public int score = 0;
    public int linesCleared = 0;
    public int level = 1;
    public bool gameOver = false;

    public RectInt Bounds
    {
        get
        {
            Vector2Int position = new Vector2Int(-this.boardBoundsSize.x / 2, -this.boardBoundsSize.y / 2);
            return new RectInt(position, this.boardBoundsSize);
        }
    }

    private void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.activePiece = GetComponentInChildren<Piece>();

        Debug.Log($"Board Awake: TetrisBlocks is {(TetrisBlocks != null ? "not null" : "NULL")}");
        Debug.Log($"Board Awake: TetrisBlocks.Length = {(TetrisBlocks != null ? TetrisBlocks.Length : 0)}");

        if (TetrisBlocks == null || TetrisBlocks.Length == 0)
        {
            Debug.LogError("Board: TetrisBlocks array is null or empty! Please assign Tetris block data in the inspector.");
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
                Debug.LogError($"TetrisBlocks[{i}].tile is NULL!");
            }
        }
        
        // Initialize queue - will be overridden by multiplayer manager if needed
        if (shapesQueue == null)
        {
            shapesQueue = new SharedShapesQueue();
        }
    }

    private void Start()
    {
        if (TetrisBlocks != null && TetrisBlocks.Length > 0)
        {
            SpawnPiece();
        }
        else
        {
            Debug.LogError("Board: Cannot spawn piece - TetrisBlocks not properly initialized!");
        }
    }

    public void SpawnPiece()
    {
        if (gameOver)
        {
            Debug.Log("Board: Cannot spawn piece - game is over!");
            return;
        }

        if (TetrisBlocks == null || TetrisBlocks.Length == 0)
        {
            Debug.LogError("Board: Cannot spawn piece - TetrisBlocks array is null or empty!");
            return;
        }

        int shapeIndex = shapesQueue.GetShape();

        if (shapeIndex < 0 || shapeIndex >= TetrisBlocks.Length)
        {
            Debug.LogError($"Invalid shape index: {shapeIndex}, TetrisBlocks.Length: {TetrisBlocks.Length}");
            shapeIndex = 0;
            
            if (shapeIndex >= TetrisBlocks.Length)
            {
                Debug.LogError("Board: Cannot spawn piece - no valid shapes available!");
                return;
            }
        }

        TetrisBlockShapeData data = this.TetrisBlocks[shapeIndex];

        if (data.tile == null)
        {
            Debug.LogError($"Board: TetrisBlocks[{shapeIndex}] has null tile!");
            return;
        }

        this.activePiece.Initialize(this, spawnPos, data);

        if (IsValidPosition(this.activePiece, this.spawnPos))
        {
            Set(this.activePiece);
        }
        else
        {
            GameOver();
        }
        
        // Notify multiplayer manager of queue change
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsServer)
        {
            MultiplayerManager.Instance.BroadcastQueueUpdate();
        }
    }
    
    // Method to synchronize queue from server
    public void SynchronizeQueue(QueueStateMessage queueState)
    {
        if (shapesQueue == null)
        {
            shapesQueue = new SharedShapesQueue(queueState.seed);
        }
        shapesQueue.ApplyQueueState(queueState);
        Debug.Log($"Board: Queue synchronized with {queueState.upcomingShapes.Length} shapes");
    }

    public void Set(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position;
            this.tilemap.SetTile(tilePosition, piece.TBSData.tile);
        }
    }

    public void Clear(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position;
            this.tilemap.SetTile(tilePosition, null);
        }
    }

    public bool IsValidPosition(Piece piece, Vector3Int position)
    {
        RectInt bounds = this.Bounds;

        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + position;

            if (!bounds.Contains((Vector2Int)tilePosition))
            {
                return false;
            }

            if (this.tilemap.HasTile(tilePosition))
            {
                return false;
            }
        }

        return true;
    }

    public void ClearLines()
    {
        RectInt bounds = this.Bounds;
        int row = bounds.yMin;
        int clearedLines = 0;

        while (row < bounds.yMax)
        {
            if (IsLineFull(row))
            {
                LineClear(row);
                clearedLines++;
            }
            else
            {
                row++;
            }
        }

        if (clearedLines > 0)
        {
            // Update game statistics
            linesCleared += clearedLines;
            UpdateScore(clearedLines);
            UpdateLevel();

            Debug.Log($"Cleared {clearedLines} lines. Total: {linesCleared}");

            // Notify multiplayer manager
            var adapter = GetComponent<BoardMultiplayerAdapter>();
            if (adapter != null)
            {
                adapter.NotifyLinesCleared(clearedLines);
            }
        }
    }

    private void UpdateScore(int lines)
    {
        // Standard Tetris scoring
        int baseScore = 0;
        switch (lines)
        {
            case 1: baseScore = 40; break;   // Single
            case 2: baseScore = 100; break;  // Double  
            case 3: baseScore = 300; break;  // Triple
            case 4: baseScore = 1200; break; // Tetris
        }
        score += baseScore * level;
    }

    private void UpdateLevel()
    {
        // Level up every 10 lines
        int newLevel = (linesCleared / 10) + 1;
        if (newLevel > level)
        {
            level = newLevel;
            Debug.Log($"Level up! Now level {level}");
        }
    }

    private bool IsLineFull(int row)
    {
        RectInt bounds = this.Bounds;

        for (int col = bounds.xMin; col < bounds.xMax; col++)
        {
            Vector3Int position = new Vector3Int(col, row, 0);

            if (!this.tilemap.HasTile(position))
            {
                return false;
            }
        }

        return true;
    }

    private void LineClear(int row)
    {
        RectInt bounds = this.Bounds;

        // Clear the full row
        for (int col = bounds.xMin; col < bounds.xMax; col++)
        {
            Vector3Int position = new Vector3Int(col, row, 0);
            this.tilemap.SetTile(position, null);
        }

        // Move all rows above down by one
        while (row < bounds.yMax)
        {
            for (int col = bounds.xMin; col < bounds.xMax; col++)
            {
                Vector3Int position = new Vector3Int(col, row + 1, 0);
                TileBase above = this.tilemap.GetTile(position);

                position = new Vector3Int(col, row, 0);
                this.tilemap.SetTile(position, above);
            }

            row++;
        }
    }

    private void GameOver()
    {
        gameOver = true;
        Debug.Log("Game Over!");
        
        // Stop the active piece
        if (activePiece != null)
        {
            activePiece.enabled = false;
        }

        SceneManager.LoadScene(0);
    }

    public void RestartGame()
    {
        // Clear the board
        tilemap.ClearAllTiles();
        
        // Reset game state
        gameOver = false;
        score = 0;
        linesCleared = 0;
        level = 1;
        
        // Reinitialize queue
        if (MultiplayerManager.Instance == null || MultiplayerManager.Instance.IsServer)
        {
            shapesQueue = new SharedShapesQueue();
        }
        
        // Re-enable active piece
        if (activePiece != null)
        {
            activePiece.enabled = true;
        }
        
        // Spawn new piece
        SpawnPiece();
        
        Debug.Log("Game restarted!");
    }
}