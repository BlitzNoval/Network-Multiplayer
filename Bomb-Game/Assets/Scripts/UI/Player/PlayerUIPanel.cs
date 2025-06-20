using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

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
    [SerializeField] Image percentageSign;
    [SerializeField] Sprite[] numberSprites; // 0-9 sprites
    
    [Header("Background Panel")]
    [SerializeField] Image backgroundPanel; // Panel behind numbers for visibility
    
    [Header("Visual Feedback Effects")]
    [SerializeField] EffectType feedbackEffect = EffectType.BarFlash;
    
    [Header("Effect Components")]
    [SerializeField] Image flashOverlay; // For flash effects
    [SerializeField] ParticleSystem sparksEffect; // For particle effects
    [SerializeField] Image glowEffect; // For glow effects
    
    [Header("Number Scale Settings")]
    [SerializeField] float baseScale = 1f;
    [SerializeField] float maxScale = 2f;
    [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Percentage Sign Settings")]
    [SerializeField] float percentageBaseScale = 0.8f;
    [SerializeField] float percentageMaxScale = 1.6f; // Half the rate of numbers
    
    [Header("Pulse Settings")]
    [SerializeField] float pulseScale = 1.2f;
    [SerializeField] float pulseDuration = 0.5f;
    
    [Header("Visual Effect Settings")]
    [SerializeField] float effectDuration = 0.3f;
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float glowIntensity = 1.5f;
    [SerializeField] int particleBurst = 15;
    
    [Header("Shake Intensity Settings")]
    [SerializeField] float minShakeStrength = 1f;     // Shake strength at 0%
    [SerializeField] float maxShakeStrength = 8f;     // Shake strength at 350%
    [SerializeField] int minShakeVibrato = 5;         // Shake speed at 0%
    [SerializeField] int maxShakeVibrato = 25;        // Shake speed at 350%
    [SerializeField] AnimationCurve shakeIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Background Panel Scaling")]
    [SerializeField] bool scaleBackgroundPanel = true;
    [SerializeField] float backgroundMaxScale = 1.2f; // How much the background grows
    
    [Header("Elimination Animation")]
    [SerializeField] float eliminationDuration = 0.8f;
    [SerializeField] Ease eliminationEase = Ease.OutQuart;

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
    
    // Effect types enum
    public enum EffectType
    {
        BarFlash,           // Flash the bar briefly
        NumberPop,          // Pop the numbers briefly 
        BarGlow,            // Glow effect around bar
        Sparks,             // Particle sparks
        ColorShift,         // Brief color change
        Shake               // Shake the UI elements
    }
    
    // Animation state tracking
    private bool isPulsing = false;
    private int lastPercentage = 0;
    private Sequence pulseSequence;
    private Sequence effectSequence;

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
        
        // Handle elimination animation when player runs out of lives
        if (newLives <= 0 && oldLives > 0)
        {
            PlayEliminationAnimation();
        }
        // Reset percentage display when player dies (loses a life) but not eliminated
        else if (newLives < oldLives)
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
        
        // Trigger visual feedback when percentage increases
        if (percentage > lastPercentage && percentage > 0)
        {
            PlayFeedbackEffect();
        }
        
        // Handle pulse animation for high percentages
        HandlePulseAnimation(percentage);
        
        lastPercentage = percentage;
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
        
        // Scale percentage sign at half the rate
        if (percentageSign != null)
        {
            float percentageScale = Mathf.Lerp(percentageBaseScale, percentageMaxScale, scaleCurve.Evaluate(scaleProgress));
            percentageSign.transform.localScale = Vector3.one * percentageScale;
        }
        
        // Scale background panel with numbers and additional scaling
        if (backgroundPanel != null)
        {
            float backgroundScale = currentScale;
            
            if (scaleBackgroundPanel)
            {
                // Add extra scaling based on percentage (grows more than numbers)
                float extraScale = Mathf.Lerp(1f, backgroundMaxScale, scaleProgress);
                backgroundScale *= extraScale;
            }
            
            backgroundPanel.transform.localScale = Vector3.one * backgroundScale;
        }
    }
    
    void ResetPercentageDisplay()
    {
        // Stop any ongoing animations
        StopAllAnimations();
        
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
        
        // Reset percentage sign
        if (percentageSign != null)
        {
            percentageSign.transform.localScale = Vector3.one * percentageBaseScale;
        }
        
        // Reset background panel
        if (backgroundPanel != null)
        {
            backgroundPanel.transform.localScale = Vector3.one * baseScale;
        }
        
        // Reset animation state
        isPulsing = false;
        lastPercentage = 0;
    }
    
    void HandlePulseAnimation(int percentage)
    {
        if (percentage >= 300 && !isPulsing)
        {
            StartPulsing();
        }
        else if (percentage < 300 && isPulsing)
        {
            StopPulsing();
        }
    }
    
    void StartPulsing()
    {
        if (isPulsing) return;
        
        isPulsing = true;
        
        // Create pulsing sequence for all number elements
        pulseSequence = DOTween.Sequence();
        
        if (hundredsDigit != null)
        {
            pulseSequence.Join(hundredsDigit.transform.DOScale(pulseScale, pulseDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
        }
        
        if (tensDigit != null)
        {
            pulseSequence.Join(tensDigit.transform.DOScale(pulseScale, pulseDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
        }
        
        if (onesDigit != null)
        {
            pulseSequence.Join(onesDigit.transform.DOScale(pulseScale, pulseDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
        }
    }
    
    void StopPulsing()
    {
        if (!isPulsing) return;
        
        isPulsing = false;
        
        if (pulseSequence != null)
        {
            pulseSequence.Kill();
            pulseSequence = null;
        }
        
        // Reset scales to current percentage-based values
        UpdateNumberDisplay(lastPercentage);
    }
    
    void PlayFeedbackEffect()
    {
        // Kill any existing effect animation
        if (effectSequence != null)
        {
            effectSequence.Kill();
        }
        
        switch (feedbackEffect)
        {
            case EffectType.BarFlash:
                PlayBarFlash();
                break;
            case EffectType.NumberPop:
                PlayNumberPop();
                break;
            case EffectType.BarGlow:
                PlayBarGlow();
                break;
            case EffectType.Sparks:
                PlaySparks();
                break;
            case EffectType.ColorShift:
                PlayColorShift();
                break;
            case EffectType.Shake:
                PlayShake();
                break;
        }
    }
    
    void PlayBarFlash()
    {
        if (flashOverlay == null) return;
        
        flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        
        effectSequence = DOTween.Sequence();
        effectSequence.Append(flashOverlay.DOFade(0.8f, effectDuration * 0.3f))
                     .Append(flashOverlay.DOFade(0f, effectDuration * 0.7f));
    }
    
    void PlayNumberPop()
    {
        effectSequence = DOTween.Sequence();
        
        Vector3 originalScale = Vector3.one * (baseScale + (lastPercentage / 350f) * (maxScale - baseScale));
        Vector3 popScale = originalScale * 1.3f;
        
        if (hundredsDigit != null)
            effectSequence.Join(hundredsDigit.transform.DOScale(popScale, effectDuration * 0.5f)
                .SetEase(Ease.OutBack).OnComplete(() => 
                    hundredsDigit.transform.DOScale(originalScale, effectDuration * 0.5f)));
                    
        if (tensDigit != null)
            effectSequence.Join(tensDigit.transform.DOScale(popScale, effectDuration * 0.5f)
                .SetEase(Ease.OutBack).OnComplete(() => 
                    tensDigit.transform.DOScale(originalScale, effectDuration * 0.5f)));
                    
        if (onesDigit != null)
            effectSequence.Join(onesDigit.transform.DOScale(popScale, effectDuration * 0.5f)
                .SetEase(Ease.OutBack).OnComplete(() => 
                    onesDigit.transform.DOScale(originalScale, effectDuration * 0.5f)));
    }
    
    void PlayBarGlow()
    {
        if (glowEffect == null) return;
        
        Color glowColor = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        glowEffect.color = glowColor;
        
        effectSequence = DOTween.Sequence();
        effectSequence.Append(glowEffect.DOFade(glowIntensity, effectDuration * 0.4f))
                     .Append(glowEffect.DOFade(0f, effectDuration * 0.6f));
    }
    
    void PlaySparks()
    {
        if (sparksEffect == null) return;
        
        sparksEffect.Emit(particleBurst);
    }
    
    void PlayColorShift()
    {
        // Temporarily shift the bar colors brighter
        effectSequence = DOTween.Sequence();
        
        if (blueBarFill != null)
        {
            Color originalBlue = blueBarFill.color;
            Color brightBlue = Color.Lerp(originalBlue, Color.white, 0.5f);
            effectSequence.Join(blueBarFill.DOColor(brightBlue, effectDuration * 0.3f)
                .OnComplete(() => blueBarFill.DOColor(originalBlue, effectDuration * 0.7f)));
        }
        
        if (yellowBarFill != null)
        {
            Color originalYellow = yellowBarFill.color;
            Color brightYellow = Color.Lerp(originalYellow, Color.white, 0.5f);
            effectSequence.Join(yellowBarFill.DOColor(brightYellow, effectDuration * 0.3f)
                .OnComplete(() => yellowBarFill.DOColor(originalYellow, effectDuration * 0.7f)));
        }
        
        if (redBarFill != null)
        {
            Color originalRed = redBarFill.color;
            Color brightRed = Color.Lerp(originalRed, Color.white, 0.5f);
            effectSequence.Join(redBarFill.DOColor(brightRed, effectDuration * 0.3f)
                .OnComplete(() => redBarFill.DOColor(originalRed, effectDuration * 0.7f)));
        }
    }
    
    void PlayShake()
    {
        Vector3 originalPos = transform.localPosition;
        
        // Calculate shake intensity based on current percentage
        float percentageProgress = lastPercentage / 350f;
        float curveValue = shakeIntensityCurve.Evaluate(percentageProgress);
        
        float shakeStrength = Mathf.Lerp(minShakeStrength, maxShakeStrength, curveValue);
        int shakeVibrato = Mathf.RoundToInt(Mathf.Lerp(minShakeVibrato, maxShakeVibrato, curveValue));
        
        effectSequence = DOTween.Sequence();
        effectSequence.Append(transform.DOShakePosition(effectDuration, shakeStrength, shakeVibrato, 90, false, true))
                     .OnComplete(() => transform.localPosition = originalPos);
    }
    
    void PlayEliminationAnimation()
    {
        // Fade out and scale down the entire panel
        transform.DOScale(0f, eliminationDuration).SetEase(eliminationEase);
        
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        canvasGroup.DOFade(0f, eliminationDuration).SetEase(eliminationEase)
            .OnComplete(() => {
                gameObject.SetActive(false);
            });
    }
    
    void StopAllAnimations()
    {
        if (pulseSequence != null)
        {
            pulseSequence.Kill();
            pulseSequence = null;
        }
        
        if (effectSequence != null)
        {
            effectSequence.Kill();
            effectSequence = null;
        }
        
        isPulsing = false;
    }
    
    void OnDestroy()
    {
        StopAllAnimations();
    }
}