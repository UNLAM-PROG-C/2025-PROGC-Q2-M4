using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenu : MonoBehaviour
{
  // Scene index constants (replace magic numbers)
  private const int SceneIndexMainMenu = 0;
  private const int SceneIndexSinglePlayer = 1;
  private const int SceneIndexSettings = 2;
  private const int SceneIndexMultiplayerMenu = 3;
  private const int SceneIndexMultiplayerGame = 4;

  public static void LoadSinglePlayerGame()
  {
    SceneManager.LoadScene(SceneIndexSinglePlayer);
  }

  public static void ExitGame()
  {
#if UNITY_EDITOR
    EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
  }

  public static void LoadSettingsMenu()
  {
    SceneManager.LoadScene(SceneIndexSettings);
  }

  public static void LoadMainMenu()
  {
    SceneManager.LoadScene(SceneIndexMainMenu);
  }

  public static void LoadMultiplayerMenu()
  {
    SceneManager.LoadScene(SceneIndexMultiplayerMenu);
  }

  public static void LoadMultiplayerGame()
  {
    SceneManager.LoadScene(SceneIndexMultiplayerGame);
  }
}