using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject playPanel;
    public GameObject settingsPanel;
    public GameObject controlsPanel;
    public GameObject howToWinPanel;
    public float animationDuration = 0.25f;

    private void Start()
    {
        playPanel.SetActive(false);
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(false);
        howToWinPanel.SetActive(false);
    }

    #region Button Callbacks

    public void OnPlayPressed()
    {
        ShowPanel(playPanel);
    }

    public void OnSettingsPressed()
    {
        ShowPanel(settingsPanel);
    }

    public void OnControlsPressed()
    {
        ShowPanel(controlsPanel);
    }

    public void OnHowToWinPressed()
    {
        ShowPanel(howToWinPanel);
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }

    #endregion

    #region Panel Close Methods

    public void ClosePlayPanel()
    {
        HidePanel(playPanel);
    }

    public void CloseSettingsPanel()
    {
        HidePanel(settingsPanel);
    }

    public void CloseControlsPanel()
    {
        HidePanel(controlsPanel);
    }

    public void CloseHowToWinPanel()
    {
        HidePanel(howToWinPanel);
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
}
