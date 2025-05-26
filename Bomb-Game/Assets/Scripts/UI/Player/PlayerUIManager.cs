using System.Collections.Generic;
using UnityEngine;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance { get; private set; }

    [Tooltip("Assign your 4 PlayerUIPanel objects here in P1–P4 order.")]
    [SerializeField] private PlayerUIPanel[] panels;

    readonly Dictionary<int, PlayerUIPanel> activePanels = new Dictionary<int, PlayerUIPanel>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            foreach (var panel in panels)
            {
                if (panel != null)
                    panel.gameObject.SetActive(false);
                else
                    Debug.LogError("PlayerUIPanel is null in panels array", this);
            }
            Debug.Log("PlayerUIManager initialized", this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Debug.Log("PlayerUIManager destroyed", this);
    }

    public void Register(PlayerLifeManager lifeManager)
    {
        if (lifeManager == null)
        {
            Debug.LogError("Register: lifeManager is null", this);
            return;
        }

        int idx = lifeManager.PlayerNumber - 1;
        Debug.Log($"Register: PlayerNumber={lifeManager.PlayerNumber}, idx={idx}", this);

        if (idx < 0 || idx >= panels.Length)
        {
            Debug.LogWarning($"[UI] Invalid panel index {idx} for player #{lifeManager.PlayerNumber}", this);
            return;
        }

        var panel = panels[idx];
        if (panel == null)
        {
            Debug.LogError($"Panel at index {idx} is null", this);
            return;
        }

        panel.gameObject.SetActive(true);
        panel.Initialize(lifeManager.PlayerNumber);
        panel.SetLives(lifeManager.CurrentLives, lifeManager.CurrentLives);
        panel.SetKnockback(0f, lifeManager.KnockbackMultiplier);

        lifeManager.OnLivesChanged += panel.SetLives;
        lifeManager.OnKnockbackChanged += panel.SetKnockback;

        activePanels[lifeManager.PlayerNumber] = panel;
        Debug.Log($"Registered player {lifeManager.PlayerNumber} to panel {idx}, activePanels={activePanels.Count}", this);
    }

    public void Unregister(PlayerLifeManager lifeManager)
    {
        if (lifeManager == null)
        {
            Debug.LogWarning("Unregister: lifeManager is null", this);
            return;
        }

        if (activePanels.TryGetValue(lifeManager.PlayerNumber, out var panel))
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
                lifeManager.OnLivesChanged -= panel.SetLives;
                lifeManager.OnKnockbackChanged -= panel.SetKnockback;
            }
            activePanels.Remove(lifeManager.PlayerNumber);
            Debug.Log($"Unregistered player {lifeManager.PlayerNumber}, activePanels={activePanels.Count}", this);
        }
    }

    public void ResetPanels()
    {
        foreach (var panel in activePanels.Values)
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
        }
        activePanels.Clear();
        Debug.Log("ResetPanels: Cleared all active panels", this);
    }
}