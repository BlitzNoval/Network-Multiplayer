using UnityEngine;

public class SimplePanelActivator : MonoBehaviour
{
    [SerializeField] private GameObject panelToActivate;
    [SerializeField] private bool activateOnStart = false;
    [SerializeField] private float delayBeforeActivation = 0f;
    
    private void Start()
    {
        if (activateOnStart)
        {
            ActivatePanel();
        }
    }
    
    public void ActivatePanel()
    {
        if (delayBeforeActivation > 0f)
        {
            Invoke(nameof(ShowPanel), delayBeforeActivation);
        }
        else
        {
            ShowPanel();
        }
    }
    
    public void DeactivatePanel()
    {
        if (panelToActivate != null)
        {
            panelToActivate.SetActive(false);
        }
    }
    
    public void TogglePanel()
    {
        if (panelToActivate != null)
        {
            panelToActivate.SetActive(!panelToActivate.activeSelf);
        }
    }
    
    private void ShowPanel()
    {
        if (panelToActivate != null)
        {
            panelToActivate.SetActive(true);
        }
    }
}