using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

public class MultiplayerMenu : MonoBehaviour
{
    [Header("References")]
    public Button hostButton;
    public Button joinButton;

    [Tooltip("Legacy InputField (leave null if using TMP).")]
    public InputField ipInputLegacy;

#if TMP_PRESENT
    [Tooltip("TMP_InputField (leave null if using legacy).")]
    public TMP_InputField ipInputTMP;
#endif

    [Header("Options")]
    public bool prefillLocalhost = true;
    public string defaultIP = "127.0.0.1";

    [Header("Debug")]
    [SerializeField] private string lastAttemptedIP;
    [SerializeField] private bool usingTMP;
    [SerializeField] private bool usingLegacy;

    private void Awake()
    {
        DetectFieldMode();
        PrefillIfNeeded();
        WireButtons();
    }

    private void DetectFieldMode()
    {
#if TMP_PRESENT
        usingTMP = ipInputTMP != null;
#endif
        usingLegacy = ipInputLegacy != null;

        if (usingTMP && usingLegacy)
        {
            Debug.LogWarning("[MultiplayerMenu] Both TMP and Legacy inputs assigned. Will prefer TMP.");
        }
        if (!usingTMP && !usingLegacy)
        {
            Debug.LogError("[MultiplayerMenu] No input field assigned. Assign either a legacy InputField or a TMP_InputField.");
        }
    }

    private void PrefillIfNeeded()
    {
        if (!prefillLocalhost) return;

#if TMP_PRESENT
        if (usingTMP && ipInputTMP != null)
        {
            ipInputTMP.text = defaultIP;
            return;
        }
#endif
        if (usingLegacy && ipInputLegacy != null)
        {
            ipInputLegacy.text = defaultIP;
        }
    }

    private void WireButtons()
    {
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(() =>
            {
                Debug.Log("[MultiplayerMenu] Host clicked.");
                MultiplayerManager.Instance.HostGame();
            });
        }
        else
        {
            Debug.LogError("[MultiplayerMenu] Host button not assigned.");
        }

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() =>
            {
                var ip = GetCurrentIPText();
                if (string.IsNullOrWhiteSpace(ip))
                {
                    Debug.LogWarning("[MultiplayerMenu] IP field empty. Aborting join attempt.");
                    return;
                }
                lastAttemptedIP = ip;
                Debug.Log("[MultiplayerMenu] Join clicked. Using IP: " + ip);
                MultiplayerManager.Instance.JoinGame(ip);
            });
        }
        else
        {
            Debug.LogError("[MultiplayerMenu] Join button not assigned.");
        }
    }

    private string GetCurrentIPText()
    {
#if TMP_PRESENT
        if (usingTMP && ipInputTMP != null)
            return ipInputTMP.text.Trim();
#endif
        if (usingLegacy && ipInputLegacy != null)
            return ipInputLegacy.text.Trim();
        return "";
    }

    // Optional helper if you want to expose manual retries from other scripts
    public void AttemptJoin()
    {
        if (joinButton != null)
            joinButton.onClick.Invoke();
    }
}