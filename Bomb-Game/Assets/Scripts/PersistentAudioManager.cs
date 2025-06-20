using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentAudioManager : MonoBehaviour
{
    public static PersistentAudioManager Instance { get; private set; }
    
    private AudioSource audioSource;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        
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