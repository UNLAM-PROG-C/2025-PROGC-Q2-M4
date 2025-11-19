using UnityEngine;
using UnityEngine.Tilemaps;

public class QueueRenderer : MonoBehaviour
{
  private const int DefaultQueueRenderSize = 5;
  private const int QueueVerticalSpacing = 4;
  private const int SpawnYOffset = 1;

  public SharedShapesQueue shapesQueue;
  public Tilemap tilemap { get; private set; }
  public TetrisBlockShapeData[] TetrisBlocks;
  public Vector3Int spawnPos;
  public Vector2Int boardBoundsSize = new Vector2Int(4, 20);
  public int queueRenderSize = DefaultQueueRenderSize;

  private bool isInitialized;
  private Board board;

  private void Awake()
  {
    tilemap = GetComponentInChildren<Tilemap>();
    board = FindObjectOfType<Board>();

    if (TetrisBlocks == null || TetrisBlocks.Length == 0)
    {
      Debug.LogError("QueueRenderer: TetrisBlocks array is null or empty.");
      return;
    }

    for (int i = 0; i < TetrisBlocks.Length; i++)
    {
      if (TetrisBlocks[i].tile != null)
      {
        TetrisBlocks[i].Initialize();
      }
      else
      {
        Debug.LogError($"QueueRenderer: TetrisBlocks[{i}].tile is NULL!");
      }
    }

    isInitialized = true;
  }

  private void Start()
  {
    if (board != null && board.shapesQueue != null)
    {
      shapesQueue = board.shapesQueue;
    }
  }

  private void Update()
  {
    if (isInitialized && shapesQueue != null)
    {
      RenderQueue();
    }
  }

  public void RenderQueue()
  {
    if (!isInitialized ||
        tilemap == null ||
        TetrisBlocks == null ||
        TetrisBlocks.Length == 0 ||
        queueRenderSize <= 0 ||
        shapesQueue == null)
    {
      return;
    }

    tilemap.ClearAllTiles();

    for (int y = 0; y < queueRenderSize; y++)
    {
      int shapeIndex = shapesQueue.PeekShape(y);
      if (shapeIndex >= 0 && shapeIndex < TetrisBlocks.Length)
      {
        RenderPieceInQueue(y, shapeIndex);
      }
    }
  }

  private void RenderPieceInQueue(int queueIndex, int shapeIndex)
  {
    TetrisBlockShapeData blockData = TetrisBlocks[shapeIndex];

    if (blockData.tile == null || blockData.cells == null)
    {
      return;
    }

    for (int i = 0; i < blockData.cells.Length; i++)
    {
      Vector3Int tilePosition =
        (Vector3Int)blockData.cells[i] +
        new Vector3Int(spawnPos.x, spawnPos.y + SpawnYOffset + queueIndex * QueueVerticalSpacing, spawnPos.z);

      tilemap.SetTile(tilePosition, blockData.tile);
    }
  }

  public void RefreshQueue()
  {
    if (isInitialized)
    {
      RenderQueue();
    }
  }

  public void SetShapesQueue(SharedShapesQueue newQueue)
  {
    if (newQueue != null)
    {
      shapesQueue = newQueue;
      RefreshQueue();
    }
  }
}