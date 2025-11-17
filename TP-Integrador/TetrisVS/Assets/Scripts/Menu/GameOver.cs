using UnityEngine;
using UnityEngine.SceneManagement;
public class GameOver : MonoBehaviour
{
    public string gameplaySceneName = "SceneSinglePlayerTetris"; 
    public string menuSceneName = "SceneMainMenuScreen";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
