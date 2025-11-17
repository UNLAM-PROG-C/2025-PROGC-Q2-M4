using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using System.Collections;

public class Board : MonoBehaviour
{
    public Tilemap tilemap { get; private set; }
    public TetrisBlockShapeData[] TetrisBlocks;
    public Piece activePiece { get; private set; }
    public Vector3Int spawnPos;
    public Vector2Int boardBoundsSize = new Vector2Int(10, 20);
    public SharedShapesQueue shapesQueue; // Shared queue
    public MultiplayerManager multiplayerManager;

    public Score scoreUI; // Reference to Score UI component

    //Audio
    public AudioSource audioSource;
    public AudioSource musicSource; 
    public AudioClip gameOverClip;
    private AudioClip lineClearClip;
    
    private AudioClip bgMusic; //Background music

    // Game state tracking
    public int score = 0;
    public int linesCleared = 0;
    public int level = 1;
    public bool gameOver = false;

    // Garbage system
    private int pendingGarbageLines = 0;
    private System.Random garbageRng = new System.Random();
    public int maxGarbageApplyPerLock = 8; // safety cap to avoid extreme spikes

    public RectInt Bounds
    {
        get
        {
            Vector2Int position = new Vector2Int(-this.boardBoundsSize.x / 2, -this.boardBoundsSize.y / 2);
            return new RectInt(position, this.boardBoundsSize);
        }
    }
// Initialization board and pieces
    private void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.activePiece = GetComponentInChildren<Piece>();

        //Sounds initialization
        lineClearClip = Resources.Load<AudioClip>("clear_line");
        gameOverClip = Resources.Load<AudioClip>("game_over");
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = false; 
        }

        bgMusic = Resources.Load<AudioClip>("music_bradinsky");
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
        }

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
// Start the game by spawning the first piece
    private void Start()
    {
        if (TetrisBlocks != null && TetrisBlocks.Length > 0)
        {
            scoreUI = FindObjectOfType<Score>();
            musicSource.clip = bgMusic;
            musicSource.Play();
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

        int shapeIndex = shapesQueue.GetShape(); // Get next shape from shared queue

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

        TetrisBlockShapeData data = this.TetrisBlocks[shapeIndex]; // Safe access after validation

        if (data.tile == null) 
        {
            Debug.LogError($"Board: TetrisBlocks[{shapeIndex}] has null tile!");
            return;
        }

        this.activePiece.Initialize(this, spawnPos, data); // Initialize piece with selected shape

        if (IsValidPosition(this.activePiece, this.spawnPos))
        {
            Set(this.activePiece); // Place piece on board
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

    // Synchronize queue from server
    public void SynchronizeQueue(QueueStateMessage queueState)
    {
        if (shapesQueue == null)
        {
            shapesQueue = new SharedShapesQueue(queueState.seed);
        }
        shapesQueue.ApplyQueueState(queueState);
        Debug.Log($"Board: Queue synchronized with {queueState.upcomingShapes.Length} shapes");
    }
// Place pieces on the board
    public void Set(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position;
            this.tilemap.SetTile(tilePosition, piece.TBSData.tile);
        }
    }
// Remove pieces from the board
    public void Clear(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position;
            this.tilemap.SetTile(tilePosition, null);
        }
    }
// Check if a piece can be placed at a given position
    public bool IsValidPosition(Piece piece, Vector3Int position)
    {
        RectInt bounds = this.Bounds;

        for (int i = 0; i < piece.cells.Length; i++) 
        {
            Vector3Int tilePosition = piece.cells[i] + position;

            if (!bounds.Contains((Vector2Int)tilePosition)) // Check out of bounds
            {
                return false;
            }

            if (this.tilemap.HasTile(tilePosition)) // Check if collision with existing tile
            {
                return false;
            }
        }

        return true;
    }
// Clear full lines by checking each row
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
            if (audioSource != null && lineClearClip != null)
            {
                audioSource.PlayOneShot(lineClearClip);
            }
            // Update game stats
            linesCleared += clearedLines;
            UpdateScore(clearedLines);
            UpdateLevel();

            Debug.Log($"Cleared {clearedLines} lines. Total: {linesCleared}");
            Debug.Log("-------------------------------------");
            Debug.Log($"Score: {score}, Level: {level}");
            // Notify multiplayer manager (will trigger garbage sending)
            var adapter = GetComponent<BoardMultiplayerAdapter>();
            if (adapter != null)
            {
                adapter.NotifyLinesCleared(clearedLines);
            }
        }
    }
