using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WaitingForPlayersUI : MonoBehaviour
{
  // Constants
  private const string WaitingStatusText = "Waiting for players to join...";
  private const string ConnectedPlayersPrefix = "Connected Players: ";
  private const int MainMenuSceneIndex = 0;

  [Header("UI Elements")]
  public GameObject waitingPanel;
  public Text statusText;
  public Text connectedPlayersText;
  public Button cancelButton;

  private void Start()
  {
    if (MultiplayerManager.Instance != null)
    {
      MultiplayerManager.OnGameStateUpdated += OnGameStateChanged;
      MultiplayerManager.OnClientConnected += OnClientConnected;
      MultiplayerManager.OnClientDisconnected += OnClientDisconnected;
    }

    if (cancelButton != null)
    {
      cancelButton.onClick.AddListener(OnCancelClicked);
    }

    UpdateUI();
  }

  private void OnDestroy()
  {
    if (MultiplayerManager.Instance != null)
    {
      MultiplayerManager.OnGameStateUpdated -= OnGameStateChanged;
      MultiplayerManager.OnClientConnected -= OnClientConnected;
      MultiplayerManager.OnClientDisconnected -= OnClientDisconnected;
    }
  }

  private void OnGameStateChanged(GameState newState)
  {
    UpdateUI();
  }

  private void OnClientConnected()
  {
    UpdateUI();
  }

  private void OnClientDisconnected()
  {
    UpdateUI();
  }

  private void UpdateUI()
  {
    if (MultiplayerManager.Instance == null)
    {
      return;
    }

    bool shouldShow =
      MultiplayerManager.Instance.IsServer &&
      MultiplayerManager.Instance.currentGameState == GameState.WaitingForPlayers;

    if (waitingPanel != null)
    {
      waitingPanel.SetActive(shouldShow);
    }

    if (!shouldShow)
    {
      return;
    }

    if (statusText != null)
    {
      statusText.text = WaitingStatusText;
    }

    if (connectedPlayersText != null)
    {
      connectedPlayersText.text = ConnectedPlayersPrefix + GetConnectedPlayersCount();
    }
  }

  private int GetConnectedPlayersCount()
  {
    // Placeholder: update when MultiplayerManager exposes real player count
    return MultiplayerManager.Instance.IsServer ? 1 : 0;
  }

  private void OnCancelClicked()
  {
    MultiplayerManager.Instance?.LeaveGame();
    SceneManager.LoadScene(MainMenuSceneIndex);
  }
}