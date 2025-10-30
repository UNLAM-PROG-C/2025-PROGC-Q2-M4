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
            // Rotation logic would go here
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
            RotatePiece(1);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotatePiece(-1);
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
        board.ClearLines();
        board.SpawnPiece();
    }

    // Network application method used by BoardMultiplayerAdapter for client sync.
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
        int originalRotation = this.rotationIndex;
        Vector3Int[] originalCells = (Vector3Int[])this.cells.Clone();

        this.rotationIndex = Wrap(this.rotationIndex + direction, 0, 4);
        ApplyRotationMatrix(direction);

        if (!this.board.IsValidPosition(this, this.position))
        {
            // wall kicks
            Vector2Int[,] wallKicks = Data.WallKicks[this.TBSData.Shape];
            for (int i = 0; i < wallKicks.GetLength(1); i++)
            {
                Vector2Int translation = wallKicks[originalRotation * 2 + (direction > 0 ? 0 : 1), i];
                Vector3Int testPosition = this.position + new Vector3Int(translation.x, translation.y, 0);

                if (this.board.IsValidPosition(this, testPosition))
                {
                    this.position = testPosition;
                    return;
                }
            }
            this.rotationIndex = originalRotation;
            this.cells = originalCells;
        }
    }

    private void ApplyRotationMatrix(int direction)
    {
        float cos = Data.RotationMatrix[0];
        float sin = Data.RotationMatrix[1];
        if (direction < 0)
        {
            sin = -sin;
        }
        for (int i = 0; i < this.cells.Length; i++)
        {
            Vector3Int cell = this.cells[i];
            int x = Mathf.RoundToInt(cos * cell.x - sin * cell.y);
            int y = Mathf.RoundToInt(sin * cell.x + cos * cell.y);
            this.cells[i] = new Vector3Int(x, y, 0);
        }


    }

    private int Wrap(int input, int min, int max)
    {
        if (input < min)
        {
            return max - (min - input) % (max - min);
        }
        else
        {
            return input;
        }
    }



}