using UnityEngine;
using UnityEngine.SceneManagement;

public class Piece : MonoBehaviour
{
  private const int CellsPerPiece = 4;
  private const int RotationStates = 4;
  private const float DefaultStepDelay = 1f;
  private const float DefaultLockDelay = 0.5f;
  private const float DefaultMoveCooldown = 0.05f;

  public Board board;
  public int rotationIndex { get; private set; }
  public Vector3Int position { get; set; }

  public Vector3Int[] cells;
  public TetrisBlockShapeData TBSData;

  public AudioSource audioSource;
  private AudioClip pieceLock;

  public float stepDelay = DefaultStepDelay;
  public float lockDelay = DefaultLockDelay;

  private float moveCooldown = DefaultMoveCooldown;
  private float lastMoveTime = 0f;

  private float stepTime;
  private float lockTime;

  private void Awake()
  {
    pieceLock = Resources.Load<AudioClip>("piece_lock");
    SetupAudioSource();

    if (cells == null || cells.Length != CellsPerPiece)
    {
      cells = new Vector3Int[CellsPerPiece];
    }

    rotationIndex = 0;

    int difficulty = PlayerPrefs.GetInt("DifficultyLevel", 2);
    int difficultyDivisor = Mathf.Max(1, difficulty - 1);
    stepDelay /= difficultyDivisor;
    lockDelay /= difficultyDivisor;
  }

  private void SetupAudioSource()
  {
    if (audioSource == null)
    {
      audioSource = gameObject.AddComponent<AudioSource>();
      audioSource.loop = false;
    }
  }

  public void Initialize(Board gameBoard, Vector3Int spawnPos, TetrisBlockShapeData data)
  {
    board = gameBoard;
    position = spawnPos;
    TBSData = data;

    for (int i = 0; i < data.cells.Length; i++)
    {
      cells[i] = (Vector3Int)data.cells[i];
    }

    stepTime = Time.time + stepDelay;
    lockTime = 0f;
    rotationIndex = 0;
  }

  private void Update()
  {
    if (!CanUpdate())
    {
      return;
    }

    HandleInputLocal();
    HandleGravity();
  }

  private bool CanUpdate()
  {
    if (board != null && board.gameOver)
    {
      return false;
    }

    if (MultiplayerManager.Instance != null)
    {
      if (!MultiplayerManager.Instance.IsGameInitialized)
      {
        return false;
      }

      bool canPlay = MultiplayerManager.Instance.CanStartGame ||
                     MultiplayerManager.Instance.currentGameState == GameState.Ready ||
                     MultiplayerManager.Instance.currentRole == MultiplayerRole.None;

      if (!canPlay)
      {
        return false;
      }
    }

    return true;
  }

  private void HandleInputLocal()
  {
    if (Time.time - lastMoveTime < moveCooldown)
    {
      return;
    }

    if (Input.GetKeyDown(KeyCode.Q))
    {
      RotatePiece(1);
      lastMoveTime = 0;
    }
    else if (Input.GetKeyDown(KeyCode.E))
    {
      RotatePiece(-1);
      lastMoveTime = 0;
    }
    else if (Input.GetKey(KeyCode.LeftArrow))
    {
      TryMove(Vector3Int.left);
      lastMoveTime = Time.time;
    }
    else if (Input.GetKey(KeyCode.RightArrow))
    {
      TryMove(Vector3Int.right);
      lastMoveTime = Time.time;
    }
    else if (Input.GetKey(KeyCode.DownArrow))
    {
      TryMove(Vector3Int.down);
      lastMoveTime = Time.time;
    }
    else if (Input.GetKeyDown(KeyCode.Space))
    {
      HardDrop();
      lastMoveTime = Time.time;
    }
    else if (Input.GetKeyDown(KeyCode.Escape))
    {
      board.multiplayerManager?.LeaveGame();
      SceneManager.LoadScene(0);
    }
    else if (Input.GetKeyDown(KeyCode.R))
    {
      if (MultiplayerManager.Instance == null || MultiplayerManager.Instance.IsServer)
      {
        board.RestartGame();
      }
    }
#if UNITY_EDITOR
    else if (Input.GetKeyDown(KeyCode.F))
    {
      MultiplayerManager.Instance?.ForceStartGame();
    }
#endif
  }

  private void HandleGravity()
  {
    if (Time.time >= stepTime)
    {
      StepDown();
    }
  }

