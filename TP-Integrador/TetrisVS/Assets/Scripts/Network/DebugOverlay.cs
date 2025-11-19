using UnityEngine;

/// <summary>
/// Simple debug overlay for multiplayer state.
/// </summary>
public class DebugOverlay : MonoBehaviour
{
  private const float DefaultAreaX = 10f;
  private const float DefaultAreaY = 10f;
  private const float DefaultAreaWidth = 220f;
  private const float DefaultAreaHeight = 80f;

  public static float LastRemoteUpdateTime;

  private void OnGUI()
  {
    if (MultiplayerManager.Instance == null) return;

    string role = MultiplayerManager.Instance.IsServer
      ? "HOST"
      : MultiplayerManager.Instance.IsClient
        ? "CLIENT"
        : "NONE";

    GUILayout.BeginArea(new Rect(DefaultAreaX, DefaultAreaY, DefaultAreaWidth, DefaultAreaHeight), GUI.skin.box);
    GUILayout.Label($"Role: {role}");
    GUILayout.Label($"Last remote update: {LastRemoteUpdateTime:F2}");
    GUILayout.EndArea();
  }
}