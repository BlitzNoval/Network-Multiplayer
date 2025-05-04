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
    
    private bool isPlayingEffects = false;
    
    // Public getter for state syncing (intended for network synchronization)
    public bool IsPlayingEffects => isPlayingEffects;

    void Awake() 
    {
        // Ensure AudioSource exists
        if (audioSource == null) 
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    // Play all explosion effects (intended for client-side call when networked)
    public void PlayExplosionEffects() 
    {
        if (isPlayingEffects) return;
        isPlayingEffects = true;
        
        // Play VFX
        if (explosionVFXPrefab != null) 
        {
            ParticleSystem vfx = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, vfx.main.duration);
        }
        
        // Play sound
        if (explosionSound != null && audioSource != null) 
        {
            audioSource.PlayOneShot(explosionSound);
        }
        
        // Activate camera shake
        if (Camera.main != null) 
        {
            CameraShake shaker = Camera.main.GetComponent<CameraShake>();
            
            // Add CameraShake component if it doesn't exist
            if (shaker == null)
            {
                shaker = Camera.main.gameObject.AddComponent<CameraShake>();
            }
            
            // Configure shake magnitude (optional)
            shaker.SetShakeAmount(shakeMagnitude);
            
            // Start the shake
            shaker.StartShake(shakeDuration);
        }
        
        isPlayingEffects = false;
    }
}