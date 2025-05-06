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

    private readonly Color[] playerColors = new Color[]
    {
        new Color(0.208f, 0.380f, 0.702f), // Blue (#3561B3) for ID 0
        new Color(0.706f, 0.196f, 0.224f), // Red (#B43239) for ID 1
        new Color(0.275f, 0.663f, 0.298f), // Green (#46A94C) for ID 2
        new Color(0.965f, 0.580f, 0.098f)  // Orange (#F69419) for ID 3
    };

    private int currentLives = 0; // Track current lives for animation

    public void Initialize(int playerID)
    {
        // Set player label (e.g., P1, P2)
        playerLabel.text = $"P{playerID + 1}";

        // Set color panel based on player ID
        if (playerID >= 0 && playerID < playerColors.Length)
        {
            colorPanel.color = playerColors[playerID];
        }
        else
        {
            Debug.LogWarning($"Invalid player ID {playerID} for color assignment.");
            colorPanel.color = Color.gray; // Fallback color
        }

        // Initialize percentage text to 0% (for future use)
        percentageText.text = "0%";
    }

    public void SetLives(int lives)
    {
        // Trigger animation for the heart being lost
        if (lives < currentLives)
        {
            int heartToAnimate = currentLives; // Heart to animate is based on previous lives
            AnimateHeartOut(heartToAnimate);
        }

        // Update heart visibility
        heart1.enabled = lives >= 1;
        heart2.enabled = lives >= 2;
        heart3.enabled = lives >= 3;

        // Update currentLives
        currentLives = lives;
    }

    private void AnimateHeartOut(int heartNumber)
    {
        Animator animator = null;
        switch (heartNumber)
        {
            case 1:
                animator = heart1.GetComponent<Animator>();
                break;
            case 2:
                animator = heart2.GetComponent<Animator>();
                break;
            case 3:
                animator = heart3.GetComponent<Animator>();
                break;
            default:
                Debug.LogWarning("Invalid heart number for animation.");
                break;
        }
        if (animator != null)
        {
            animator.SetTrigger("Lose"); // Trigger the "Lose" animation
        }
    }

    public void SetPercentage(int percentage)
    {
        // Update percentage text (for future functionality)
        percentageText.text = $"{percentage}%";
    }
}