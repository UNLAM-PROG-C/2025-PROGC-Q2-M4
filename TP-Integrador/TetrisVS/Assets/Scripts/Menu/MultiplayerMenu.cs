using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerMenu : MonoBehaviour
{
  // Defaults and constants
  private const string DefaultLocalhostIP = "127.0.0.1";
  private const string EmptyIPWarning = "IP vacía.";
  private const int DefaultTetrisSceneIndex = 0;

  public Button hostButton;
  public Button joinButton;
  public InputField ipInput;
  public bool prefillLocalhost = true;
  public string defaultIP = DefaultLocalhostIP;
  public int tetrisSceneIndex = DefaultTetrisSceneIndex;

  private void Start()
  {
    if (prefillLocalhost && ipInput != null)
    {
      ipInput.text = defaultIP;
    }

    if (hostButton != null)
    {
      hostButton.onClick.AddListener(() =>
      {
        EnsureManager();
        MultiplayerManager.Instance.HostGame();
        SceneManager.LoadScene(tetrisSceneIndex);
      });
    }

    if (joinButton != null)
    {
      joinButton.onClick.AddListener(() =>
      {
        string ip = ipInput != null ? ipInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(ip))
        {
          Debug.LogWarning(EmptyIPWarning);
          return;
        }

        EnsureManager();
        MultiplayerManager.Instance.JoinGame(ip);
        SceneManager.LoadScene(tetrisSceneIndex);
      });
    }
  }

  private void EnsureManager()
  {
    if (MultiplayerManager.Instance == null)
    {
      GameObject go = new GameObject("MultiplayerManager");
      go.AddComponent<MultiplayerManager>();
    }
  }
}