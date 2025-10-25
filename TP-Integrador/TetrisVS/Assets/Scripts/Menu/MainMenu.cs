using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor; // Required for EditorApplication.isPlaying
public class MainMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public static void LoadSinglePlayerGame()
    {
        // Assuming the single-player game is in scene index 1
        SceneManager.LoadScene(1);
    }

    public static void ExitGame()
    {
        // If running in the Unity editor
    #if UNITY_EDITOR
                EditorApplication.isPlaying = false;
    #else
            // If running as a standalone build
            Application.Quit();
    #endif
    }
    public static void LoadSettingsMenu()
    {
        // Assuming the settings menu is in scene index 2
        SceneManager.LoadScene(2);
    }

}
