using UnityEngine;
using UnityEngine.Tilemaps;

public class GhostPiece : MonoBehaviour
{
  private const int GhostCellsCount = 4;

  public Tile tile;
  public Board board;
  public Piece trackedPiece;
  public Tilemap tilemap { get; private set; }
  public Vector3Int position { get; private set; }
  public Vector3Int[] cells { get; private set; }

  private void Awake()
  {
    tilemap = GetComponentInChildren<Tilemap>();
    cells = new Vector3Int[GhostCellsCount];
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
    if (tilemap == null) return false;
    if (board == null) return false;
    if (trackedPiece == null) return false;
    if (trackedPiece.cells == null) return false;
    if (cells == null) return false;
    if (tile == null) return false;
    if (board.gameOver) return false;
    return true;
  }

  private void Clear()
  {
    if (cells == null || tilemap == null) return;

    for (int i = 0; i < cells.Length; i++)
    {
      Vector3Int tilePosition = cells[i] + position;
      tilemap.SetTile(tilePosition, null);
    }
  }

  private void Copy()
  {
    if (trackedPiece == null || trackedPiece.cells == null || cells == null) return;
    if (trackedPiece.cells.Length != cells.Length) return;

    for (int i = 0; i < cells.Length; i++)
    {
      cells[i] = trackedPiece.cells[i];
    }
  }

  private void Drop()
  {
    if (trackedPiece == null || board == null) return;

    Vector3Int pos = trackedPiece.position;
    int current = pos.y;
    int bottom = -board.boardBoundsSize.y / 2 - 1;

    board.Clear(trackedPiece);

    for (int row = current; row >= bottom; row--)
    {
      pos.y = row;
      if (!CheckNextPosition(pos))
      {
        break;
      }
    }

    board.Set(trackedPiece);
  }

  private bool CheckNextPosition(Vector3Int testPos)
  {
    if (board.IsValidPosition(trackedPiece, testPos))
    {
      position = testPos;
      return true;
    }
    return false;
  }

  private void Set()
  {
    if (cells == null || tilemap == null || tile == null) return;

    for (int i = 0; i < cells.Length; i++)
    {
      Vector3Int tilePosition = cells[i] + position;
      tilemap.SetTile(tilePosition, tile);
    }
  }

  public void Initialize(Board gameBoard, Piece pieceToTrack, Tile ghostTile)
  {
    board = gameBoard;
    trackedPiece = pieceToTrack;
    tile = ghostTile;

    if (cells == null)
    {
      cells = new Vector3Int[GhostCellsCount];
    }
  }

  public bool IsProperlyConfigured()
  {
    return board != null &&
           trackedPiece != null &&
           tile != null &&
           tilemap != null &&
           cells != null;
  }
}