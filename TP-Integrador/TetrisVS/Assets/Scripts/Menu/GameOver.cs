using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
public class GameOver : MonoBehaviour
{
    public string gameplaySceneName = "SceneSinglePlayerTetris"; 
    public string menuSceneName = "SceneMainMenuScreen";
    public AudioSource audioSource;
    public AudioClip gameOverClip;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        gameOverClip = Resources.Load<AudioClip>("game_over");
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = false; 
        }
        audioSource.PlayOneShot(gameOverClip);
        yield return new WaitForSeconds(gameOverClip.length);
    }

    // Update is called once per frame
    void Update()
    {
        
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
