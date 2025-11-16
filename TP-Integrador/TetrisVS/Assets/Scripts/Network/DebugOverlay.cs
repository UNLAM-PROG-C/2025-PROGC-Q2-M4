using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
    public static float LastRemoteUpdateTime;

    private void OnGUI()
    {// Display debug information about multiplayer status
        if (MultiplayerManager.Instance == null) return;
        string role = MultiplayerManager.Instance.IsServer ? "HOST" :
                      MultiplayerManager.Instance.IsClient ? "CLIENT" : "NONE";
        GUILayout.BeginArea(new Rect(10, 10, 220, 80), GUI.skin.box);
        GUILayout.Label($"Role: {role}");
        GUILayout.Label($"Last remote update: {LastRemoteUpdateTime:F2}");
        GUILayout.EndArea();
    }
}