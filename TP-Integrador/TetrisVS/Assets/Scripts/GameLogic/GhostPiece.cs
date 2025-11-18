using UnityEngine;
using UnityEngine.Tilemaps;
// Manages the shadow as a ghost piece that shows where the current piece will land
public class GhostPiece : MonoBehaviour
{
    public Tile tile;
    public Board board;
    public Piece trackedPiece;
    public Tilemap tilemap { get; private set; }
    public Vector3Int position { get; private set; }
    public Vector3Int[] cells { get; private set; }
// Initialize references and cell array
    private void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.cells = new Vector3Int[4];
    }

    private void LateUpdate()
    {
        if (!IsValidForUpdate() || board + "" == "Grid_EnemyGameBoard (Board)")
        {
            return;
        }

        Clear();
        Copy();
        Drop();
        Set();
    }

    private bool IsValidForUpdate()
    {
        if (this.tilemap == null) return false;
        if (this.board == null) return false;
        if (this.trackedPiece == null) return false;
        if (this.trackedPiece.cells == null) return false;
        if (this.cells == null) return false;
        if (this.tile == null) return false;
        if (board.gameOver) return false;
        return true;
    }

    private void Clear()
    {
        if (this.cells == null || this.tilemap == null) return;

        for (int i = 0; i < this.cells.Length; i++)
        {
            Vector3Int tilePosition = this.cells[i] + this.position;
            this.tilemap.SetTile(tilePosition, null);
        }
    }

    private void Copy()
    {
        if (this.trackedPiece == null || this.trackedPiece.cells == null || this.cells == null) return;

        if (this.trackedPiece.cells.Length != this.cells.Length) return;

        for (int i = 0; i < this.cells.Length; i++)
        {
            this.cells[i] = this.trackedPiece.cells[i];
        }
    }

    private void Drop()
    {
        if (this.trackedPiece == null || this.board == null) return;

        Vector3Int position = this.trackedPiece.position;

        int current = position.y;
        int bottom = -this.board.boardBoundsSize.y / 2 - 1;

        this.board.Clear(this.trackedPiece);

        for (int row = current; row >= bottom; row--)
        {
            position.y = row;

            if (this.board.IsValidPosition(this.trackedPiece, position))
            {
                this.position = position;
            }
            else
            {
                break;
            }
        }

        this.board.Set(this.trackedPiece);
    }

    private void Set()
    {
        if (this.cells == null || this.tilemap == null || this.tile == null) return;

        for (int i = 0; i < this.cells.Length; i++)
        {
            Vector3Int tilePosition = this.cells[i] + this.position;
            this.tilemap.SetTile(tilePosition, this.tile);
        }
    }

    public void Initialize(Board gameBoard, Piece pieceToTrack, Tile ghostTile)
    {
        this.board = gameBoard;
        this.trackedPiece = pieceToTrack;
        this.tile = ghostTile;

        if (this.cells == null)
        {
            this.cells = new Vector3Int[4];
        }
    }

    public bool IsProperlyConfigured()
    {
        return this.board != null &&
               this.trackedPiece != null &&
               this.tile != null &&
               this.tilemap != null &&
               this.cells != null;
    }
}