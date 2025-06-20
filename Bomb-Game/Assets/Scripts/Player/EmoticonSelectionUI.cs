using UnityEngine;
using UnityEngine.UI;

public class EmoticonSelectionUI : MonoBehaviour
{
    [SerializeField] Image emoticon1, emoticon2, emoticon3;
    [SerializeField] float highlightScale = 1.2f;

    void Awake()
    {
        // Ensure UI is hidden by default
        gameObject.SetActive(false);
    }

    public void HighlightEmoticon(int index)
    {
        // Index is 0-based (0, 1, 2)
        emoticon1.transform.localScale = index == 0 ? Vector3.one * highlightScale : Vector3.one;
        emoticon2.transform.localScale = index == 1 ? Vector3.one * highlightScale : Vector3.one;
        emoticon3.transform.localScale = index == 2 ? Vector3.one * highlightScale : Vector3.one;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        // Reset all scales to normal
        emoticon1.transform.localScale = Vector3.one;
        emoticon2.transform.localScale = Vector3.one;
        emoticon3.transform.localScale = Vector3.one;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}