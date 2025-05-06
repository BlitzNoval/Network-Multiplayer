using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip lifeLostSound1; // For 3 to 2
    [SerializeField] private AudioClip lifeLostSound2; // For 2 to 1
    [SerializeField] private AudioClip lifeLostSound3; // For 1 to 0

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayLifeLostSound(int livesLeft)
    {
        switch (livesLeft)
        {
            case 2:
                audioSource.PlayOneShot(lifeLostSound1);
                break;
            case 1:
                audioSource.PlayOneShot(lifeLostSound2);
                break;
            case 0:
                audioSource.PlayOneShot(lifeLostSound3);
                break;
            default:
                Debug.LogWarning("No sound for this number of lives left.");
                break;
        }
    }
}