using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add this for TextMeshPro
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject optionsPanel;
    public GameObject controlsPanel;
    public GameObject howToPlayPanel;

    [Header("Options")]
    public Slider soundSlider;
    public Button muteButton; // Use button for mute/unmute
    public TextMeshProUGUI muteButtonText; // Reference to the button's text

    [Header("Animation Settings")]
    public float animationDuration = 0.25f;

    private bool isMuted;

    private void Start()
    {
        // Ensure panels start inactive
        optionsPanel.SetActive(false);
        controlsPanel.SetActive(false);
        howToPlayPanel.SetActive(false);

        // Initialize sound settings
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        isMuted = PlayerPrefs.GetInt("Muted", 0) == 1;

        soundSlider.value = savedVolume;
        ApplyVolume(savedVolume, isMuted);

        soundSlider.onValueChanged.AddListener(OnSliderChanged);

        // Add listener to mute button
        muteButton.onClick.AddListener(ToggleMute);

        UpdateMuteButtonText();
    }

    #region Button Callbacks

    public void OnPlayPressed()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void OnOptionsPressed()
    {
        ShowPanel(optionsPanel);
    }

    public void OnControlsPressed()
    {
        ShowPanel(controlsPanel);
    }

    public void OnHowToPlayPressed()
    {
        ShowPanel(howToPlayPanel);
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }

    #endregion

    #region Panel Close Methods

    public void CloseOptionsPanel()
    {
        HidePanel(optionsPanel);
    }

    public void CloseControlsPanel()
    {
        HidePanel(controlsPanel);
    }

    public void CloseHowToPlayPanel()
    {
        HidePanel(howToPlayPanel);
    }

    #endregion

    #region Panel Animation Helpers

    private void ShowPanel(GameObject panel)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateOpen(panel));
    }

    private void HidePanel(GameObject panel)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateClose(panel));
    }

    private IEnumerator AnimateOpen(GameObject panel)
    {
        panel.transform.localScale = Vector3.zero;
        panel.SetActive(true);
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            panel.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null;
        }
        panel.transform.localScale = Vector3.one;
    }

    private IEnumerator AnimateClose(GameObject panel)
    {
        float elapsed = 0f;
        panel.transform.localScale = Vector3.one;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            panel.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            yield return null;
        }
        panel.SetActive(false);
    }

    #endregion

    #region Settings

    private void OnSliderChanged(float value)
    {
        ApplyVolume(value, isMuted);
    }

    private void ApplyVolume(float volume, bool isMuted)
    {
        AudioListener.volume = isMuted ? 0f : volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
        PlayerPrefs.SetInt("Muted", isMuted ? 1 : 0);
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        ApplyVolume(soundSlider.value, isMuted);
        UpdateMuteButtonText();
    }

    private void UpdateMuteButtonText()
    {
        if (muteButtonText != null)
        {
            muteButtonText.text = isMuted ? "Unmute" : "Mute";
        }
    }

    private void OnDestroy()
    {
        soundSlider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    #endregion
}