// Update score based on lines cleared and current level
    private void UpdateScore(int lines)
    {
        int baseScore = 0;
        switch (lines)
        {
            case 1: baseScore = 40; break;
            case 2: baseScore = 100; break;
            case 3: baseScore = 300; break;
            case 4: baseScore = 1200; break;
        }
        Debug.Log($"Score increased by {baseScore * level} points");
        score += baseScore * level;
        Debug.Log($"New Score: {score}");
        // Update score UI
        Debug.Log("Updating score UI... - Before calling scoreUI.UpdateScore");
        if (scoreUI != null)
        {
            scoreUI.UpdateScore(score);
            Debug.Log("Score UI updated. - After calling scoreUI.UpdateScore");
        }
        
    }
// Increase level every 10 lines cleared
    private void UpdateLevel()
    {
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
// Clear a specific line and move above lines down
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
// Handle game over state
    private void GameOver()
    {
        if (gameOver) return;

        gameOver = true;
        if (audioSource != null && gameOverClip != null)
        {
            StartCoroutine(GameOverRoutine());
        }
        Debug.Log("Game Over!");

        if (activePiece != null)
        {
            activePiece.enabled = false;
        }
    }

    private IEnumerator GameOverRoutine()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
        audioSource.PlayOneShot(gameOverClip);

        yield return new WaitForSeconds(gameOverClip.length);

        SceneManager.LoadScene(5);// Restart the scene (goes to game over screen)
    }
// Restart the game by resetting state and clearing the board
    public void RestartGame()
    {
        tilemap.ClearAllTiles();

        gameOver = false;
        score = 0;
        linesCleared = 0;
        level = 1;
        pendingGarbageLines = 0;

        if (MultiplayerManager.Instance == null || MultiplayerManager.Instance.IsServer) // Only reset queue if not in multiplayer client mode
        {
            shapesQueue = new SharedShapesQueue();
        }

        if (activePiece != null) 
        {
            activePiece.enabled = true;
        }

        SpawnPiece();

        Debug.Log("Game restarted!");
    }

    // --- Garbage System Public Interface ---

    public void EnqueueGarbage(int count)
    {
        if (count <= 0 || gameOver) return;
        pendingGarbageLines += count;
        Debug.Log($"[Board] Enqueued {count} garbage lines (total pending: {pendingGarbageLines})");
    }

    public void ApplyPendingGarbage()
    {
        if (pendingGarbageLines <= 0 || gameOver) return;

        int applyCount = Mathf.Min(pendingGarbageLines, maxGarbageApplyPerLock);
        pendingGarbageLines -= applyCount;

        ApplyGarbageLines(applyCount);
    }

    private void ApplyGarbageLines(int count)
    {
        if (count <= 0) return;

        RectInt bounds = Bounds;

        // Shift existing tiles UP
        for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int fromPos = new Vector3Int(x, y, 0);
                TileBase tile = tilemap.GetTile(fromPos);
                if (tile != null)
                {
                    Vector3Int toPos = new Vector3Int(x, y + count, 0);
                    if (toPos.y >= bounds.yMax)
                    {
                        // Top-out
                        Debug.Log("[Board] Garbage caused top-out");
                        GameOver();
                        return;
                    }
                    tilemap.SetTile(toPos, tile);
                }
            }
        }

        // Clear old positions that were shifted (avoid duplication)
        for (int y = bounds.yMin; y < bounds.yMin + count; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
            }
        }

        // Create garbage rows at bottom
        for (int g = 0; g < count; g++)
        {
            int holeColumn = garbageRng.Next(bounds.xMin, bounds.xMax);
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                if (x == holeColumn) continue;

                // Use a neutral tile: pick first tile or any
                TileBase garbageTile = TetrisBlocks.Length > 0 ? TetrisBlocks[0].tile : null;
                if (garbageTile != null)
                {
                    tilemap.SetTile(new Vector3Int(x, bounds.yMin + g, 0), garbageTile);
                }
            }
        }

        Debug.Log($"[Board] Applied {count} garbage lines. Remaining pending: {pendingGarbageLines}");
    }
}