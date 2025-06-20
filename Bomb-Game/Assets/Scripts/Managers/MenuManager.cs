using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }
    
    [Header("UI References")]
    public Slider volumeSlider;
    public Slider sensitivitySlider;
    public Button changeSongButton;
    public Button toggleSoundButton;
    public Button resetSensitivityButton;
    public TextMeshProUGUI songNameText;
    
    [Header("Mute Button Sprites")]
    public Sprite mutedSprite;
    public Sprite unmutedSprite;
    
    [Header("Text Display Settings")]
    public int maxCharacters = 7;
    public float scrollSpeed = 1f;  
    public float scrollDelay = 2f;
    
    [Header("Default Values")]
    public float defaultVolume = 0.5f;
    public bool defaultMuteState = false;
    public float defaultSensitivity = 1.0f;
    
    private bool isMuted = false;
    private float savedVolume;
    private float savedSensitivity;
    private string fullSongName = "";
    private bool isScrolling = false;
    private int scrollPosition = 0;
    private float scrollTimer = 0f;
    
    private const string VOLUME_KEY = "GameVolume";
    private const string MUTE_KEY = "GameMuted";
    private const string SENSITIVITY_KEY = "ThrowSensitivity";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        SetupButtonListeners();
        LoadSettings();
        UpdateUI();
        
        InvokeRepeating(nameof(UpdateSongDisplay), 0f, 0.5f);
    }
    
    private void SetupButtonListeners()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            
        if (changeSongButton != null)
            changeSongButton.onClick.AddListener(OnChangeSongClicked);
            
        if (toggleSoundButton != null)
            toggleSoundButton.onClick.AddListener(ToggleSound);
            
        if (resetSensitivityButton != null)
            resetSensitivityButton.onClick.AddListener(ResetSensitivity);
    }
    
    private void LoadSettings()
    {
        savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, defaultVolume);
        isMuted = PlayerPrefs.GetInt(MUTE_KEY, defaultMuteState ? 1 : 0) == 1;
        savedSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, defaultSensitivity);
        
        ApplyVolumeSettings();
    }
    
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(VOLUME_KEY, savedVolume);
        PlayerPrefs.SetInt(MUTE_KEY, isMuted ? 1 : 0);
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, savedSensitivity);
        PlayerPrefs.Save();
    }
    
    private void UpdateUI()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
        }
        
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = savedSensitivity;
        }
        
        UpdateMuteButton();
        UpdateSongDisplay();
    }
    
    private void UpdateMuteButton()
    {
        if (toggleSoundButton != null)
        {
            TextMeshProUGUI buttonText = toggleSoundButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isMuted ? "MUTED" : "UNMUTED";
            }
            
            Image buttonImage = toggleSoundButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = isMuted ? mutedSprite : unmutedSprite;
            }
        }
    }
    
    private void UpdateSongDisplay()
    {
        if (RadioManager.Instance != null)
        {
            string currentSongName = RadioManager.Instance.GetCurrentSongName();
            if (currentSongName != fullSongName)
            {
                fullSongName = currentSongName;
                scrollPosition = 0;
                isScrolling = false;
                scrollTimer = 0f;
                UpdateScrollingText();
            }
        }
    }
    
    private void Update()
    {
        HandleTextScrolling();
    }
    
    private void HandleTextScrolling()
    {
        if (songNameText == null || string.IsNullOrEmpty(fullSongName)) return;
        
        if (fullSongName.Length <= maxCharacters)
        {
            songNameText.text = fullSongName;
            return;
        }
        
        scrollTimer += Time.deltaTime;
        
        if (!isScrolling && scrollTimer >= scrollDelay)
        {
            isScrolling = true;
            scrollTimer = 0f;
        }
        
        if (isScrolling && scrollTimer >= (1f / scrollSpeed))
        {
            scrollTimer = 0f;
            scrollPosition++;
            
            if (scrollPosition > fullSongName.Length - maxCharacters)
            {
                scrollPosition = 0;
                isScrolling = false;
                scrollTimer = -scrollDelay;
            }
            
            UpdateScrollingText();
        }
    }
    
    private void UpdateScrollingText()
    {
        if (songNameText == null || string.IsNullOrEmpty(fullSongName)) return;
        
        if (fullSongName.Length <= maxCharacters)
        {
            songNameText.text = fullSongName;
        }
        else
        {
            string displayText = fullSongName.Substring(scrollPosition, Mathf.Min(maxCharacters, fullSongName.Length - scrollPosition));
            
            if (displayText.Length < maxCharacters)
            {
                int remainingChars = maxCharacters - displayText.Length;
                displayText += new string(' ', 3);
                if (remainingChars > 3)
                {
                    displayText += fullSongName.Substring(0, remainingChars - 3);
                }
            }
            
            songNameText.text = displayText;
        }
    }
    
    private void ApplyVolumeSettings()
    {
        if (PersistentAudioManager.Instance != null)
        {
            AudioSource audioSource = PersistentAudioManager.Instance.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.volume = isMuted ? 0f : savedVolume;
            }
        }
    }
    
    public void OnVolumeChanged(float value)
    {
        savedVolume = value;
        
        if (!isMuted)
        {
            ApplyVolumeSettings();
        }
        
        SaveSettings();
    }
    
    public void ToggleSound()
    {
        isMuted = !isMuted;
        ApplyVolumeSettings();
        UpdateMuteButton();
        SaveSettings();
    }
    
    public void OnChangeSongClicked()
    {
        if (RadioManager.Instance != null)
        {
            RadioManager.Instance.PlayNextSong();
        }
    }
    
    // Test function - you can call this from a button or console
    public void ResetToAutoMode()
    {
        if (RadioManager.Instance != null)
        {
            RadioManager.Instance.ResetToAutoMode();
        }
    }
    
    public void OnSensitivityChanged(float value)
    {
        savedSensitivity = value;
        SaveSettings();
    }
    
    public void ResetSensitivity()
    {
        savedSensitivity = defaultSensitivity;
        if (sensitivitySlider != null)
            sensitivitySlider.value = savedSensitivity;
        SaveSettings();
    }

    public void ResetSound()
    {
        savedVolume = defaultVolume;
        isMuted = defaultMuteState;
        
        ApplyVolumeSettings();
        UpdateUI();
        SaveSettings();
    }
    
    public void SetVolume(float volume)
    {
        savedVolume = Mathf.Clamp01(volume);
        if (volumeSlider != null)
            volumeSlider.value = savedVolume;
        ApplyVolumeSettings();
        SaveSettings();
    }
    
    public float GetVolume()
    {
        return savedVolume;
    }
    
    public bool IsMuted()
    {
        return isMuted;
    }
    
    public void SetMuted(bool muted)
    {
        isMuted = muted;
        ApplyVolumeSettings();
        UpdateMuteButton();
        SaveSettings();
    }
    
    // Sensitivity methods for external access
    public void SetSensitivity(float sensitivity)
    {
        savedSensitivity = Mathf.Clamp(sensitivity, 0.1f, 3.0f);
        if (sensitivitySlider != null)
            sensitivitySlider.value = savedSensitivity;
        SaveSettings();
    }
    
    public float GetSensitivity()
    {
        return savedSensitivity;
    }
}