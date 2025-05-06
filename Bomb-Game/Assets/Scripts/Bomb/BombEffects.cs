using UnityEngine;
using System.Collections;

public class BombEffects : MonoBehaviour 
{
    [Header("Explosion Effects")]
    [Tooltip("Particle system prefab for explosion VFX")]
    public ParticleSystem explosionVFXPrefab;
    
    [Tooltip("Audio clip for explosion sound")]
    public AudioClip explosionSound;
    
    [Tooltip("Audio source for playing explosion sound")]
    public AudioSource audioSource;
    
    [Tooltip("Camera shake duration in seconds")]
    public float shakeDuration = 0.5f;
    
    [Tooltip("Camera shake magnitude")]
    public float shakeMagnitude = 0.1f;
    
    [Tooltip("Pre-load buffer time for audio (seconds)")]
    [Range(0f, 0.1f)]
    public float audioPreloadTime = 0.05f;
    
    private bool isPlayingEffects = false;
    
    // Public getter for state syncing (intended for network synchronization)
    public bool IsPlayingEffects => isPlayingEffects;

    void Awake() 
    {
        // Ensure AudioSource exists
        if (audioSource == null) 
        {
            audioSource = GetComponentInChildren<AudioSource>();
            
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (audioSource != null)
        {
            audioSource.spatialBlend = 1.0f; // Full 3D sound
        }
    }

    // Play all explosion effects (intended for client-side call when networked)
    public void PlayExplosionEffects() 
    {
        // Check if already playing effects and prevent multiple calls
        if (isPlayingEffects) 
        {
            Debug.Log("PlayExplosionEffects already running, ignoring duplicate call", this);
            return;
        }
        
        isPlayingEffects = true;
        
        Debug.Log("PlayExplosionEffects called", this);
        // Cache position in case bomb is destroyed
        Vector3 position = transform.position;

        // Start audio playback immediately with priority
        GameObject tempAudio = null;
        if (explosionSound != null) 
        {
            tempAudio = new GameObject("TempExplosionAudio");
            tempAudio.transform.position = position;
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            
            // Configure audio for minimal delay
            tempSource.clip = explosionSound;
            tempSource.spatialBlend = 1.0f;
            tempSource.priority = 0; // Highest priority
            tempSource.volume = 1.0f;
            
            // Set minimal buffer size for faster processing
            tempSource.bypassEffects = true;
            tempSource.bypassListenerEffects = true;
            tempSource.bypassReverbZones = true;
            
            // Play immediately
            tempSource.PlayScheduled(AudioSettings.dspTime + audioPreloadTime);
            
            float clipLength = explosionSound.length;
            Destroy(tempAudio, clipLength + 0.2f);
        }
        
        // Play VFX slightly delayed to match audio if needed
        if (explosionVFXPrefab != null) 
        {
            ParticleSystem vfx = Instantiate(explosionVFXPrefab, position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, vfx.main.duration);
        }
        
        // Activate camera shake
        if (Camera.main != null) 
        {
            CameraShake shaker = Camera.main.GetComponent<CameraShake>();
            
            if (shaker == null)
            {
                shaker = Camera.main.gameObject.AddComponent<CameraShake>();
            }
            
            shaker.SetShakeAmount(shakeMagnitude);
            shaker.StartShake(shakeDuration);
        }
    
    }
}