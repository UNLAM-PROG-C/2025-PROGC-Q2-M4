using UnityEngine;
using UnityEngine.Tilemaps;
public class GhostPiece : MonoBehaviour
{
    public Tile tile;
    public Board board;
    public Piece trackedPiece;
    public Tilemap tilemap { get; private set; }
    public Vector3Int position { get; private set; } //this is used for tilemaps, tilemaps use Vector3Ints instead of Vector2Ints
    public Vector3Int[] cells { get; private set; } //variable to handle piece rotations

    private void Awake()
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.cells = new Vector3Int[4];
    }

    private void LateUpdate()
    {
        Clear();
        Copy();
        Drop();
        Set();
    }

    private void Clear()
    {
        for (int i = 0; i < this.cells.Length; i++)
        {
            Vector3Int tilePosition = this.cells[i] + this.position; //set the piece to their default piece value + the new coordinate on the board
            this.tilemap.SetTile(tilePosition, null);
        }
    }

    private void Copy()
    {         
        for (int i = 0; i < this.cells.Length; i++)
        {
            this.cells[i] = this.trackedPiece.cells[i];
        }
    }

    private void Drop()
    {
        Vector3Int position = this.trackedPiece.position;

        int current = position.y;
        int bottom = -this.board.boardBoundsSize.y / 2 - 1;

        this.board.Clear(this.trackedPiece); //clear the tracked piece from the board to avoid collision with itself

        for (int row = current; row >= bottom; row--)
        {
            position.y = row;

            if(this.board.IsValidPosition(this.trackedPiece, position))
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
        for (int i = 0; i < this.cells.Length; i++)
        {
            Vector3Int tilePosition = this.cells[i] + this.position; //set the piece to their default piece value + the new coordinate on the board
            this.tilemap.SetTile(tilePosition, this.tile);
        }
    }

}
