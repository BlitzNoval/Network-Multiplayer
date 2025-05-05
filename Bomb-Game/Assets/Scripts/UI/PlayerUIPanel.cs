using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIPanel : MonoBehaviour
{
    public Image heart1;
    public Image heart2;
    public Image heart3;
    public TextMeshProUGUI playerLabel;
    public Image colorPanel;
    public TextMeshProUGUI percentageText;

    public AudioClip heartLoseSound;   // 👈 Add this
    private AudioSource audioSource;   // 👈 And this

    private readonly Color[] playerColors = new Color[]
    {
        new Color(0.208f, 0.380f, 0.702f), // Blue (#3561B3)
        new Color(0.706f, 0.196f, 0.224f), // Red (#B43239)
        new Color(0.275f, 0.663f, 0.298f), // Green (#46A94C)
        new Color(0.965f, 0.580f, 0.098f)  // Orange (#F69419)
    };

    private int currentLives = 0;

    public void Initialize(int playerID)
    {
        playerLabel.text = $"P{playerID + 1}";

        if (playerID >= 0 && playerID < playerColors.Length)
        {
            colorPanel.color = playerColors[playerID];
        }
        else
        {
            Debug.LogWarning($"Invalid player ID {playerID} for color assignment.");
            colorPanel.color = Color.gray;
        }

        percentageText.text = "0%";

        // Get the AudioSource component
        audioSource = GetComponent<AudioSource>();
    }

    public void SetLives(int lives)
    {
        if (lives < currentLives)
        {
            PlayHeartLoseSound();  // 👈 Play sound instead of animation
        }

        heart1.enabled = lives >= 1;
        heart2.enabled = lives >= 2;
        heart3.enabled = lives >= 3;

        currentLives = lives;
    }

    private void PlayHeartLoseSound()
    {
        if (heartLoseSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(heartLoseSound);
        }
    }

    public void SetPercentage(int percentage)
    {
        percentageText.text = $"{percentage}%";
    }
}
