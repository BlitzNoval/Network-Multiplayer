using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button), typeof(Image), typeof(RectTransform))]
public class AdvancedUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Sprites")]
    public Sprite normalSprite;
    public Sprite hoverSprite;

    [Header("Press Feedback")]
    public Color pressedColor = Color.gray;

    [Header("Slide In")]
    public Vector2 slideOffset = new Vector2(-200f, 0f);
    public float slideSpeed = 500f;

    [Header("Hover Animation")]
    public float hoverScale = 1.1f;
    public float scaleSpeed = 10f;

    private Image image;
    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private Vector2 startPosition;
    private bool slidingIn = true;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private bool isHovered = false;
    private bool isPressed = false;
    private Color originalColor;

    void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();

        if (normalSprite == null || hoverSprite == null)
        {
            Debug.LogError("Assign both normal and hover sprites.");
            enabled = false;
            return;
        }

        image.sprite = normalSprite;
        originalColor = image.color;

        targetPosition = rectTransform.anchoredPosition;
        startPosition = targetPosition + slideOffset;
        rectTransform.anchoredPosition = startPosition;

        originalScale = rectTransform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Slide-in movement
        if (slidingIn)
        {
            rectTransform.anchoredPosition = Vector2.MoveTowards(
                rectTransform.anchoredPosition,
                targetPosition,
                slideSpeed * Time.deltaTime
            );

            if (Vector2.Distance(rectTransform.anchoredPosition, targetPosition) < 0.1f)
            {
                rectTransform.anchoredPosition = targetPosition;
                slidingIn = false;
            }
        }

        // Smooth scale
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (!isPressed)
        {
            image.sprite = hoverSprite;
            targetScale = originalScale * hoverScale;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (!isPressed)
        {
            image.sprite = normalSprite;
            targetScale = originalScale;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        image.color = pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        image.color = originalColor;

        // Restore hover or normal state
        if (isHovered)
        {
            image.sprite = hoverSprite;
            targetScale = originalScale * hoverScale;
        }
        else
        {
            image.sprite = normalSprite;
            targetScale = originalScale;
        }
    }
}
