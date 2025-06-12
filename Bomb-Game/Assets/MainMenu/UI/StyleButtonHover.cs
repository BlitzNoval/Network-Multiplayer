using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;

public class TABSStyleButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]
    [SerializeField] private float hoverScale = 1.3f;
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private Ease animationEase = Ease.OutQuart;
    
    [Header("Shift Settings")]
    [SerializeField] private float shiftDistance = 30f;
    [SerializeField] private float shiftDuration = 0.2f;
    
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private bool isHovered = false;
    
    // Static references to manage all buttons
    private static List<TABSStyleButtonHover> allButtons = new List<TABSStyleButtonHover>();
    private static TABSStyleButtonHover currentHoveredButton;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        
        // Ensure the button starts at normal scale
        rectTransform.localScale = Vector3.one;
    }

    private void Start()
    {
        allButtons.Add(this);
    }

    private void OnDestroy()
    {
        allButtons.Remove(this);
        rectTransform.DOKill();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovered) return;
        
        isHovered = true;
        currentHoveredButton = this;
        
        // Scale up this button
        rectTransform.DOScale(hoverScale, animationDuration).SetEase(animationEase);
        
        // Shift other buttons
        ShiftOtherButtons();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovered) return;
        
        isHovered = false;
        currentHoveredButton = null;
        
        // Scale back to normal
        rectTransform.DOScale(1f, animationDuration).SetEase(animationEase);
        
        // Return all buttons to original positions
        ReturnAllButtons();
    }

    private void ShiftOtherButtons()
    {
        int myIndex = transform.GetSiblingIndex();
        
        for (int i = 0; i < allButtons.Count; i++)
        {
            var button = allButtons[i];
            if (button == this || button == null) continue;
            
            int buttonIndex = button.transform.GetSiblingIndex();
            Vector2 targetPosition = button.originalPosition;
            
            // Buttons above move up, buttons below move down
            if (buttonIndex < myIndex)
            {
                targetPosition.y += shiftDistance;
            }
            else if (buttonIndex > myIndex)
            {
                targetPosition.y -= shiftDistance;
            }
            
            button.rectTransform.DOAnchorPos(targetPosition, shiftDuration).SetEase(animationEase);
        }
    }

    private void ReturnAllButtons()
    {
        foreach (var button in allButtons)
        {
            if (button == null) continue;
            button.rectTransform.DOAnchorPos(button.originalPosition, shiftDuration).SetEase(animationEase);
        }
    }

    // Call this if you need to update positions after layout changes
    public void RefreshOriginalPosition()
    {
        originalPosition = rectTransform.anchoredPosition;
    }

    public static void RefreshAllPositions()
    {
        foreach (var button in allButtons)
        {
            button?.RefreshOriginalPosition();
        }
    }
}