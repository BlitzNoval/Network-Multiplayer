using UnityEngine;
using UnityEngine.UI;

public class ImageSwitcher : MonoBehaviour
{
    public Image targetImage;

    public Sprite spriteA;
    public Sprite spriteB;

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

        targetImage.sprite = spriteA;
        timer = interval;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            showingA = !showingA;
            targetImage.sprite = showingA ? spriteA : spriteB;

            timer = interval;
        }
    }
}
