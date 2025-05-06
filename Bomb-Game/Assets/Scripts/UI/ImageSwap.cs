using UnityEngine;
using UnityEngine.UI;

public class ImageSwitcher : MonoBehaviour
{
    [Header("Target UI Image")]
    public Image targetImage;

    [Header("Sprites to Switch Between")]
    public Sprite spriteA;
    public Sprite spriteB;

    [Header("Switching Interval (seconds)")]
    public float interval = 1.0f;

    private float timer;
    private bool showingA = true;

    void Start()
    {
        if (targetImage == null || spriteA == null || spriteB == null)
        {
            Debug.LogError("ImageSwitcher: Please assign all fields in the inspector.");
            enabled = false;
            return;
        }

        // Initialize the image
        targetImage.sprite = spriteA;
        timer = interval;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            // Toggle sprite
            showingA = !showingA;
            targetImage.sprite = showingA ? spriteA : spriteB;

            // Reset timer
            timer = interval;
        }
    }
}
