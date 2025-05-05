using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    public void GoBack()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        if (currentIndex > 0)
        {
            SceneManager.LoadScene(currentIndex - 1);
        }
        else
        {
            Debug.LogWarning("No previous scene to go back to.");
        }
    }
}
