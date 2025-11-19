using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
// Multiplayer menu logic for hosting or joining a game
public class MultiplayerMenu : MonoBehaviour
{
  public Button hostButton;
  public Button joinButton;
  public InputField ipInput;
  public bool prefillLocalhost = true;
  public string defaultIP = "127.0.0.1";
  public int tetrisSceneIndex = 0;

  private void Start()
  { 
    if (prefillLocalhost && ipInput != null)
      ipInput.text = defaultIP; // Pre-fill with localhost IP

    if (hostButton != null) 
      hostButton.onClick.AddListener(() =>
      { // Host a new multiplayer game
        EnsureManager();
        MultiplayerManager.Instance.HostGame(); 
        SceneManager.LoadScene(tetrisSceneIndex);
      });

    if (joinButton != null)
      joinButton.onClick.AddListener(() =>
      { // Join an existing multiplayer game
        var ip = ipInput != null ? ipInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(ip))
        {
          Debug.LogWarning("IP vacía.");
          return;
        }
        EnsureManager();
        MultiplayerManager.Instance.JoinGame(ip);
        SceneManager.LoadScene(tetrisSceneIndex);
      });
  }

  // Ensure the MultiplayerManager instance exists
  private void EnsureManager()
  {
    if (MultiplayerManager.Instance == null)
    {
      var go = new GameObject("MultiplayerManager");
      go.AddComponent<MultiplayerManager>(); // Unity native function
    }
  }
}