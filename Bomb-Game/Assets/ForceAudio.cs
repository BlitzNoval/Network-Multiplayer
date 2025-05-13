using UnityEngine;

public class ForceAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip testClip;

    void Start()
    {
        if (audioSource == null || testClip == null)
        {
            Debug.LogError("AudioSource or AudioClip not assigned!");
            return;
        }
        audioSource.PlayOneShot(testClip);
        Debug.Log("Playing test audio...");
    }
}