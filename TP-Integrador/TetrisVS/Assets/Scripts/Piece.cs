using UnityEngine;

public class Piece : MonoBehaviour
{
    public Board board { get; private set; }
    public TetrisBlockShapeData TBSData { get; private set; }
    public Vector3Int position { get; private set; } //this is used for tilemaps, tilemaps use Vector3Ints instead of Vector2Ints
    public Vector3Int[] cells {get; private set;} //variable to handle piece rotations

    public void Initialize(Board board, Vector3Int V3Position, TetrisBlockShapeData TBSData)
    {
        this.board = board;
        this.position = V3Position;
        this.TBSData = TBSData;

        if(this.cells == null)
        {
            this.cells = new Vector3Int[this.TBSData.cells.Length];
        }

        for (int i = 0; i < this.TBSData.cells.Length; i++) {
            this.cells[i] = (Vector3Int)this.TBSData.cells[i];
        }

    }
}
