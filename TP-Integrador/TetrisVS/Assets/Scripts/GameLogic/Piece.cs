// NOTE: This is an adapted version; integrate into your existing Piece.cs content.
// Add or merge only the shown changes if your file contains more logic.

using UnityEngine;

public class Piece : MonoBehaviour
{
    public Board board;

    // If originally private set, change to public set OR keep private and use ApplyNetworkState.
    public Vector3Int position { get; set; }

    public Vector3Int[] cells;
    public TetrisBlockShapeData TBSData;

    public float stepDelay = 0.5f;
    public float lockDelay = 0.5f;

    private float stepTime;
    private float lockTime;
        
    void Awake()
    {
        if (cells == null || cells.Length != 4)
            cells = new Vector3Int[4];
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
        // Prevent client-side piece control in multiplayer (host authoritative).
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsClient)
            return;

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
}