  private void StepDown()
  {
    TryMove(Vector3Int.down);
    stepTime = Time.time + stepDelay;
  }

  private static bool IsDown(Vector3Int dir)
  {
    return dir == Vector3Int.down;
  }

  private void TryMove(Vector3Int direction)
  {
    board.Clear(this);
    position += direction;

    if (board.IsValidPosition(this, position))
    {
      MultiplayerManager.Instance?.NotifyPieceMoved();
    }
    else
    {
      position -= direction;
      if (IsDown(direction))
      {
        Lock();
      }
    }

    board.Set(this);
  }

  private void HardDrop()
  {
    board.Clear(this);
    while (board.IsValidPosition(this, position + Vector3Int.down))
    {
      position += Vector3Int.down;
    }
    board.Set(this);
    Lock();
  }

  private void Lock()
  {
    if (audioSource != null && pieceLock != null)
    {
      audioSource.PlayOneShot(pieceLock);
    }

    board.Clear(this);
    board.Set(this);

    MultiplayerManager.Instance?.NotifyPiecePlaced();

    board.ClearLines();
    board.ApplyPendingGarbage();
    board.SpawnPiece();
  }

  private void RotatePiece(int direction)
  {
    board.Clear(this);

    int originalRotation = rotationIndex;
    rotationIndex = Wrap(rotationIndex + direction, 0, RotationStates);
    ApplyRotationMatrix(direction);

    if (!TestWallKicks(rotationIndex, direction))
    {
      rotationIndex = originalRotation;
      ApplyRotationMatrix(-direction);
      board.Set(this);
      return;
    }

    MultiplayerManager.Instance?.NotifyPieceRotated();
    board.Set(this);
  }

  private void ApplyRotationMatrix(int direction)
  {
    float[] matrix = Data.RotationMatrix;

    for (int i = 0; i < cells.Length; i++)
    {
      Vector3 cell = cells[i];
      ApplyRotationOnBlock(direction, matrix, ref cell);
      cells[i] = new Vector3Int(Mathf.RoundToInt(cell.x), Mathf.RoundToInt(cell.y), 0);
    }
  }

  private void ApplyRotationOnBlock(int direction, float[] matrix, ref Vector3 cell)
  {
    float x;
    float y;

    switch (TBSData.Shape)
    {
      case eTetrisBlockShapes.I:
      case eTetrisBlockShapes.O:
        cell.x -= 0.5f;
        cell.y -= 0.5f;
        x = (cell.x * matrix[0] * direction) + (cell.y * matrix[1] * direction);
        y = (cell.x * matrix[2] * direction) + (cell.y * matrix[3] * direction);
        cell.x = Mathf.Ceil(x);
        cell.y = Mathf.Ceil(y);
        break;

      default:
        x = (cell.x * matrix[0] * direction) + (cell.y * matrix[1] * direction);
        y = (cell.x * matrix[2] * direction) + (cell.y * matrix[3] * direction);
        cell.x = Mathf.Round(x);
        cell.y = Mathf.Round(y);
        break;
    }
  }

  private bool TestWallKicks(int newRotationIndex, int rotationDirection)
  {
    int wallKickIndex = newRotationIndex;
    if (rotationDirection < 0)
    {
      wallKickIndex = Wrap(newRotationIndex + 1, 0, RotationStates);
    }

    Vector2Int[,] wallKicks = TBSData.wallKicks;

    for (int i = 0; i < wallKicks.GetLength(1); i++)
    {
      int rowIndex = wallKickIndex * 2 + (rotationDirection > 0 ? 0 : 1);
      Vector2Int offset = wallKicks[rowIndex, i];
      Vector3Int testPosition = position + new Vector3Int(offset.x, offset.y, 0);

      if (board.IsValidPosition(this, testPosition))
      {
        position = testPosition;
        return true;
      }
    }
    return false;
  }

  private int Wrap(int input, int min, int max)
  {
    if (input < min)
    {
      return max - (min - input) % (max - min);
    }
    return min + (input - min) % (max - min);
  }

  public void ApplyNetworkState(Vector3Int newPos, Vector3Int[] newCells, TetrisBlockShapeData shapeData)
  {
    position = newPos;
    TBSData = shapeData;
    for (int i = 0; i < cells.Length && i < newCells.Length; i++)
    {
      cells[i] = newCells[i];
    }
  }
}