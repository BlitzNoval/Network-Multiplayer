using UnityEngine;
using UnityEngine.SceneManagement;

public class RadioManager : MonoBehaviour
{
    public static RadioManager Instance { get; private set; }
    
    [Header("Scene-Specific Music")]
    [SerializeField] private AudioClip mainMenuSong;
    [SerializeField] private AudioClip roomSceneSong;
    
    [Header("Map-Specific Music")]
    [SerializeField] private AudioClip cityMapSong;
    [SerializeField] private AudioClip islandMapSong;
    [SerializeField] private AudioClip shipMapSong;
    
    [Header("All Available Songs for Manual Selection")]
    [SerializeField] private AudioClip[] allSongs;
    
    private bool manualOverride = false;
    private string currentSceneName;
    private string currentMapName;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void Start()
    {
        currentSceneName = SceneManager.GetActiveScene().name;
        PlaySceneSpecificMusic();
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        
        if (!manualOverride)
        {
            PlaySceneSpecificMusic();
        }
        
        if (currentSceneName == "Game")
        {
            Invoke(nameof(CheckForSpawnedMap), 0.5f);
        }
    }
    
    private void CheckForSpawnedMap()
    {
        if (manualOverride) return;
        
        string selectedMap = "";
        
        if (MyRoomManager.Singleton != null)
        {
            selectedMap = MyRoomManager.Singleton.selectedMapName;
        }
        
        if (!string.IsNullOrEmpty(selectedMap) && selectedMap != currentMapName)
        {
            currentMapName = selectedMap;
            PlayMapSpecificMusic(selectedMap);
        }
    }
    
    private void PlaySceneSpecificMusic()
    {
        AudioClip songToPlay = null;
        
        switch (currentSceneName)
        {
            case "MainMenu":
                songToPlay = mainMenuSong;
                break;
            case "Room":
                songToPlay = roomSceneSong;
                break;
            case "Game":
                if (!string.IsNullOrEmpty(currentMapName))
                {
                    PlayMapSpecificMusic(currentMapName);
                    return;
                }
                else
                {
                    Invoke(nameof(CheckForSpawnedMap), 1f);
                    return;
                }
        }
        
        if (songToPlay != null && PersistentAudioManager.Instance != null)
        {
            PersistentAudioManager.Instance.PlayMusic(songToPlay, true);
        }
    }
    
    private void PlayMapSpecificMusic(string mapName)
    {
        AudioClip songToPlay = null;
        
        switch (mapName.ToLower())
        {
            case "city":
            case "skyscraper":
                songToPlay = cityMapSong;
                break;
            case "island":
                songToPlay = islandMapSong;
                break;
            case "ship":
                songToPlay = shipMapSong;
                break;
        }
        
        if (songToPlay != null && PersistentAudioManager.Instance != null)
        {
            PersistentAudioManager.Instance.PlayMusic(songToPlay, true);
        }
    }
    
    public void PlayNextSong()
    {
        if (allSongs == null || allSongs.Length == 0) return;
        
        manualOverride = true;
        
        AudioClip currentClip = null;
        if (PersistentAudioManager.Instance != null)
        {
            AudioSource audioSource = PersistentAudioManager.Instance.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                currentClip = audioSource.clip;
            }
        }
        
        int currentIndex = -1;
        for (int i = 0; i < allSongs.Length; i++)
        {
            if (allSongs[i] == currentClip)
            {
                currentIndex = i;
                break;
            }
        }
        
        int nextIndex = (currentIndex + 1) % allSongs.Length;
        
        if (PersistentAudioManager.Instance != null)
        {
            PersistentAudioManager.Instance.PlayMusic(allSongs[nextIndex], true);
        }
    }
    
    public void ResetToSceneMusic()
    {
        manualOverride = false;
        PlaySceneSpecificMusic();
    }
    
    public AudioClip GetCurrentSong()
    {
        if (PersistentAudioManager.Instance != null)
        {
            AudioSource audioSource = PersistentAudioManager.Instance.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                return audioSource.clip;
            }
        }
        return null;
    }
    
    public string GetCurrentSongName()
    {
        AudioClip currentClip = GetCurrentSong();
        return currentClip != null ? currentClip.name : "No Song";
    }
    
    public AudioClip[] GetAllSongs()
    {
        return allSongs;
    }
    
    public bool IsManualOverride()
    {
        return manualOverride;
    }
}