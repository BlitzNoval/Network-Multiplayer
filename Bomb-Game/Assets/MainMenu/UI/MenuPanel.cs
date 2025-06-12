using UnityEngine;
using UnityEngine.UI;

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
    
    void Start()
    {
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
        if (playPanel != null)
        {
            playPanel.SetActive(true);
            Debug.Log("Play panel opened");
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
        if (playPanel != null)
        {
            playPanel.SetActive(false);
            Debug.Log("Play panel closed");
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
}