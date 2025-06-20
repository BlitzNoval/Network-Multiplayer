using UnityEngine;
using System.Collections;

public class BombEffects : MonoBehaviour 
{
    [Header("Explosion Effects")]
    public ParticleSystem explosionVFXPrefab;
    
    public AudioClip explosionSound;
    
    public AudioSource audioSource;
    
    public float shakeDuration = 0.5f;
    
    public float shakeMagnitude = 0.1f;
    
    [Range(0f, 0.1f)]
    public float audioPreloadTime = 0.05f;
    
    private bool isPlayingEffects = false;
    
    public bool IsPlayingEffects => isPlayingEffects;

    void Awake() 
    {
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
            audioSource.spatialBlend = 1.0f;
        }
    }

    public void PlayExplosionEffects() 
    {
        if (isPlayingEffects) 
        {
            Debug.Log("PlayExplosionEffects already running, ignoring duplicate call", this);
            return;
        }
        
        isPlayingEffects = true;
        
        Debug.Log("PlayExplosionEffects called", this);
        Vector3 position = transform.position;

        GameObject tempAudio = null;
        if (explosionSound != null) 
        {
            tempAudio = new GameObject("TempExplosionAudio");
            tempAudio.transform.position = position;
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            
            tempSource.clip = explosionSound;
            tempSource.spatialBlend = 1.0f;
            tempSource.priority = 0;
            tempSource.volume = 1.0f;
            
            tempSource.bypassEffects = true;
            tempSource.bypassListenerEffects = true;
            tempSource.bypassReverbZones = true;
            
            tempSource.PlayScheduled(AudioSettings.dspTime + audioPreloadTime);
            
            float clipLength = explosionSound.length;
            Destroy(tempAudio, clipLength + 0.2f);
        }
        
        if (explosionVFXPrefab != null) 
        {
            ParticleSystem vfx = Instantiate(explosionVFXPrefab, position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, vfx.main.duration);
        }
        
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