using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentAudioManager : MonoBehaviour
{
    // Singleton instance
    public static PersistentAudioManager Instance { get; private set; }
    
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        
        // This is the key line that makes the object persist across scene loads
        DontDestroyOnLoad(gameObject);
    }
    
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;
            
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();
    }
    
    public void StopMusic()
    {
        audioSource.Stop();
    }
}