using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIPanel : MonoBehaviour
{
    [SerializeField] Image heart1, heart2, heart3;
    [SerializeField] TextMeshProUGUI playerLabel;
    [SerializeField] Image colorPanel;
    [SerializeField] TextMeshProUGUI percentageText;

    readonly Color[] playerColors = new Color[]
    {
        new Color(0.208f, 0.380f, 0.702f), 
        new Color(0.706f, 0.196f, 0.224f), 
        new Color(0.275f, 0.663f, 0.298f), 
        new Color(0.965f, 0.580f, 0.098f)  
    };

    public void Initialize(int playerNumber)
    {
        playerLabel.text = $"P{playerNumber}";
        if (playerNumber >= 1 && playerNumber <= playerColors.Length)
            colorPanel.color = playerColors[playerNumber - 1];
        else
        {
            colorPanel.color = Color.gray;
            Debug.LogWarning($"Invalid playerNumber {playerNumber} for color assignment.");
        }
    }

    public void SetLives(int oldLives, int newLives)
    {
        if (newLives < oldLives && AudioManager.Instance != null)
            AudioManager.Instance.PlayLifeLostSound(newLives);

        heart1.enabled = newLives >= 1;
        heart2.enabled = newLives >= 2;
        heart3.enabled = newLives >= 3;
    }

    public void SetKnockback(float oldPercentage, float newPercentage)
    {
        // The value passed in is already the percentage (0-350), not a multiplier
        int percentage = Mathf.Clamp(Mathf.RoundToInt(newPercentage), 0, 350);
        percentageText.text = $"{percentage}%";
    }
}