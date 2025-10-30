using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
    public static float LastRemoteUpdateTime;

    private void OnGUI()
    {
        if (MultiplayerManager.Instance == null) return;
        string role = MultiplayerManager.Instance.IsServer ? "HOST" :
                      MultiplayerManager.Instance.IsClient ? "CLIENT" : "NONE";
        GUILayout.Label($"Role: {role}");
        GUILayout.Label($"Last remote update: {LastRemoteUpdateTime:F2}s TimeNow:{Time.time:F2}");
    }
}