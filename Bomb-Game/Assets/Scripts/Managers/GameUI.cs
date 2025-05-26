using TMPro;
using UnityEngine;
using Mirror;
using System.Collections;

public class GameUI : MonoBehaviour
{
    [Header("Countdown")]
    public TMP_Text countdownText;

    [Header("Winner")]
    public GameObject winnerPanel;
    public TMP_Text   winnerText;

    [Header("Ping")]
    public TMP_Text pingText;
    [SerializeField] float pingUpdateInterval = 1f;

    [Header("Pause")]
    public GameObject pauseMenuPanel;     
    public GameObject pausedNotificationPanel;

    void Start()
    {
        if (NetworkClient.isConnected)
        {
            StartCoroutine(UpdatePing());
        }
    }

    void Update()
    {
        if (GameManager.Instance != null)
        {
            bool isPaused = GameManager.Instance.IsPaused;
            NetworkIdentity pauser = GameManager.Instance.Pauser;
            if (isPaused)
            {
                if (pauser != null && pauser.isLocalPlayer)
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
    }

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
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerMovement>();
        if (localPlayer != null)
        {
            localPlayer.CmdResumeGame();
        }
    }

    public void OnLeaveButtonClicked()
    {
        if (NetworkServer.active)
        {
            NetworkManager.singleton.StopHost();
        }
        else
        {
            NetworkManager.singleton.StopClient();
        }
    }
}