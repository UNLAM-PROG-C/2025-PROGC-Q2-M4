using UnityEngine;
using UnityEngine.SceneManagement;

public class Piece : MonoBehaviour
{
    public Board board;
    public int rotationIndex { get; private set; }
    public Vector3Int position { get; set; }

    public Vector3Int[] cells;
    public TetrisBlockShapeData TBSData;

    public float stepDelay = 1f;
    public float lockDelay = 0.5f;

    private float stepTime;
    private float lockTime;

    void Awake()
    {
        if (cells == null || cells.Length != 4)
        {
            cells = new Vector3Int[4];
        }

        rotationIndex = 0;

        int difficulty = PlayerPrefs.GetInt("DifficultyLevel", 2);
        stepDelay = stepDelay / Mathf.Max(1, difficulty - 1);
        lockDelay = lockDelay / Mathf.Max(1, difficulty - 1);

        Debug.Log($"[Piece] Awake - stepDelay: {stepDelay}, lockDelay: {lockDelay}");
    }

    public void Initialize(Board board, Vector3Int spawnPos, TetrisBlockShapeData data)
    {
        this.board = board;
        this.position = spawnPos;
        this.TBSData = data;

        for (int i = 0; i < data.cells.Length; i++)
        {
            cells[i] = (Vector3Int)data.cells[i];
        }

        stepTime = Time.time + stepDelay;
        lockTime = 0f;
        rotationIndex = 0;

        Debug.Log($"[Piece] Initialized at position {spawnPos} with shape {data.Shape}");
    }

    void Update()
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
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            TryMove(Vector3Int.left);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            TryMove(Vector3Int.right);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            TryMove(Vector3Int.down);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            HardDrop();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            board.multiplayerManager?.LeaveGame();
            SceneManager.LoadScene(0);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            RotatePiece(1);      // Clockwise
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotatePiece(-1);     // Counter-Clockwise
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            if (MultiplayerManager.Instance == null || MultiplayerManager.Instance.IsServer)
            {
                board.RestartGame();
            }
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
#if UNITY_EDITOR
            if (MultiplayerManager.Instance != null)
            {
                MultiplayerManager.Instance.ForceStartGame();
            }
#endif
        }
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

    private void TryMove(Vector3Int direction)
    {
        board.Clear(this);
        position += direction;
        if (board.IsValidPosition(this, position))
        {
            board.Set(this);
            lockTime = 0f;

            MultiplayerManager.Instance?.NotifyPieceMoved();
        }
        else
        {
            position -= direction;
            board.Set(this);
            lockTime += Time.deltaTime;
            if (lockTime >= lockDelay)
            {
                Lock();
            }
        }
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
        board.Clear(this);
        board.Set(this);

        MultiplayerManager.Instance?.NotifyPiecePlaced();

        board.ClearLines();

        // Apply any pending garbage BEFORE spawning the next piece
        board.ApplyPendingGarbage();

        board.SpawnPiece();
    }

    private void RotatePiece(int direction)
    {
        board.Clear(this);

        int originalRotation = rotationIndex;
        rotationIndex = Wrap(rotationIndex + direction, 0, 4);

        ApplyRotationMatrix(direction);

        if (!TestWallKicks(rotationIndex, direction))
        {
            rotationIndex = originalRotation;
            ApplyRotationMatrix(-direction);
        }

        board.Set(this);

        MultiplayerManager.Instance?.NotifyPieceRotated();
    }

    private void ApplyRotationMatrix(int direction)
    {
        float[] matrix = Data.RotationMatrix;

        for (int i = 0; i < cells.Length; i++)
        {
            Vector3 cell = cells[i];

            int x, y;

            switch (TBSData.Shape)
            {
                case eTetrisBlockShapes.I:
                case eTetrisBlockShapes.O:
                    cell.x -= 0.5f;
                    cell.y -= 0.5f;
                    x = Mathf.CeilToInt((cell.x * matrix[0] * direction) + (cell.y * matrix[1] * direction));
                    y = Mathf.CeilToInt((cell.x * matrix[2] * direction) + (cell.y * matrix[3] * direction));
                    break;

                default:
                    x = Mathf.RoundToInt((cell.x * matrix[0] * direction) + (cell.y * matrix[1] * direction));
                    y = Mathf.RoundToInt((cell.x * matrix[2] * direction) + (cell.y * matrix[3] * direction));
                    break;
            }

            cells[i] = new Vector3Int(x, y, 0);
        }
    }

    private bool TestWallKicks(int rotationIndex, int rotationDirection)
    {
        // Placeholder wall kick test (SRS kicks can be added integrating Data.WallKicks)
        // For now always return true for simplicity
        return true;
    }

    private int Wrap(int input, int min, int max)
    {
        if (input < min)
        {
            return max - (min - input) % (max - min);
        }
        else
        {
            return min + (input - min) % (max - min);
        }
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