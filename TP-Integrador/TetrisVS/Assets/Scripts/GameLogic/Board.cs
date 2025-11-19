using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class Board : MonoBehaviour
{
  // Constants (replace magic numbers)
  private const int DefaultBoardWidth = 10;
  private const int DefaultBoardHeight = 20;
  private const int LinesPerLevel = 10;
  private const int ScoreSingleLine = 40;
  private const int ScoreDoubleLine = 100;
  private const int ScoreTripleLine = 300;
  private const int ScoreTetrisLine = 1200;
  private const int MaxGarbageApplyPerLockDefault = 8;
  private const int GameOverSceneIndex = 5;

  public Tilemap tilemap { get; private set; }
  public TetrisBlockShapeData[] TetrisBlocks;
  public Piece activePiece { get; private set; }
  public Vector3Int spawnPos;
  public Vector2Int boardBoundsSize = new Vector2Int(DefaultBoardWidth, DefaultBoardHeight);

  public SharedShapesQueue shapesQueue; // Shared queue
  public MultiplayerManager multiplayerManager;
  public Score scoreUI;

  // Audio
  public AudioSource audioSource;
  public AudioSource musicSource;
  private AudioClip lineClearClip;
  private AudioClip bgMusic;

  // Game state
  public int score = 0;
  public int linesCleared = 0;
  public int level = 1;
  public bool gameOver = false;

  // Garbage system
  private int pendingGarbageLines = 0;
  private readonly System.Random garbageRng = new System.Random();
  public int maxGarbageApplyPerLock = MaxGarbageApplyPerLockDefault; // safety cap

  public RectInt Bounds
  {
    get
    {
      Vector2Int position = new Vector2Int(-boardBoundsSize.x / 2, -boardBoundsSize.y / 2);
      return new RectInt(position, boardBoundsSize);
    }
  }

  private void Awake()
  {
    tilemap = GetComponentInChildren<Tilemap>();
    activePiece = GetComponentInChildren<Piece>();

    InitializeAudio();

    if (TetrisBlocks == null || TetrisBlocks.Length == 0)
    {
      Debug.LogError("Board: TetrisBlocks array is null or empty! Assign in inspector.");
      return;
    }

    InitializeTetrisBlocks();

    if (shapesQueue == null)
    {
      shapesQueue = new SharedShapesQueue();
    }
  }

  private void InitializeTetrisBlocks()
  {
    for (int i = 0; i < TetrisBlocks.Length; i++)
    {
      if (TetrisBlocks[i].tile != null)
      {
        TetrisBlocks[i].Initialize();
      }
      else
      {
        Debug.LogError($"TetrisBlocks[{i}].tile is NULL!");
      }
    }
  }

  private void InitializeAudio()
  {
    lineClearClip = Resources.Load<AudioClip>("clear_line");
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
  }

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
      Debug.LogError("Board: Cannot spawn piece - TetrisBlocks not initialized!");
    }
  }

  public void SpawnPiece()
  {
    if (gameOver)
    {
      Debug.Log("Board: Cannot spawn piece - game is over.");
      return;
    }

    if (TetrisBlocks == null || TetrisBlocks.Length == 0)
    {
      Debug.LogError("Board: Cannot spawn piece - TetrisBlocks empty.");
      return;
    }

    int shapeIndex = shapesQueue.GetShape();

    if (shapeIndex < 0 || shapeIndex >= TetrisBlocks.Length)
    {
      Debug.LogError($"Invalid shape index: {shapeIndex}, defaulting to 0.");
      shapeIndex = 0;
      if (shapeIndex >= TetrisBlocks.Length)
      {
        Debug.LogError("Board: No valid shapes available!");
        return;
      }
    }

    TetrisBlockShapeData data = TetrisBlocks[shapeIndex];

    if (data.tile == null)
    {
      Debug.LogError($"Board: TetrisBlocks[{shapeIndex}] has null tile!");
      return;
    }

    activePiece.Initialize(this, spawnPos, data);

    if (IsValidPosition(activePiece, spawnPos))
    {
      Set(activePiece);
    }
    else
    {
      GameOver();
    }

    if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsServer)
    {
      MultiplayerManager.Instance.BroadcastQueueUpdate();
    }
  }

  public void SynchronizeQueue(QueueStateMessage queueState)
  {
    if (shapesQueue == null)
    {
      shapesQueue = new SharedShapesQueue(queueState.seed);
    }
    shapesQueue.ApplyQueueState(queueState);
    Debug.Log($"Board: Queue synchronized with {queueState.upcomingShapes.Length} shapes");
  }

  // Place piece tiles
  public void Set(Piece piece)
  {
    for (int i = 0; i < piece.cells.Length; i++)
    {
      Vector3Int tilePosition = piece.cells[i] + piece.position;
      tilemap.SetTile(tilePosition, piece.TBSData.tile);
    }
  }

  // Remove piece tiles
  public void Clear(Piece piece)
  {
    for (int i = 0; i < piece.cells.Length; i++)
    {
      Vector3Int tilePosition = piece.cells[i] + piece.position;
      tilemap.SetTile(tilePosition, null);
    }
  }

  public bool IsValidPosition(Piece piece, Vector3Int position)
  {
    RectInt bounds = Bounds;

    for (int i = 0; i < piece.cells.Length; i++)
    {
      Vector3Int tilePosition = piece.cells[i] + position;

      if (!bounds.Contains((Vector2Int)tilePosition))
      {
        return false;
      }

      if (tilemap.HasTile(tilePosition))
      {
        return false;
      }
    }
    return true;
  }

  public void ClearLines()
  {
    RectInt bounds = Bounds;
    int clearedLines = GetFullLines(bounds);

    if (clearedLines > 0)
    {
      if (audioSource != null && lineClearClip != null)
      {
        audioSource.PlayOneShot(lineClearClip);
      }
      linesCleared += clearedLines;
      UpdateGameStats(clearedLines);
    }
  }

  private int GetFullLines(RectInt bounds)
  {
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
    return clearedLines;
  }

  private void UpdateGameStats(int clearedLines)
  {
    UpdateScore(clearedLines);
    UpdateLevel();

    var adapter = GetComponent<BoardMultiplayerAdapter>();
    if (adapter != null)
    {
      adapter.NotifyLinesCleared(clearedLines);
    }
  }

  private void UpdateScore(int lines)
  {
    int baseScore = lines switch
    {
      1 => ScoreSingleLine,
      2 => ScoreDoubleLine,
      3 => ScoreTripleLine,
      4 => ScoreTetrisLine,
      _ => 0
    };

    score += baseScore * level;

    if (scoreUI != null)
    {
      scoreUI.UpdateScore(score);
    }
  }

  private void UpdateLevel()
  {
    int newLevel = (linesCleared / LinesPerLevel) + 1;
    if (newLevel > level)
    {
      level = newLevel;
      Debug.Log($"Level up! Now level {level}");
    }
  }

  private bool IsLineFull(int row)
  {
    RectInt bounds = Bounds;

    for (int col = bounds.xMin; col < bounds.xMax; col++)
    {
      Vector3Int position = new Vector3Int(col, row, 0);
      if (!tilemap.HasTile(position))
      {
        return false;
      }
    }
    return true;
  }

  private void LineClear(int row)
  {
    RectInt bounds = Bounds;

    for (int col = bounds.xMin; col < bounds.xMax; col++)
    {
      Vector3Int position = new Vector3Int(col, row, 0);
      tilemap.SetTile(position, null);
    }

    MoveRowsDown(row, bounds);
  }

  private void MoveRowsDown(int row, RectInt bounds)
  {
    while (row < bounds.yMax)
    {
      for (int col = bounds.xMin; col < bounds.xMax; col++)
      {
        Vector3Int abovePos = new Vector3Int(col, row + 1, 0);
        TileBase above = tilemap.GetTile(abovePos);

        Vector3Int currentPos = new Vector3Int(col, row, 0);
        tilemap.SetTile(currentPos, above);
      }
      row++;
    }
  }

  private void GameOver()
  {
    if (gameOver)
    {
      return;
    }

    gameOver = true;

    if (activePiece != null)
    {
      activePiece.enabled = false;
    }
    if (musicSource != null)
    {
      musicSource.Stop();
    }

    SceneManager.LoadScene(GameOverSceneIndex);
  }

  public void RestartGame()
  {
    tilemap.ClearAllTiles();

    gameOver = false;
    score = 0;
    linesCleared = 0;
    level = 1;
    pendingGarbageLines = 0;

    if (MultiplayerManager.Instance == null || MultiplayerManager.Instance.IsServer)
    {
      shapesQueue = new SharedShapesQueue();
    }

    if (activePiece != null)
    {
      activePiece.enabled = true;
    }

    SpawnPiece();
  }

  // Garbage System

  public void EnqueueGarbage(int count)
  {
    if (count <= 0 || gameOver)
    {
      return;
    }
    pendingGarbageLines += count;
  }

  public void ApplyPendingGarbage()
  {
    if (pendingGarbageLines <= 0 || gameOver)
    {
      return;
    }

    int applyCount = Mathf.Min(pendingGarbageLines, maxGarbageApplyPerLock);
    pendingGarbageLines -= applyCount;

    ApplyGarbageLines(applyCount);
  }

  private void ApplyGarbageLines(int count)
  {
    if (count <= 0)
    {
      return;
    }

    RectInt bounds = Bounds;

    bool canApply = ShiftExistingTilesUp(count, ref bounds);
    if (!canApply)
    {
      return;
    }

    // Clear newly created gaps at bottom zone after shift
    for (int y = bounds.yMin; y < bounds.yMin + count; y++)
    {
      for (int x = bounds.xMin; x < bounds.xMax; x++)
      {
        tilemap.SetTile(new Vector3Int(x, y, 0), null);
      }
    }

    CreateBottomRows(count, bounds);
  }

  private void CreateBottomRows(int count, RectInt bounds)
  {
    for (int g = 0; g < count; g++)
    {
      int holeColumn = garbageRng.Next(bounds.xMin, bounds.xMax);

      for (int x = bounds.xMin; x < bounds.xMax; x++)
      {
        if (x == holeColumn)
        {
          continue;
        }

        TileBase garbageTile = TetrisBlocks.Length > 0 ? TetrisBlocks[0].tile : null;
        if (garbageTile != null)
        {
          tilemap.SetTile(new Vector3Int(x, bounds.yMin + g, 0), garbageTile);
        }
      }
    }
  }

  private bool ShiftExistingTilesUp(int count, ref RectInt bounds)
  {
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
            GameOver();
            return false;
          }
          tilemap.SetTile(toPos, tile);
        }
      }
    }
    return true;
  }
}