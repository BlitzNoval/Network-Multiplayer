using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
    private string currentContext = "";
    
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
        CheckForMusicContext();
        StartCoroutine(ContinuousContextCheck());
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"RadioManager: Scene loaded - {scene.name}");
        
        // Reset manual override on scene change for consistency
        manualOverride = false;
        
        // Check for music context after a short delay to let objects spawn
        Invoke(nameof(CheckForMusicContext), 0.5f);
    }
    
    private void CheckForMusicContext()
    {
        if (manualOverride) return;
        
        string newContext = "";
        AudioClip songToPlay = null;
        
        // First check for scene markers
        GameObject sceneMarker = GameObject.FindWithTag("MainMenuMarker");
        if (sceneMarker != null)
        {
            newContext = "MainMenu";
            songToPlay = mainMenuSong;
            Debug.Log("RadioManager: Found MainMenu scene marker");
        }
        else
        {
            sceneMarker = GameObject.FindWithTag("RoomSceneMarker");
            if (sceneMarker != null)
            {
                newContext = "RoomScene";
                songToPlay = roomSceneSong;
                Debug.Log("RadioManager: Found RoomScene scene marker");
            }
        }
        
        // Then check for map markers (these override scene markers)
        GameObject mapMarker = GameObject.FindWithTag("city");
        if (mapMarker != null)
        {
            newContext = "city";
            songToPlay = cityMapSong;
            Debug.Log("RadioManager: Found city map marker");
        }
        else
        {
            mapMarker = GameObject.FindWithTag("island");
            if (mapMarker != null)
            {
                newContext = "island";
                songToPlay = islandMapSong;
                Debug.Log("RadioManager: Found island map marker");
            }
            else
            {
                mapMarker = GameObject.FindWithTag("ship");
                if (mapMarker != null)
                {
                    newContext = "ship";
                    songToPlay = shipMapSong;
                    Debug.Log("RadioManager: Found ship map marker");
                }
            }
        }
        
        // Play the song if context changed or if we found a valid song
        if (newContext != currentContext || songToPlay != null)
        {
            currentContext = newContext;
            
            if (songToPlay != null && PersistentAudioManager.Instance != null)
            {
                Debug.Log($"RadioManager: Playing song {songToPlay.name} for context '{currentContext}'");
                PersistentAudioManager.Instance.PlayMusic(songToPlay, true);
            }
            else if (songToPlay == null)
            {
                Debug.LogWarning($"RadioManager: No song assigned for context '{currentContext}'");
            }
        }
    }
    
    private IEnumerator ContinuousContextCheck()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f); // Check every 2 seconds
            
            if (!manualOverride)
            {
                CheckForMusicContext();
            }
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
            Debug.Log($"RadioManager: Manual override - playing {allSongs[nextIndex].name}");
            PersistentAudioManager.Instance.PlayMusic(allSongs[nextIndex], true);
        }
    }
    
    public void ResetToAutoMode()
    {
        Debug.Log("RadioManager: Resetting to automatic mode");
        manualOverride = false;
        CheckForMusicContext();
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
    
    public string GetCurrentContext()
    {
        return currentContext;
    }
}