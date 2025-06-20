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
    [SerializeField] Sprite[] numberSprites;
    
    [Header("Background Panel")]
    [SerializeField] Image backgroundPanel;
    
    [Header("Visual Feedback Effects")]
    [SerializeField] EffectType feedbackEffect = EffectType.BarFlash;
    
    [Header("Effect Components")]
    [SerializeField] Image flashOverlay;
    [SerializeField] ParticleSystem sparksEffect;
    [SerializeField] Image glowEffect;
    
    [Header("Number Scale Settings")]
    [SerializeField] float baseScale = 1f;
    [SerializeField] float maxScale = 2f;
    [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Percentage Sign Settings")]
    [SerializeField] float percentageBaseScale = 0.8f;
    [SerializeField] float percentageMaxScale = 1.6f;
    
    [Header("Pulse Settings")]
    [SerializeField] float pulseScale = 1.2f;
    [SerializeField] float pulseDuration = 0.5f;
    
    [Header("Visual Effect Settings")]
    [SerializeField] float effectDuration = 0.3f;
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float glowIntensity = 1.5f;
    [SerializeField] int particleBurst = 15;
    
    [Header("Shake Intensity Settings")]
    [SerializeField] float minShakeStrength = 1f;
    [SerializeField] float maxShakeStrength = 8f;
    [SerializeField] int minShakeVibrato = 5;
    [SerializeField] int maxShakeVibrato = 25;
    [SerializeField] AnimationCurve shakeIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Background Panel Scaling")]
    [SerializeField] bool scaleBackgroundPanel = true;
    [SerializeField] float backgroundMaxScale = 1.2f;
    
    [Header("Elimination Animation")]
    [SerializeField] float eliminationDuration = 0.8f;
    [SerializeField] Ease eliminationEase = Ease.OutQuart;

    [Header("Emoticon Display")]
    [SerializeField] Sprite[] emoticonSprites;
    [SerializeField] Image emoticonDisplay;

    readonly Color[] playerColors = new Color[]
    {
        new Color(0.208f, 0.380f, 0.702f), 
        new Color(0.706f, 0.196f, 0.224f), 
        new Color(0.275f, 0.663f, 0.298f), 
        new Color(0.965f, 0.580f, 0.098f)  
    };

    const float BLUE_MAX = 116.67f;
    const float YELLOW_MAX = 233.33f;
    const float RED_MAX = 350f;
    
    public enum EffectType
    {
        BarFlash, NumberPop, BarGlow, Sparks, ColorShift, Shake
    }
    
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
            
        ResetPercentageDisplay();
        if (emoticonDisplay != null)
            emoticonDisplay.gameObject.SetActive(false);
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
        
        if (newLives <= 0 && oldLives > 0)
        {
            PlayEliminationAnimation();
        }
        else if (newLives < oldLives)
        {
            ResetPercentageDisplay();
        }
    }

    public void SetKnockback(float oldPercentage, float newPercentage)
    {
        int percentage = Mathf.Clamp(Mathf.RoundToInt(newPercentage), 0, 350);
        UpdateGradientBars(percentage);
        UpdateNumberDisplay(percentage);
        
        if (percentage > lastPercentage && percentage > 0)
        {
            PlayFeedbackEffect();
        }
        
        HandlePulseAnimation(percentage);
        lastPercentage = percentage;
    }

    public void SetGhost(bool ghost)
    {
        gameObject.SetActive(!ghost);
    }
    
    void UpdateGradientBars(int percentage)
    {
        float progress = percentage / 350f;
        
        float blueFill = 0f;
        float yellowFill = 0f;
        float redFill = 0f;
        float blueAlpha = 1f;
        float yellowAlpha = 0f;
        float redAlpha = 0f;
        
        if (progress <= 0.333f)
        {
            blueFill = progress / 0.333f;
            blueAlpha = 1f;
            yellowAlpha = 0f;
            redAlpha = 0f;
        }
        else if (progress <= 0.666f)
        {
            blueFill = 1f;
            yellowFill = (progress - 0.333f) / 0.333f;
            float transitionProgress = (progress - 0.333f) / 0.333f;
            blueAlpha = Mathf.Lerp(1f, 0.3f, transitionProgress);
            yellowAlpha = Mathf.Lerp(0f, 1f, transitionProgress);
            redAlpha = 0f;
        }
        else
        {
            blueFill = 1f;
            yellowFill = 1f;
            redFill = (progress - 0.666f) / 0.334f;
            float transitionProgress = (progress - 0.666f) / 0.334f;
            blueAlpha = 0.2f;
            yellowAlpha = Mathf.Lerp(1f, 0.3f, transitionProgress);
            redAlpha = Mathf.Lerp(0f, 1f, transitionProgress);
        }
        
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
        
        int hundreds = percentage / 100;
        int tens = (percentage % 100) / 10;
        int ones = percentage % 10;
        
        if (hundredsDigit != null && hundreds < numberSprites.Length)
            hundredsDigit.sprite = numberSprites[hundreds];
            
        if (tensDigit != null && tens < numberSprites.Length)
            tensDigit.sprite = numberSprites[tens];
            
        if (onesDigit != null && ones < numberSprites.Length)
            onesDigit.sprite = numberSprites[ones];
        
        float scaleProgress = percentage / 350f;
        float currentScale = Mathf.Lerp(baseScale, maxScale, scaleCurve.Evaluate(scaleProgress));
        
        if (hundredsDigit != null) hundredsDigit.transform.localScale = Vector3.one * currentScale;
        if (tensDigit != null) tensDigit.transform.localScale = Vector3.one * currentScale;
        if (onesDigit != null) onesDigit.transform.localScale = Vector3.one * currentScale;
        
        if (percentageSign != null)
        {
            float percentageScale = Mathf.Lerp(percentageBaseScale, percentageMaxScale, scaleCurve.Evaluate(scaleProgress));
            percentageSign.transform.localScale = Vector3.one * percentageScale;
        }
        
        if (backgroundPanel != null)
        {
            float backgroundScale = currentScale;
            
            if (scaleBackgroundPanel)
            {
                float extraScale = Mathf.Lerp(1f, backgroundMaxScale, scaleProgress);
                backgroundScale *= extraScale;
            }
            
            backgroundPanel.transform.localScale = Vector3.one * backgroundScale;
        }
    }
    
    void ResetPercentageDisplay()
    {
        StopAllAnimations();
        
        if (blueBarFill != null) 
        {
            blueBarFill.fillAmount = 0f;
            var color = blueBarFill.color;
            color.a = 1f;
            blueBarFill.color = color;
        }
        
        if (yellowBarFill != null) 
        {
            yellowBarFill.fillAmount = 0f;
            var color = yellowBarFill.color;
            color.a = 0f;
            yellowBarFill.color = color;
        }
        
        if (redBarFill != null) 
        {
            redBarFill.fillAmount = 0f;
            var color = redBarFill.color;
            color.a = 0f;
            redBarFill.color = color;
        }
        
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
        
        if (percentageSign != null)
        {
            percentageSign.transform.localScale = Vector3.one * percentageBaseScale;
        }
        
        if (backgroundPanel != null)
        {
            backgroundPanel.transform.localScale = Vector3.one * baseScale;
        }
        
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
        
        UpdateNumberDisplay(lastPercentage);
    }
    
    void PlayFeedbackEffect()
    {
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

    public void ShowEmoticon(int index)
    {
        if (index < 0 || index >= emoticonSprites.Length || emoticonDisplay == null) return;
        
        emoticonDisplay.sprite = emoticonSprites[index];
        emoticonDisplay.gameObject.SetActive(true);
        emoticonDisplay.transform.localScale = Vector3.zero;
        
        Sequence seq = DOTween.Sequence();
        seq.Append(emoticonDisplay.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.7f);
        seq.Append(emoticonDisplay.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
        seq.OnComplete(() => emoticonDisplay.gameObject.SetActive(false));
    }
}