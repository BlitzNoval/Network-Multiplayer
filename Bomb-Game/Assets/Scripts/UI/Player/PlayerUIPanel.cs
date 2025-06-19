using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerUIPanel : MonoBehaviour
{
    [Header("Hearts")]
    [SerializeField] Image heart1, heart2, heart3;
    
    [Header("Player Info")]
    [SerializeField] TextMeshProUGUI playerLabel;
    [SerializeField] Image colorPanel;
    
    [Header("Percentage Bar")]
    [SerializeField] Image blueBarFill;
    [SerializeField] Image yellowBarFill;
    [SerializeField] Image redBarFill;
    
    [Header("Custom Number Display")]
    [SerializeField] Image hundredsDigit;
    [SerializeField] Image tensDigit;
    [SerializeField] Image onesDigit;
    [SerializeField] Sprite[] numberSprites; // 0-9 sprites
    
    [Header("Number Scale Settings")]
    [SerializeField] float baseScale = 1f;
    [SerializeField] float maxScale = 2f;
    [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    readonly Color[] playerColors = new Color[]
    {
        new Color(0.208f, 0.380f, 0.702f), 
        new Color(0.706f, 0.196f, 0.224f), 
        new Color(0.275f, 0.663f, 0.298f), 
        new Color(0.965f, 0.580f, 0.098f)  
    };

    // Bar section thresholds (each section is 1/3 of 350)
    const float BLUE_MAX = 116.67f;   // 0-116 (blue section)
    const float YELLOW_MAX = 233.33f; // 117-233 (yellow section)
    const float RED_MAX = 350f;       // 234-350 (red section)

    public void Initialize(int playerNumber)
    {
        if (playerNumber >= 1 && playerNumber <= playerColors.Length)
            colorPanel.color = playerColors[playerNumber - 1];
        else
            colorPanel.color = Color.gray;
            
        // Initialize UI to default state
        ResetPercentageDisplay();
    }

    public void SetPlayerName(string name)
    {
        playerLabel.text = name;
    }

    public void SetLives(int oldLives, int newLives)
    {
        if (newLives < oldLives && AudioManager.Instance != null)
            AudioManager.Instance.PlayLifeLostSound(newLives);

        heart1.enabled = newLives >= 1;
        heart2.enabled = newLives >= 2;
        heart3.enabled = newLives >= 3;
        
        // Reset percentage display when player dies (loses a life)
        if (newLives < oldLives)
        {
            ResetPercentageDisplay();
        }
    }

    public void SetKnockback(float oldPercentage, float newPercentage)
    {
        int percentage = Mathf.Clamp(Mathf.RoundToInt(newPercentage), 0, 350);
        
        // Update gradient bars
        UpdateGradientBars(percentage);
        
        // Update custom number display
        UpdateNumberDisplay(percentage);
    }
    
    void UpdateGradientBars(int percentage)
    {
        float progress = percentage / 350f; // 0-1 progress across entire bar
        
        // Calculate smooth gradient transitions
        float blueFill = 0f;
        float yellowFill = 0f;
        float redFill = 0f;
        float blueAlpha = 1f;
        float yellowAlpha = 0f;
        float redAlpha = 0f;
        
        if (progress <= 0.333f) // 0-33% (0-116)
        {
            // Blue section dominates
            blueFill = progress / 0.333f;
            blueAlpha = 1f;
            yellowAlpha = 0f;
            redAlpha = 0f;
        }
        else if (progress <= 0.666f) // 33-66% (117-233)
        {
            // Transition from blue to yellow
            blueFill = 1f;
            yellowFill = (progress - 0.333f) / 0.333f;
            
            // Blend colors: blue fades out, yellow fades in
            float transitionProgress = (progress - 0.333f) / 0.333f;
            blueAlpha = Mathf.Lerp(1f, 0.3f, transitionProgress); // Blue becomes semi-transparent
            yellowAlpha = Mathf.Lerp(0f, 1f, transitionProgress); // Yellow becomes opaque
            redAlpha = 0f;
        }
        else // 66-100% (234-350)
        {
            // Transition from yellow to red
            blueFill = 1f;
            yellowFill = 1f;
            redFill = (progress - 0.666f) / 0.334f;
            
            // Blend colors: yellow fades out, red fades in
            float transitionProgress = (progress - 0.666f) / 0.334f;
            blueAlpha = 0.2f; // Keep blue subtle
            yellowAlpha = Mathf.Lerp(1f, 0.3f, transitionProgress); // Yellow becomes semi-transparent
            redAlpha = Mathf.Lerp(0f, 1f, transitionProgress); // Red becomes opaque
        }
        
        // Apply fill amounts
        if (blueBarFill != null) 
        {
            blueBarFill.fillAmount = blueFill;
            var color = blueBarFill.color;
            color.a = blueAlpha;
            blueBarFill.color = color;
        }
        
        if (yellowBarFill != null) 
        {
            yellowBarFill.fillAmount = yellowFill;
            var color = yellowBarFill.color;
            color.a = yellowAlpha;
            yellowBarFill.color = color;
        }
        
        if (redBarFill != null) 
        {
            redBarFill.fillAmount = redFill;
            var color = redBarFill.color;
            color.a = redAlpha;
            redBarFill.color = color;
        }
    }
    
    void UpdateNumberDisplay(int percentage)
    {
        if (numberSprites == null || numberSprites.Length < 10) return;
        
        // Extract digits (ensure 3-digit format: 000-350)
        int hundreds = percentage / 100;
        int tens = (percentage % 100) / 10;
        int ones = percentage % 10;
        
        // Set sprites for each digit
        if (hundredsDigit != null && hundreds < numberSprites.Length)
            hundredsDigit.sprite = numberSprites[hundreds];
            
        if (tensDigit != null && tens < numberSprites.Length)
            tensDigit.sprite = numberSprites[tens];
            
        if (onesDigit != null && ones < numberSprites.Length)
            onesDigit.sprite = numberSprites[ones];
        
        // Scale numbers based on percentage (0-350 maps to baseScale-maxScale)
        float scaleProgress = percentage / 350f;
        float currentScale = Mathf.Lerp(baseScale, maxScale, scaleCurve.Evaluate(scaleProgress));
        
        // Apply scale to all digits
        if (hundredsDigit != null) hundredsDigit.transform.localScale = Vector3.one * currentScale;
        if (tensDigit != null) tensDigit.transform.localScale = Vector3.one * currentScale;
        if (onesDigit != null) onesDigit.transform.localScale = Vector3.one * currentScale;
    }
    
    void ResetPercentageDisplay()
    {
        // Reset bars to empty and reset alpha values
        if (blueBarFill != null) 
        {
            blueBarFill.fillAmount = 0f;
            var color = blueBarFill.color;
            color.a = 1f; // Reset to full opacity
            blueBarFill.color = color;
        }
        
        if (yellowBarFill != null) 
        {
            yellowBarFill.fillAmount = 0f;
            var color = yellowBarFill.color;
            color.a = 0f; // Reset to transparent
            yellowBarFill.color = color;
        }
        
        if (redBarFill != null) 
        {
            redBarFill.fillAmount = 0f;
            var color = redBarFill.color;
            color.a = 0f; // Reset to transparent
            redBarFill.color = color;
        }
        
        // Reset numbers to 000 and base scale
        if (numberSprites != null && numberSprites.Length >= 1)
        {
            if (hundredsDigit != null) 
            {
                hundredsDigit.sprite = numberSprites[0];
                hundredsDigit.transform.localScale = Vector3.one * baseScale;
            }
            if (tensDigit != null) 
            {
                tensDigit.sprite = numberSprites[0];
                tensDigit.transform.localScale = Vector3.one * baseScale;
            }
            if (onesDigit != null) 
            {
                onesDigit.sprite = numberSprites[0];
                onesDigit.transform.localScale = Vector3.one * baseScale;
            }
        }
    }
}