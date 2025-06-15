using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TrajectoryDot : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    
    [Header("Visual Settings")]
    [SerializeField] private Sprite dotSprite;
    [SerializeField] private float baseSize = 0.3f;
    [SerializeField] private bool billboardToCamera = true;
    
    private Camera cachedCamera;
    private Quaternion lastCameraRotation;
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (dotSprite == null)
        {
            // Create a simple circle sprite if none assigned
            dotSprite = CreateCircleDotSprite();
        }
        
        spriteRenderer.sprite = dotSprite;
        transform.localScale = Vector3.one * baseSize;
        
        // Set to UI layer or similar for proper rendering
        spriteRenderer.sortingLayerName = "UI";
        spriteRenderer.sortingOrder = 5;
    }
    
    void Update()
    {
        if (billboardToCamera)
        {
            // Cache camera reference
            if (cachedCamera == null)
                cachedCamera = Camera.main;
                
            if (cachedCamera != null)
            {
                // Only update rotation if camera rotation changed significantly
                if (Quaternion.Angle(lastCameraRotation, cachedCamera.transform.rotation) > 1f)
                {
                    transform.rotation = cachedCamera.transform.rotation;
                    lastCameraRotation = cachedCamera.transform.rotation;
                }
            }
        }
    }
    
    public void SetColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }
    
    public void SetSize(float size)
    {
        transform.localScale = Vector3.one * size;
    }
    
    Sprite CreateCircleDotSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    tex.SetPixel(x, y, Color.white);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}