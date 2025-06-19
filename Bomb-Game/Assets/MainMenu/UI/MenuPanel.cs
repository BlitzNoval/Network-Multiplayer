using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIMenuHub : MonoBehaviour
{
    [Header("Main Menu")]
    public GameObject mainMenuPanel;
    
    [Header("Panels")]
    public GameObject playPanel;
    public GameObject controlsPanel;
    public GameObject settingsPanel;
    public GameObject howToWinPanel;
    
    [Header("Main Menu Buttons")]
    public Button playButton;
    public Button controlsButton;
    public Button settingsButton;
    public Button howToWinButton;
    public Button quitButton;
    
    [Header("Close Buttons")]
    public Button closePlayButton;
    public Button closeControlsButton;
    public Button closeSettingsButton;
    public Button closeHowToWinButton;
    
    [Header("Play Panel Animation Settings")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private Ease slideEase = Ease.OutQuart;
    [SerializeField] private Vector2 playPanelStartPosition = new Vector2(1920, 0); // Off-screen right
    [SerializeField] private Vector2 playPanelEndPosition = new Vector2(0, 0); // Final position
    
    private RectTransform playPanelRect;
    
    void Start()
    {
        // Get the play panel RectTransform for animations
        if (playPanel != null)
        {
            playPanelRect = playPanel.GetComponent<RectTransform>();
        }
        
        // Set up all buttons
        if (playButton != null)
            playButton.onClick.AddListener(OpenPlayPanel);
            
        if (controlsButton != null)
            controlsButton.onClick.AddListener(OpenControlsPanel);
            
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettingsPanel);
            
        if (howToWinButton != null)
            howToWinButton.onClick.AddListener(OpenHowToWinPanel);
            
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
        
        // Close buttons
        if (closePlayButton != null)
            closePlayButton.onClick.AddListener(ClosePlayPanel);
            
        if (closeControlsButton != null)
            closeControlsButton.onClick.AddListener(CloseControlsPanel);
            
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(CloseSettingsPanel);
            
        if (closeHowToWinButton != null)
            closeHowToWinButton.onClick.AddListener(CloseHowToWinPanel);
        
        // Hide all panels at start
        HideAllPanels();
        
        // Hide main menu initially (camera will show it)
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
    }
    
    public void OpenPlayPanel()
    {
        HideAllPanels();
        HideMainMenu();
        if (playPanel != null && playPanelRect != null)
        {
            playPanel.SetActive(true);
            
            // Start the panel at the defined start position
            playPanelRect.anchoredPosition = playPanelStartPosition;
            
            // Animate sliding to the end position
            playPanelRect.DOAnchorPos(playPanelEndPosition, animationDuration).SetEase(slideEase);
            
            Debug.Log("Play panel opened with slide animation");
        }
    }
    
    public void OpenControlsPanel()
    {
        HideAllPanels();
        HideMainMenu();
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(true);
            Debug.Log("Controls panel opened");
        }
    }
    
    public void OpenSettingsPanel()
    {
        HideAllPanels();
        HideMainMenu();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Debug.Log("Settings panel opened");
        }
    }
    
    public void OpenHowToWinPanel()
    {
        HideAllPanels();
        HideMainMenu();
        if (howToWinPanel != null)
        {
            howToWinPanel.SetActive(true);
            Debug.Log("How To Win panel opened");
        }
    }
    
    public void ClosePlayPanel()
    {
        if (playPanel != null && playPanelRect != null)
        {
            // Animate sliding out to the start position (off-screen)
            playPanelRect.DOAnchorPos(playPanelStartPosition, animationDuration)
                .SetEase(slideEase)
                .OnComplete(() => {
                    playPanel.SetActive(false);
                    // Reset position for next time
                    playPanelRect.anchoredPosition = playPanelEndPosition;
                    Debug.Log("Play panel closed with slide animation");
                });
        }
        else if (playPanel != null)
        {
            playPanel.SetActive(false);
            Debug.Log("Play panel closed (no animation)");
        }
        ShowMainMenu();
    }
    
    public void CloseControlsPanel()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
            Debug.Log("Controls panel closed");
        }
        ShowMainMenu();
    }
    
    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            Debug.Log("Settings panel closed");
        }
        ShowMainMenu();
    }
    
    public void CloseHowToWinPanel()
    {
        if (howToWinPanel != null)
        {
            howToWinPanel.SetActive(false);
            Debug.Log("How To Win panel closed");
        }
        ShowMainMenu();
    }
    
    public void HideAllPanels()
    {
        if (playPanel != null) playPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToWinPanel != null) howToWinPanel.SetActive(false);
    }
    
    public void HideMainMenu()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
    }
    
    public void ShowMainMenu()
    {
        HideAllPanels();
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("Main menu shown");
        }
    }
    
    // Call this when intro is complete
    public void OnIntroComplete()
    {
        ShowMainMenu();
    }
    
    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowMainMenu();
        }
    }
    
    void OnDestroy()
    {
        // Clean up any ongoing animations
        if (playPanelRect != null)
            playPanelRect.DOKill();
    }
}