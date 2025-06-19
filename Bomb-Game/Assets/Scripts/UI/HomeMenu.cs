using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using DG.Tweening;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject playPanel;
    public GameObject settingsPanel; 
    public GameObject controlsPanel;
    public GameObject howToWinPanel;
    public float animationDuration = 0.25f;
    
    [Header("Play Panel Slide Animation")]
    [SerializeField] private float slideAnimationDuration = 0.5f;
    [SerializeField] private Ease slideEase = Ease.OutQuart;
    [SerializeField] private Vector2 playPanelStartPosition = new Vector2(1920, 0); // Off-screen right
    [SerializeField] private Vector2 playPanelEndPosition = new Vector2(0, 0); // Final position
    
    private RectTransform playPanelRect;  

    private void Start()
    {
        // Get play panel RectTransform for slide animation
        if (playPanel != null)
            playPanelRect = playPanel.GetComponent<RectTransform>();
            
        InitPanel(playPanel);
        InitPanel(settingsPanel);
        InitPanel(controlsPanel);
        InitPanel(howToWinPanel);
    }

    private void InitPanel(GameObject panel)
    {
        panel.SetActive(false);
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = panel.AddComponent<CanvasGroup>();
        }
        cg.alpha = 0f;
    }

    #region Button Callbacks

    public void OnPlayPressed() => ShowPlayPanel();
    public void OnSettingsPressed() 
    {
        ClosePlayPanelIfOpen();
        ShowPanel(settingsPanel);
    }
    
    public void OnControlsPressed() 
    {
        ClosePlayPanelIfOpen();
        ShowPanel(controlsPanel);
    }
    
    public void OnHowToWinPressed() 
    {
        ClosePlayPanelIfOpen();
        ShowPanel(howToWinPanel);
    }
    public void OnQuitPressed() => Application.Quit();

    #endregion

    #region Close Panel Methods

    public void ClosePlayPanel() => HidePlayPanel();
    public void CloseSettingsPanel() => HidePanel(settingsPanel);
    public void CloseControlsPanel() => HidePanel(controlsPanel);
    public void CloseHowToWinPanel() => HidePanel(howToWinPanel);

    #endregion

    #region Play Panel Slide Animation
    
    private void ClosePlayPanelIfOpen()
    {
        if (playPanel != null && playPanel.activeInHierarchy)
        {
            HidePlayPanel();
        }
    }
    
    private void ShowPlayPanel()  
    {
        if (playPanel != null && playPanelRect != null)
        {
            StopAllCoroutines();
            playPanelRect.DOKill(); // Kill any existing animations
            
            playPanel.SetActive(true);
            CanvasGroup cg = playPanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            
            // Start from off-screen position
            playPanelRect.anchoredPosition = playPanelStartPosition;
            
            // Slide to end position
            playPanelRect.DOAnchorPos(playPanelEndPosition, slideAnimationDuration).SetEase(slideEase);
        }
    }
    
    private void HidePlayPanel()
    {
        if (playPanel != null && playPanelRect != null)
        {
            StopAllCoroutines();
            playPanelRect.DOKill(); // Kill any existing animations
            
            // Slide to start position (off-screen)
            playPanelRect.DOAnchorPos(playPanelStartPosition, slideAnimationDuration)
                .SetEase(slideEase)
                .OnComplete(() => {
                    playPanel.SetActive(false);
                    // Reset position for next time
                    playPanelRect.anchoredPosition = playPanelEndPosition;
                });
        }
    }
    
    #endregion

    #region Panel Animation Helpers

    private void ShowPanel(GameObject panel)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateOpen(panel));
    }

    private void HidePanel(GameObject panel)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateClose(panel));
    }

    private IEnumerator AnimateOpen(GameObject panel)
    {
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        panel.transform.localScale = Vector3.zero;
        panel.SetActive(true);
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float easeT = EaseOutBack(t);
            panel.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, easeT);
            cg.alpha = t;
            yield return null;
        }

        panel.transform.localScale = Vector3.one;
        cg.alpha = 1f;
    }

    private IEnumerator AnimateClose(GameObject panel)
    {
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        float elapsed = 0f;
        Vector3 startScale = panel.transform.localScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            panel.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            cg.alpha = 1f - t;
            yield return null;
        }

        panel.SetActive(false);
        panel.transform.localScale = Vector3.one;
        cg.alpha = 0f;
    }

    // EaseOutBack for subtle bounce
    private float EaseOutBack(float t, float s = 1.70158f)
    {
        t = t - 1;
        return (t * t * ((s + 1) * t + s) + 1);
    }

    #endregion
    
    private void OnDestroy()
    {
        // Clean up any ongoing DOTween animations
        if (playPanelRect != null)
            playPanelRect.DOKill();
    }
}
