using UnityEngine;
using UnityEngine.SceneManagement;

public class Piece : MonoBehaviour
{
    public Board board;
    public int rotationIndex { get; private set; }
    // If originally private set, change to public set OR keep private and use ApplyNetworkState.
    public Vector3Int position { get; set; }

    public Vector3Int[] cells;
    public TetrisBlockShapeData TBSData;

    public float stepDelay = 1f;
    public float lockDelay = 0.001f;

    private float stepTime;
    private float lockTime;
        
    void Awake()
    {
        if (cells == null || cells.Length != 4)
        {
            cells = new Vector3Int[4];
        }

        rotationIndex = 0;

        stepDelay = stepDelay / (PlayerPrefs.GetInt("DifficultyLevel", 2) - 1);
        lockDelay = lockDelay / (PlayerPrefs.GetInt("DifficultyLevel", 2) - 1);
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
    }

    void Update()
    {
        HandleInputLocal();
        HandleGravity();
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
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            //used to be a cheat button XD
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            HardDrop();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            board.multiplayerManager.LeaveGame();
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
            
            // Trigger network update for piece movement
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
        // Trigger network update for piece placement
        MultiplayerManager.Instance?.NotifyPiecePlaced();
        
        board.ClearLines();
        board.SpawnPiece();
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

    public void RotatePiece(int direction)
    {
        // O-piece (square) does not rotate in SRS; treat as no-op.
        if (TBSData.Shape == eTetrisBlockShapes.O)
        {
            return;
        }

        board.Clear(this);

        int originalRotation = rotationIndex;
        Vector3Int[] originalCells = (Vector3Int[])cells.Clone();
        Vector3Int originalPosition = position;

        rotationIndex = Wrap(rotationIndex + direction, 0, 4);
        RotateCells(direction);

        bool rotationSucceeded = true;

        if (!board.IsValidPosition(this, position))
        {
            rotationSucceeded = false;

            // Wall kicks according to SRS arrays
            Vector2Int[,] wallKicks = Data.WallKicks[TBSData.Shape];
            // For each set of kicks we choose the correct row pair: originalRotation and direction.
            int kickIndexBase = originalRotation * 2 + (direction > 0 ? 0 : 1);

            for (int i = 0; i < wallKicks.GetLength(1); i++)
            {
                Vector2Int translation = wallKicks[kickIndexBase, i];
                position = originalPosition + new Vector3Int(translation.x, translation.y, 0);

                if (board.IsValidPosition(this, position))
                {
                    rotationSucceeded = true;
                    break;
                }
            }
        }

        if (!rotationSucceeded)
        {
            // Revert
            rotationIndex = originalRotation;
            cells = originalCells;
            position = originalPosition;
        }
        else
        {
            // Successful rotation resets lock timer to give player time.
            lockTime = 0f;
            
            // Trigger network update for piece rotation
            MultiplayerManager.Instance?.NotifyPieceRotated();
        }

        board.Set(this);
    }

    private void RotateCells(int direction)
    {
        // Integer 90° rotations:
        // CW: (x,y) -> ( y, -x )
        // CCW: (x,y) -> (-y,  x )
        for (int i = 0; i < cells.Length; i++)
        {
            Vector3Int c = cells[i];
            Vector3Int rotated = direction > 0
                ? new Vector3Int(c.y, -c.x, 0)
                : new Vector3Int(-c.y, c.x, 0);
            cells[i] = rotated;
        }
    }

    private int Wrap(int input, int min, int max)
    {
        // Proper modulo wrap: ensures result in [min, max)
        int range = max - min;
        if (range <= 0) return min;
        int value = (input - min) % range;
        if (value < 0) value += range;
        return min + value;
    }
}