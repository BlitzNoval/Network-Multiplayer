using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonImageSwitcher : MonoBehaviour
{
    public Image sharedImage;

    public Button joinRoomButton;
    public Sprite joinRoomSprite;

    public Button createRoomButton;
    public Sprite createRoomSprite;

    public Button backButton;
    public Sprite backSprite;

    public Sprite defaultSprite;

    void Start()
    {
        AddEventTriggers(joinRoomButton, joinRoomSprite);
        AddEventTriggers(createRoomButton, createRoomSprite);
        AddEventTriggers(backButton, backSprite);
    }

    void AddEventTriggers(Button button, Sprite sprite)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener((_) => SetImage(sprite));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exit.callback.AddListener((_) => ResetImage());
        trigger.triggers.Add(exit);
    }

    void SetImage(Sprite sprite)
    {
        if (sharedImage != null && sprite != null)
            sharedImage.sprite = sprite;
    }

    void ResetImage()
    {
        if (sharedImage != null && defaultSprite != null)
            sharedImage.sprite = defaultSprite;
    }
}
