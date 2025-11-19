using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver : MonoBehaviour
{
  // Constants
  private const string DefaultGameplaySceneName = "SceneSinglePlayerTetris";
  private const string DefaultMenuSceneName = "SceneMainMenuScreen";
  private const string GameOverClipResourceName = "game_over";

  public string gameplaySceneName = DefaultGameplaySceneName;
  public string menuSceneName = DefaultMenuSceneName;

  public AudioSource audioSource;
  public AudioClip gameOverClip;

  private IEnumerator Start()
  {
    gameOverClip = Resources.Load<AudioClip>(GameOverClipResourceName);

    if (audioSource == null)
    {
      audioSource = gameObject.AddComponent<AudioSource>();
      audioSource.loop = false;
    }

    if (gameOverClip != null)
    {
      audioSource.PlayOneShot(gameOverClip);
      yield return new WaitForSeconds(gameOverClip.length);
    }
  }

  public void OnRetryButton()
  {
    SceneManager.LoadScene(gameplaySceneName);
  }

  public void OnMenuButton()
  {
    SceneManager.LoadScene(menuSceneName);
  }
}