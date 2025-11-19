using UnityEngine;
using UnityEngine.SceneManagement;
// Manages the Tetris piece behavior including movement, rotation, and locking
public class Piece : MonoBehaviour
{
    public Board board;
    public int rotationIndex { get; private set; }
    public Vector3Int position { get; set; }

    public Vector3Int[] cells;
    public TetrisBlockShapeData TBSData;

    public AudioSource audioSource;
    private AudioClip pieceLock;

    public float stepDelay = 1f;
    public float lockDelay = 0.5f;

    private float moveCooldown = 0.05f; //Piece movement cooldown
    private float lastMoveTime = 0f; //Piece last move time

    private float stepTime;
    private float lockTime;

    // Initialize piece state and adjust delays based on difficulty
    void Awake()
    {
        pieceLock = Resources.Load<AudioClip>("piece_lock");
        SetupAudioSource();

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

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = false;
        }
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

    // read the key inputs and move the piece accordingly
    private void HandleInputLocal()
    {
        if (Time.time - lastMoveTime < moveCooldown){
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotatePiece(1);      // Clockwise
            lastMoveTime = 0;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotatePiece(-1);     // Counter-Clockwise
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

    // Move the piece down by one step and handle locking if it cannot move further
    private void StepDown()
    {
        TryMove(Vector3Int.down);
        stepTime = Time.time + stepDelay;
    }

    private bool IsDown(Vector3Int dir)
    {
        return dir == Vector3Int.down;
    }

    private void TryMove(Vector3Int direction)
    {
        board.Clear(this);
        position += direction;        
        if (board.IsValidPosition(this, position)) 
        {
            MultiplayerManager.Instance?.NotifyPieceMoved(); // Notify only on successful move
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

    // Perform a hard drop to instantly place the piece at the lowest valid position
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

    // Lock the piece in place and notify multiplayer manager if applicable
    private void Lock()
    {
        if (audioSource != null && pieceLock != null)
        {
            audioSource.PlayOneShot(pieceLock);
        }
        
        board.Clear(this);
        board.Set(this);

        MultiplayerManager.Instance?.NotifyPiecePlaced(); // Notify that the piece has been placed

        board.ClearLines();

        // Apply any pending garbage BEFORE spawning the next piece
        board.ApplyPendingGarbage();

        board.SpawnPiece();
    }

    // Rotate the piece in the specified direction and handle wall kicks
    private void RotatePiece(int direction)
    {
        board.Clear(this);
        int originalRotation = rotationIndex;
        rotationIndex = Wrap(rotationIndex + direction, 0, 4);
        ApplyRotationMatrix(direction);
        if(!TestWallKicks(rotationIndex, direction))
        {
            // Revert rotation if wall kicks fail
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

            int x, y;

            ApplyRotationOnBlock(direction, matrix, cell, out x, out y);

            cells[i] = new Vector3Int(x, y, 0);
        }
    }

    private void ApplyRotationOnBlock(int direction, float[] matrix, Vector3 cell, out int x, out int y)
    {
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
    }

    private bool TestWallKicks(int rotationIndex, int rotationDirection)
    {
        int wallKickIndex = rotationIndex;
        if (rotationDirection < 0)
        {
            wallKickIndex = Wrap(rotationIndex + 1, 0, 4);
        }
        Vector2Int[,] wallKicks = TBSData.wallKicks;

        for (int i = 0; i < wallKicks.GetLength(1); i++)
        {
            Vector2Int offset = wallKicks[wallKickIndex * 2 + (rotationDirection > 0 ? 0 : 1), i];
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
        else
        {
            return min + (input - min) % (max - min);
        }
    }

    // Apply the state received from the network to synchronize piece position and shape
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