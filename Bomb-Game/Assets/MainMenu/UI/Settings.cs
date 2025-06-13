using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public Slider volumeSlider;
    public Button toggleSoundButton;
    public Button changeSongButton;
    public Button resetSoundButton;
    public TextMeshProUGUI songNameText;
    
    [Header("Video Settings")]
    public Button fullscreenButton;
    public Button windowedButton;
    public Button borderlessButton;
    public Button resetVideoButton;
    
    [Header("Audio Configuration")]
    public List<AudioClip> availableSongs = new List<AudioClip>();
    public Sprite mutedSprite;
    public Sprite unmutedSprite;
    
    [Header("Default Values")]
    public float defaultVolume = 0.5f;
    public bool defaultMuteState = false;
    public int defaultSongIndex = 0;
    
    [Header("Playlist Settings")]
    public bool autoAdvanceToNextSong = true; // Set this to true if you want automatic progression
    
    [Header("Text Display Settings")]
    public int maxCharacters = 7;
    public float scrollSpeed = 1f;
    public float scrollDelay = 2f; // Delay before scrolling starts
    
    // Private variables
    private int currentSongIndex = 0;
    private bool isMuted = false;
    private float savedVolume;
    private string fullSongName = "";
    private bool isScrolling = false;
    private int scrollPosition = 0;
    private float scrollTimer = 0f;
    
    // PlayerPrefs keys
    private const string VOLUME_KEY = "GameVolume";
    private const string MUTE_KEY = "GameMuted";
    private const string SONG_INDEX_KEY = "CurrentSongIndex";
    
    private void Start()
    {
        InitializeSettings();
        SetupButtonListeners();
        LoadSettings();
        UpdateUI();
        
        // Start checking for song completion if auto-advance is enabled
        if (autoAdvanceToNextSong)
        {
            InvokeRepeating(nameof(CheckSongCompletion), 1f, 1f);
        }
    }
    
    private void InitializeSettings()
    {
        // Ensure we have songs available
        if (availableSongs.Count == 0)
        {
            Debug.LogWarning("No songs assigned to SettingsManager!");
            return;
        }
        
        // Set default song index within bounds
        if (defaultSongIndex >= availableSongs.Count)
            defaultSongIndex = 0;
            
        currentSongIndex = defaultSongIndex;
        savedVolume = defaultVolume;
    }
    
    private void SetupButtonListeners()
    {
        // Audio button listeners
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            
        if (toggleSoundButton != null)
            toggleSoundButton.onClick.AddListener(ToggleSound);
            
        if (changeSongButton != null)
            changeSongButton.onClick.AddListener(NextSong);
            
        if (resetSoundButton != null)
            resetSoundButton.onClick.AddListener(ResetSound);
        
        // Video button listeners
        if (fullscreenButton != null)
            fullscreenButton.onClick.AddListener(SetFullscreen);
            
        if (windowedButton != null)
            windowedButton.onClick.AddListener(SetWindowed);
            
        if (borderlessButton != null)
            borderlessButton.onClick.AddListener(SetBorderless);
            
        if (resetVideoButton != null)
            resetVideoButton.onClick.AddListener(ResetVideo);
    }
    
    private void LoadSettings()
    {
        // Load audio settings
        savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, defaultVolume);
        isMuted = PlayerPrefs.GetInt(MUTE_KEY, defaultMuteState ? 1 : 0) == 1;
        currentSongIndex = PlayerPrefs.GetInt(SONG_INDEX_KEY, defaultSongIndex);
        
        // Ensure song index is within bounds
        if (currentSongIndex >= availableSongs.Count)
            currentSongIndex = defaultSongIndex;
            
        // Apply settings
        ApplyVolumeSettings();
        PlayCurrentSong();
    }
    
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(VOLUME_KEY, savedVolume);
        PlayerPrefs.SetInt(MUTE_KEY, isMuted ? 1 : 0);
        PlayerPrefs.SetInt(SONG_INDEX_KEY, currentSongIndex);
        PlayerPrefs.Save();
    }
    
    private void UpdateUI()
    {
        // Update volume slider
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
        }
        
        // Update mute button
        UpdateMuteButton();
        
        // Update song name
        UpdateSongName();
    }
    
    private void UpdateMuteButton()
    {
        if (toggleSoundButton != null)
        {
            // Update button text
            TextMeshProUGUI buttonText = toggleSoundButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isMuted ? "MUTED" : "UNMUTED";
            }
            
            // Update button image/sprite
            Image buttonImage = toggleSoundButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = isMuted ? mutedSprite : unmutedSprite;
            }
        }
    }

        private void UpdateSongName()
    {
        if (songNameText != null && availableSongs.Count > 0 && currentSongIndex < availableSongs.Count)
        {
            fullSongName = availableSongs[currentSongIndex].name;
            
            // Reset scrolling variables
            scrollPosition = 0;
            isScrolling = false;
            scrollTimer = 0f;
            
            // Update the display immediately
            UpdateScrollingText();
        }
    }
    
    private void Update()
    {
        HandleTextScrolling();
    }
    
    private void HandleTextScrolling()
    {
        if (songNameText == null || string.IsNullOrEmpty(fullSongName)) return;
        
        // If text is short enough, no need to scroll
        if (fullSongName.Length <= maxCharacters)
        {
            songNameText.text = fullSongName;
            return;
        }
        
        // Handle scrolling timer
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
            
            // Reset scroll position when we've scrolled through the entire text
            if (scrollPosition > fullSongName.Length - maxCharacters)
            {
                scrollPosition = 0;
                isScrolling = false;
                scrollTimer = -scrollDelay; // Add delay before next scroll cycle
            }
            
            // Update displayed text
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
            // Create scrolling text effect
            string displayText = fullSongName.Substring(scrollPosition, Mathf.Min(maxCharacters, fullSongName.Length - scrollPosition));
            
            // If we're near the end and don't have enough characters, pad with spaces and start of text
            if (displayText.Length < maxCharacters)
            {
                int remainingChars = maxCharacters - displayText.Length;
                displayText += new string(' ', 3); // Add some spaces
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
    
    private void PlayCurrentSong()
    {
        if (PersistentAudioManager.Instance != null && availableSongs.Count > 0 && currentSongIndex < availableSongs.Count)
        {
            Debug.Log($"Playing song: {availableSongs[currentSongIndex].name} (Index: {currentSongIndex})");
            PersistentAudioManager.Instance.PlayMusic(availableSongs[currentSongIndex], !autoAdvanceToNextSong);
        }
    }
    
    private void CheckSongCompletion()
    {
        if (!autoAdvanceToNextSong || PersistentAudioManager.Instance == null) return;
        
        AudioSource audioSource = PersistentAudioManager.Instance.GetComponent<AudioSource>();
        if (audioSource != null && !audioSource.isPlaying && availableSongs.Count > 1)
        {
            // Song finished, advance to next
            NextSong();
        }
    }
    
    // Audio Methods
    public void OnVolumeChanged(float value)
    {
        savedVolume = value;
        
        // If not muted, apply the volume immediately
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
    
    public void NextSong()
    {
        if (availableSongs.Count <= 1) return;
        
        currentSongIndex = (currentSongIndex + 1) % availableSongs.Count;
        Debug.Log($"Changed Song: Moving to index {currentSongIndex} - {availableSongs[currentSongIndex].name}");
        PlayCurrentSong();
        UpdateSongName();
        SaveSettings();
    }
    
    public void ResetSound()
    {
        // Reset to default values
        savedVolume = defaultVolume;
        isMuted = defaultMuteState;
        currentSongIndex = defaultSongIndex;
        
        // Apply and update UI
        ApplyVolumeSettings();
        PlayCurrentSong();
        UpdateUI();
        SaveSettings();
    }
    
    // Video Methods
    public void SetFullscreen()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen);
    }
    
    public void SetWindowed()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
    }
    
    public void SetBorderless()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
    }
    
    public void ResetVideo()
    {
        SetFullscreen();
    }
    
    // Public methods for external access
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
    
    public void PlaySong(int index)
    {
        if (index >= 0 && index < availableSongs.Count)
        {
            currentSongIndex = index;
            PlayCurrentSong();
            UpdateSongName();
            SaveSettings();
        }
    }
    
    public string GetCurrentSongName()
    {
        if (availableSongs.Count > 0 && currentSongIndex < availableSongs.Count)
            return availableSongs[currentSongIndex].name;
        return "No Song";
    }
}