using UnityEngine;
using UnityEngine.Tilemaps;

public class Board : MonoBehaviour
{
    public Tilemap tilemap { get; private set; }
    public TetrisBlockShapeData[] TetrisBlocks;
    public Piece activePiece { get; private set; }
    public Vector3Int spawnPos;

    private void Awake() //unity method that gets called when the component, in this case Board gets initialized
    {
        this.tilemap = GetComponentInChildren<Tilemap>();
        this.activePiece = GetComponentInChildren<Piece>();
        for (int i = 0; i < this.TetrisBlocks.Length; i++)
        {
            this.TetrisBlocks[i].Initialize();
        }
    }

    private void Start()
    {
        SpawnPiece();
    }

    public void SpawnPiece()
    {
        int random = Random.Range(0, this.TetrisBlocks.Length);
        TetrisBlockShapeData data = this.TetrisBlocks[random];

        this.activePiece.Initialize(this, spawnPos, data);
        Set(this.activePiece);
    }

    public void Set(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position; //set the piece to their default piece value + the new coordinate on the board
            this.tilemap.SetTile(tilePosition, piece.TBSData.tile);

        }
    }

}
