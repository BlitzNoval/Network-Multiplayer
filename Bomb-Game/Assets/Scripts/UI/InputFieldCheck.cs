using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_InputField))]
[RequireComponent(typeof(Image))]
public class InputFieldSpriteSwitcher : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("Sprites")]
    public Sprite normalSprite;
    public Sprite activeSprite;

    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();

        if (normalSprite == null || activeSprite == null)
        {
            Debug.LogError("Assign both normal and active sprites.");
            enabled = false;
            return;
        }

        image.sprite = normalSprite;
    }

    public void OnSelect(BaseEventData eventData)
    {
        image.sprite = activeSprite;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        image.sprite = normalSprite;
    }
}
