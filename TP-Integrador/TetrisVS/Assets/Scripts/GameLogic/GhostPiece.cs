using UnityEngine;
using UnityEngine.Tilemaps;

public class GhostPiece : MonoBehaviour
{
    public Tile tile;
    public Board board;
    public Piece trackedPiece;
    public Tilemap tilemap { get; private set; }
    public Vector3Int position { get; private set; }
    public Vector3Int[] cells { get; private set; }

    private void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.cells = new Vector3Int[4];
    }

    private void LateUpdate()
    {
        // Validate all required components before proceeding
        if (!IsValidForUpdate())
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
        // Check if all required components are properly initialized
        if (this.tilemap == null)
        {
            Debug.LogWarning("GhostPiece: Tilemap is null");
            return false;
        }

        if (this.board == null)
        {
            Debug.LogWarning("GhostPiece: Board is null");
            return false;
        }

        if (this.trackedPiece == null)
        {
            Debug.LogWarning("GhostPiece: TrackedPiece is null");
            return false;
        }

        if (this.trackedPiece.cells == null)
        {
            Debug.LogWarning("GhostPiece: TrackedPiece.cells is null");
            return false;
        }

        if (this.cells == null)
        {
            Debug.LogWarning("GhostPiece: cells array is null");
            return false;
        }

        if (this.tile == null)
        {
            Debug.LogWarning("GhostPiece: Tile is null");
            return false;
        }

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
        // Validate before copying
        if (this.trackedPiece == null || this.trackedPiece.cells == null || this.cells == null)
        {
            Debug.LogError("GhostPiece: Cannot copy - trackedPiece or cells is null");
            return;
        }

        // Ensure arrays have the same length
        if (this.trackedPiece.cells.Length != this.cells.Length)
        {
            Debug.LogError($"GhostPiece: Array length mismatch - trackedPiece.cells.Length: {this.trackedPiece.cells.Length}, cells.Length: {this.cells.Length}");
            return;
        }

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

        this.board.Clear(this.trackedPiece); // Clear the tracked piece from the board to avoid collision with itself

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

    // Public method to initialize the ghost piece (useful for multiplayer scenarios)
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

    // Method to check if the ghost piece is properly configured
    public bool IsProperlyConfigured()
    {
        return this.board != null && 
               this.trackedPiece != null && 
               this.tile != null && 
               this.tilemap != null && 
               this.cells != null;
    }
}