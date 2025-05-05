using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    [Header("Countdown")]
    public TMP_Text countdownText;

    [Header("Winner")]
    public GameObject winnerPanel;
    public TMP_Text   winnerText;

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
        if (winnerText) winnerText.text = $"{name} WINS!";
        if (winnerPanel) winnerPanel.SetActive(true);
    }
}
 