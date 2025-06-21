using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip lifeLostSound1;
    [SerializeField] private AudioClip lifeLostSound2;
    [SerializeField] private AudioClip lifeLostSound3; 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("AudioSource component missing on AudioManager.");
            }
        }
    }

    public void PlayLifeLostSound(int livesLeft)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("AudioSource is null in AudioManager.");
            return;
        }

        switch (livesLeft)
        {
            case 2:
                if (lifeLostSound1 != null) audioSource.PlayOneShot(lifeLostSound1);
                else Debug.LogWarning("lifeLostSound1 is not assigned.");
                break;
            case 1:
                if (lifeLostSound2 != null) audioSource.PlayOneShot(lifeLostSound2);
                else Debug.LogWarning("lifeLostSound2 is not assigned.");
                break;
            case 0:
                if (lifeLostSound3 != null) audioSource.PlayOneShot(lifeLostSound3);
                else Debug.LogWarning("lifeLostSound3 is not assigned.");
                break;
            default:
                Debug.LogWarning($"No sound for lives left: {livesLeft}");
                break;
        }
    }
}
