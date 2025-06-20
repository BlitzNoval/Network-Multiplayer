using Mirror;
using UnityEngine;
using TMPro;
using System.Collections;

public class GameUI : MonoBehaviour
{
    [Header("Countdown")]
    public TMP_Text countdownText;

    [Header("Winner")]
    public GameObject winnerPanel;
    public TMP_Text winnerText;

    [Header("Ping")]
    public TMP_Text pingText;
    [SerializeField] float pingUpdateInterval = 1f;

    [Header("Pause")]
    public GameObject pauseMenuPanel;
    public GameObject pausedNotificationPanel;

    [Header("Bomb Timer")]
    public GameObject timerPanel;
    public TMP_Text timerText;

    void Awake()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterUI(this);
    }

    void Start()
    {
        if (NetworkClient.isConnected)
        {
            StartCoroutine(UpdatePing());
        }
    }

    // Subscribe to pause events for reactive UI updates
    void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IsPausedChanged += OnPauseStateChanged;
            GameManager.Instance.PauserChanged += OnPauserChanged;
        }
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IsPausedChanged -= OnPauseStateChanged;
            GameManager.Instance.PauserChanged -= OnPauserChanged;
        }
    }

    // Remove Update() logic since events handle UI state
    public void ShowCountdown(string t)
    {
        if (!countdownText) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = t;
    }

    public void HideCountdown()
    {
        if (countdownText) countdownText.gameObject.SetActive(false);
    }

    public void ShowWinner(string name)
    {
        if (winnerText)
        {
            winnerText.text = $"{name} WINS!";
        }
        if (winnerPanel)
        {
            winnerPanel.SetActive(true);
        }
    }

    public void UpdateBombTimer(int seconds)
    {
        if (timerPanel != null)
        {
            timerPanel.SetActive(true);
        }
        if (timerText != null)
        {
            timerText.text = seconds.ToString();
        }
    }

    public void HideBombTimer()
    {
        if (timerPanel != null)
        {
            timerPanel.SetActive(false);
        }
    }

    private IEnumerator UpdatePing()
    {
        while (NetworkClient.isConnected)
        {
            if (pingText != null)
            {
                float rtt = (float)NetworkTime.rtt * 1000f;
                pingText.text = $"Ping: {Mathf.RoundToInt(rtt)} ms";
            }
            yield return new WaitForSeconds(pingUpdateInterval);
        }
    }

    public void OnResumeButtonClicked()
    {
        Debug.Log("Resume button clicked");
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerMovement>();
        if (localPlayer != null)
        {
            localPlayer.CmdResumeGame();
        }
        else
        {
            Debug.LogWarning("Local player not found or lacks PlayerMovement component");
        }
    }

    public void OnUnpauseButtonClicked()
    {
        Debug.Log("Unpause button clicked");
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerMovement>();
        if (localPlayer != null)
        {
            localPlayer.CmdResumeGame();
        }
        else
        {
            Debug.LogWarning("Local player not found or lacks PlayerMovement component");
        }
    }

    public void OnLeaveButtonClicked()
    {
        Debug.Log("Leave button clicked");
        if (NetworkServer.active)
        {
            Debug.Log("Host is leaving, attempting host migration");
            HostMigrationManager.ElectAndNotify();
            NetworkManager.singleton.StopHost();
        }
        else
        {
            Debug.Log("Client is leaving");
            NetworkManager.singleton.StopClient();
        }
    }

    void OnPauseStateChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            if (GameManager.Instance.Pauser != null && GameManager.Instance.Pauser.isLocalPlayer)
            {
                if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
                if (pausedNotificationPanel != null) pausedNotificationPanel.SetActive(false);
            }
            else
            {
                if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
                if (pausedNotificationPanel != null) pausedNotificationPanel.SetActive(true);
            }
        }
        else
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (pausedNotificationPanel != null) pausedNotificationPanel.SetActive(false);
        }
    }

    void OnPauserChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
    {
        OnPauseStateChanged(false, GameManager.Instance.IsPaused);
    }
}