using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EmoticonSelectionUI : MonoBehaviour
{
    [SerializeField] Button emoticonButton1;
    [SerializeField] Button emoticonButton2; 
    [SerializeField] Button emoticonButton3;
    
    [SerializeField] Sprite[] emoticonSprites = new Sprite[3];
    
    [SerializeField] Image emoticonDisplay;
    
    [SerializeField] float slideDistance = 200f;
    [SerializeField] float slideDuration = 0.3f;
    [SerializeField] Ease slideEase = Ease.OutBack;
    
    private PlayerMovement playerMovement;
    private RectTransform rectTransform;
    private Vector3 hiddenPosition;
    private Vector3 shownPosition;
    private bool isAnimating = false;
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        shownPosition = rectTransform.localPosition;
        hiddenPosition = new Vector3(shownPosition.x - slideDistance, shownPosition.y, shownPosition.z);
        
        rectTransform.localPosition = hiddenPosition;
        
        gameObject.SetActive(true);
        
        SetupButtons();
        
        SetButtonsInteractable(false);
    }
    
    void Start()
    {
        playerMovement = GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
        {
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
        
        SetButtonsInteractable(true);
        
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
        
        SetButtonsInteractable(false);
        
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
        
        Hide();
        
        ShowSelectedEmoticon(emoticonIndex);
        
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
            
            float pulseScale = 1f + Mathf.Sin(progress * Mathf.PI * 4) * 0.2f;
            emoticonDisplay.transform.localScale = originalScale * pulseScale;
            
            float rotationAngle = Mathf.Sin(progress * Mathf.PI * 3) * 15f;
            emoticonDisplay.transform.rotation = Quaternion.Euler(0, 0, originalRotation + rotationAngle);
            
            yield return null;
        }
        
        emoticonDisplay.transform.localScale = originalScale;
        emoticonDisplay.transform.rotation = Quaternion.Euler(0, 0, originalRotation);
        
        emoticonDisplay.gameObject.SetActive(false);
    }
    
    public void ShowNetworkEmoticon(int emoticonIndex)
    {
        ShowSelectedEmoticon(emoticonIndex);
    }
}