using UnityEngine;
using UnityEngine.UI;

public class Score : MonoBehaviour
{
  public Text scoreText;

  public void UpdateScore(int score)
  {
    if (scoreText != null)
    {
      scoreText.text = score.ToString();
    }
  }
}