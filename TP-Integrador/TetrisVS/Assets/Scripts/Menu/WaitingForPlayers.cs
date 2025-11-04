using UnityEngine;
using UnityEngine.UI;

public class WaitingForPlayersUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject waitingPanel;
    public Text statusText;
    public Text connectedPlayersText;
    public Button cancelButton;
    
    private void Start()
    {
        // Subscribe to multiplayer events
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.OnGameStateUpdated += OnGameStateChanged;
            MultiplayerManager.OnClientConnected += OnClientConnected;
            MultiplayerManager.OnClientDisconnected += OnClientDisconnected;
        }
        
        // Setup cancel button
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
        
        UpdateUI();
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
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
        if (MultiplayerManager.Instance == null) return;
        
        bool shouldShowWaiting = MultiplayerManager.Instance.IsServer && 
                                MultiplayerManager.Instance.currentGameState == GameState.WaitingForPlayers;
        
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(shouldShowWaiting);
        }
        
        if (shouldShowWaiting)
        {
            if (statusText != null)
            {
                statusText.text = "Waiting for players to join...";
            }
            
            if (connectedPlayersText != null)
            {
                connectedPlayersText.text = $"Connected Players: {GetConnectedPlayersCount()}";
            }
        }
    }
    
    private int GetConnectedPlayersCount()
    {
        // This would need to be exposed from MultiplayerManager
        // For now, return 1 if server is running
        return MultiplayerManager.Instance.IsServer ? 1 : 0;
    }
    
    private void OnCancelClicked()
    {
        MultiplayerManager.Instance?.LeaveGame();
        // Return to main menu or previous scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}