using UnityEngine;
using UnityEngine.Tilemaps;

public class Board : MonoBehaviour
{
    public Tilemap tilemap { get; private set; }
    public TetrisBlockShapeData[] TetrisBlocks;
    public Piece activePiece { get; private set; }
    public Vector3Int spawnPos;
    public Vector2Int boardBoundsSize = new Vector2Int(10, 20);
    public ShapesQueue shapesQueue = new ShapesQueue();
    public MultiplayerManager multiplayerManager;

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

        // Debug the TetrisBlocks array
        Debug.Log($"Board Awake: TetrisBlocks is {(TetrisBlocks != null ? "not null" : "NULL")}");
        Debug.Log($"Board Awake: TetrisBlocks.Length = {(TetrisBlocks != null ? TetrisBlocks.Length : 0)}");

        if (TetrisBlocks != null)
        {
            for (int i = 0; i < this.TetrisBlocks.Length; i++)
            {
                // For structs, check if the tile is null instead
                if (TetrisBlocks[i].tile != null)
                {
                    this.TetrisBlocks[i].Initialize();
                }
                else
                {
                    Debug.LogError($"TetrisBlocks[{i}].tile is NULL!");
                }
            }
        }
    }

    private void Start()
    {
        SpawnPiece();
    }

    public void SpawnPiece()
    {
        int shapeIndex = shapesQueue.getShape();

        // Safety check to prevent IndexOutOfRangeException
        if (shapeIndex < 0 || shapeIndex >= TetrisBlocks.Length)
        {
            Debug.LogError($"Invalid shape index: {shapeIndex}, TetrisBlocks.Length: {TetrisBlocks.Length}");
            shapeIndex = 0; // Use first piece as fallback
        }

        TetrisBlockShapeData data = this.TetrisBlocks[shapeIndex];

        this.activePiece.Initialize(this, spawnPos, data);

        if (IsValidPosition(this.activePiece, this.spawnPos))
        {
            Set(this.activePiece);
        }
        else
        {
            GameOver();
        }
    }

    public void Set(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position; //set the piece to their default piece value + the new coordinate on the board
            this.tilemap.SetTile(tilePosition, piece.TBSData.tile);

        }
    }


    public void Clear(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position; //set the piece to their default piece value + the new coordinate on the board
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

        while (row < bounds.yMax)
        {
            if (IsLineFull(row))
            {
                LineClear(row);
            } else
            {
                row++;
            }
        }
    }

    public bool IsLineFull(int row)
    {
        RectInt bounds = this.Bounds;
        for (int col = bounds.xMin; col < bounds.xMax; col++)
        {
            Vector3Int position = new Vector3Int(col, row, 0);

            if(!this.tilemap.HasTile(position))
            {
                return false;
            }
        }

        return true;
    }

    public void LineClear(int row)
    {
        RectInt bounds = this.Bounds;

        for (int col = bounds.xMin; col < bounds.xMax; col++)
        {
            Vector3Int position = new Vector3Int(col, row, 0);

            this.tilemap.SetTile(position, null);
        }

        while (row < bounds.yMax)
        {
            for (int col = bounds.xMin; col < bounds.xMax; col++)
            {
                Vector3Int position = new Vector3Int(col, row+1, 0);
                TileBase above = this.tilemap.GetTile(position);

                position = new Vector3Int(col, row, 0);
                this.tilemap.SetTile(position, above);
            }

            row++;
        }
    }

    private void GameOver()
    {
        this.tilemap.ClearAllTiles();
    }
}
