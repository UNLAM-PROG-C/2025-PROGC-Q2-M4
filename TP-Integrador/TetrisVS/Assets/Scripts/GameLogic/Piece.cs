using UnityEngine;

public class Piece : MonoBehaviour
{
    public Board board { get; private set; }
    public TetrisBlockShapeData TBSData { get; private set; }
    public Vector3Int position { get; private set; } //this is used for tilemaps, tilemaps use Vector3Ints instead of Vector2Ints
    public Vector3Int[] cells { get; private set; } //variable to handle piece rotations
    public int rotationIndex { get; private set; } //variable to handle piece rotations
    public float stepDelay; //time delay for piece to move down automatically
    public float lockDelay; //time delay before piece locks in place after reaching the bottom

    private float stepTime;
    private float lockTime;


    public void Initialize(Board board, Vector3Int V3Position, TetrisBlockShapeData TBSData)
    {
        this.board = board;
        this.position = V3Position;
        this.TBSData = TBSData;
        this.rotationIndex = 0;
        this.stepTime = Time.time + this.stepDelay;
        this.lockTime = 0f;

        if (this.cells == null)
        {
            this.cells = new Vector3Int[this.TBSData.cells.Length];
        }

        for (int i = 0; i < this.TBSData.cells.Length; i++)
        {
            this.cells[i] = (Vector3Int)this.TBSData.cells[i];
        }

    }


    public void Update() //this method is called every frame that unity renders
    {
        this.board.Clear(this);

        this.lockTime += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.A))
        {
            MovePiece(Vector2Int.left);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            MovePiece(Vector2Int.right);
        }

            if (Input.GetKeyDown(KeyCode.W))
            {
                this.board.SpawnPiece();
                return;
            }


        if (Input.GetKeyDown(KeyCode.S))
        {
            MovePiece(Vector2Int.down);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            InstantDropToTheBottom();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotatePiece(-1);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotatePiece(1);
        }

        if (Time.time >= this.stepTime)
        {
            Step();
        }

        this.board.Set(this);

    }

    public void Step()
    {
        this.stepTime = Time.time + this.stepDelay;

        MovePiece(Vector2Int.down);


        //eventually lock the piece if it can't move down anymore
        if (this.lockTime >= this.lockDelay)
        {
            Lock();
        }
    }

    public void Lock()
    {
        this.board.Set(this);
        this.board.ClearLines();
        this.board.SpawnPiece();
    }

    public bool MovePiece(Vector2Int translation)
    {
        Vector3Int newPosition = this.position;
        newPosition.x += translation.x;
        newPosition.y += translation.y;

        bool isValidPosition = this.board.IsValidPosition(this, newPosition);

        if (isValidPosition)
        {
            this.position = newPosition;
            this.lockTime = 0f;
        }

        return isValidPosition;

    }

    public void InstantDropToTheBottom()
    {
        while (MovePiece(Vector2Int.down))
        {
            continue;
        }

        Lock();
    }

    public void RotatePiece(int direction)
    {
        int originalRotation = this.rotationIndex;
        Vector3Int[] originalCells = (Vector3Int[])this.cells.Clone();

        this.rotationIndex = Wrap(this.rotationIndex + direction, 0, 4);
        ApplyRotationMatrix(direction);

        // Verifica si la nueva rotación está dentro de los límites y es válida
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
            // revertir rotación y celdas si no es válida
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

    void Awake()
    {
        stepDelay = 1f / PlayerPrefs.GetInt("DifficultyLevel", 1);
        lockDelay = 0.5f / PlayerPrefs.GetInt("DifficultyLevel", 1);
    }

}


