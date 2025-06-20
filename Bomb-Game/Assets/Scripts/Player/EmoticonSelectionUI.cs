using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EmoticonSelectionUI : MonoBehaviour
{
    [Header("Emoticon Buttons")]
    [SerializeField] Button emoticonButton1;
    [SerializeField] Button emoticonButton2; 
    [SerializeField] Button emoticonButton3;
    
    [Header("Button Sprites")]
    [SerializeField] Sprite[] emoticonSprites = new Sprite[3]; // Assign your 3 emoticon sprites here
    
    [Header("Display Settings")]
    [SerializeField] Image emoticonDisplay; // The image that shows the selected emoticon
    
    [Header("Animation Settings")]
    [SerializeField] float slideDistance = 200f; // How far to slide from the left
    [SerializeField] float slideDuration = 0.3f; // How long the slide animation takes
    [SerializeField] Ease slideEase = Ease.OutBack;
    
    private PlayerMovement playerMovement;
    private RectTransform rectTransform;
    private Vector3 hiddenPosition;
    private Vector3 shownPosition;
    private bool isAnimating = false;
    
    void Awake()
    {
        // Get RectTransform for positioning
        rectTransform = GetComponent<RectTransform>();
        
        // Set up positions for slide animation
        shownPosition = rectTransform.localPosition;
        hiddenPosition = new Vector3(shownPosition.x - slideDistance, shownPosition.y, shownPosition.z);
        
        // Start in hidden position
        rectTransform.localPosition = hiddenPosition;
        
        // Keep GameObject active but positioned off-screen
        gameObject.SetActive(true);
        
        // Set up button sprites and click listeners
        SetupButtons();
        
        // Disable buttons initially
        SetButtonsInteractable(false);
    }
    
    void Start()
    {
        // Get reference to the player movement component
        playerMovement = GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
        {
            // Try to find it in the same GameObject or parent hierarchy
            Transform current = transform;
            while (current != null && playerMovement == null)
            {
                playerMovement = current.GetComponent<PlayerMovement>();
                current = current.parent;
            }
        }
    }
    
    void SetupButtons()
    {
        // Set button sprites and add click listeners
        if (emoticonButton1 != null && emoticonSprites.Length > 0)
        {
            emoticonButton1.image.sprite = emoticonSprites[0];
            emoticonButton1.onClick.AddListener(() => OnEmoticonButtonClicked(0));
        }
        
        if (emoticonButton2 != null && emoticonSprites.Length > 1)
        {
            emoticonButton2.image.sprite = emoticonSprites[1];
            emoticonButton2.onClick.AddListener(() => OnEmoticonButtonClicked(1));
        }
        
        if (emoticonButton3 != null && emoticonSprites.Length > 2)
        {
            emoticonButton3.image.sprite = emoticonSprites[2];
            emoticonButton3.onClick.AddListener(() => OnEmoticonButtonClicked(2));
        }
    }
    
    public void Show()
    {
        if (isAnimating) return;
        
        Debug.Log("EmoticonSelectionUI showing with slide animation");
        isAnimating = true;
        
        // Enable buttons
        SetButtonsInteractable(true);
        
        // Slide in from left
        rectTransform.DOLocalMove(shownPosition, slideDuration)
            .SetEase(slideEase)
            .OnComplete(() => {
                isAnimating = false;
                Debug.Log("EmoticonSelectionUI slide-in complete");
            });
    }
    
    public void Hide()
    {
        if (isAnimating) return;
        
        Debug.Log("EmoticonSelectionUI hiding with slide animation");
        isAnimating = true;
        
        // Disable buttons immediately
        SetButtonsInteractable(false);
        
        // Slide out to left
        rectTransform.DOLocalMove(hiddenPosition, slideDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => {
                isAnimating = false;
                Debug.Log("EmoticonSelectionUI slide-out complete");
            });
    }
    
    void SetButtonsInteractable(bool interactable)
    {
        if (emoticonButton1 != null) emoticonButton1.interactable = interactable;
        if (emoticonButton2 != null) emoticonButton2.interactable = interactable;
        if (emoticonButton3 != null) emoticonButton3.interactable = interactable;
    }
    
    void OnEmoticonButtonClicked(int emoticonIndex)
    {
        Debug.Log($"Emoticon button {emoticonIndex} clicked!");
        
        // Hide the selection UI immediately
        Hide();
        
        // Show the selected emoticon on the display
        ShowSelectedEmoticon(emoticonIndex);
        
        // Send network command to show this emoticon to other players
        if (playerMovement != null)
        {
            playerMovement.SelectEmoticon(emoticonIndex);
        }
        else
        {
            Debug.LogError("PlayerMovement reference not found!");
        }
    }
    
    void ShowSelectedEmoticon(int emoticonIndex)
    {
        if (emoticonDisplay != null && emoticonIndex >= 0 && emoticonIndex < emoticonSprites.Length)
        {
            emoticonDisplay.sprite = emoticonSprites[emoticonIndex];
            emoticonDisplay.gameObject.SetActive(true);
            
            // Start the animation (rotate and pulse)
            StartCoroutine(PlayEmoticonAnimation());
        }
    }
    
    System.Collections.IEnumerator PlayEmoticonAnimation()
    {
        if (emoticonDisplay == null) yield break;
        
        float duration = 2f;
        float elapsed = 0f;
        Vector3 originalScale = emoticonDisplay.transform.localScale;
        float originalRotation = emoticonDisplay.transform.rotation.z;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            
            // Pulse effect (scale)
            float pulseScale = 1f + Mathf.Sin(progress * Mathf.PI * 4) * 0.2f; // 4 pulses over 2 seconds
            emoticonDisplay.transform.localScale = originalScale * pulseScale;
            
            // Taunt rotation (left-right)
            float rotationAngle = Mathf.Sin(progress * Mathf.PI * 3) * 15f; // 3 rotations over 2 seconds, ±15 degrees
            emoticonDisplay.transform.rotation = Quaternion.Euler(0, 0, originalRotation + rotationAngle);
            
            yield return null;
        }
        
        // Reset to original state
        emoticonDisplay.transform.localScale = originalScale;
        emoticonDisplay.transform.rotation = Quaternion.Euler(0, 0, originalRotation);
        
        // Hide the emoticon after animation
        emoticonDisplay.gameObject.SetActive(false);
    }
    
    // Public method to show emoticon from network (for other players to see)
    public void ShowNetworkEmoticon(int emoticonIndex)
    {
        ShowSelectedEmoticon(emoticonIndex);
    }
}