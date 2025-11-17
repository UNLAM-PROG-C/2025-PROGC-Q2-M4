using UnityEngine;
using UnityEngine.UI;

public class Score : MonoBehaviour
{
    public Text scoreText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateScore(int score)
    {
        Debug.Log($"Updating score display to: {score}");
        scoreText.text = score.ToString(); // Update the UI in text field with the new score
        Debug.Log("Score display updated.");
    }

}
