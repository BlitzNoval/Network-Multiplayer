// PlayerUIManager.cs
using System.Collections.Generic;
using UnityEngine;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance { get; private set; }

    [Tooltip("Assign your 4 PlayerUIPanel objects here in P1–P4 order.")]
    [SerializeField] private PlayerUIPanel[] panels;

    // Track which panels are currently in use
    readonly Dictionary<int, PlayerUIPanel> activePanels = new Dictionary<int, PlayerUIPanel>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Start with all panels hidden
            foreach (var panel in panels)
                panel.gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called by each PlayerLifeManager in OnStartClient.
    /// </summary>
    public void Register(PlayerLifeManager lifeManager)
    {
        int idx = lifeManager.playerNumber - 1;
        if (idx < 0 || idx >= panels.Length)
        {
            Debug.LogWarning($"[UI] Invalid panel index {idx} for player #{lifeManager.playerNumber}");
            return;
        }

        var panel = panels[idx];
        panel.gameObject.SetActive(true);

        // Initialize labels/value
        panel.Initialize(lifeManager.playerNumber);
        panel.SetLives(lifeManager.currentLives, lifeManager.currentLives);
        panel.SetKnockback(0f, lifeManager.knockbackMultiplier);

        // Wire up the hooks
        lifeManager.OnLivesChanged     += panel.SetLives;
        lifeManager.OnKnockbackChanged += panel.SetKnockback;

        activePanels[lifeManager.playerNumber] = panel;
    }

    /// <summary>
    /// Called by each PlayerLifeManager in OnStopClient to clean up.
    /// </summary>
    public void Unregister(PlayerLifeManager lifeManager)
    {
        if (activePanels.TryGetValue(lifeManager.playerNumber, out var panel))
        {
            panel.gameObject.SetActive(false);
            lifeManager.OnLivesChanged     -= panel.SetLives;
            lifeManager.OnKnockbackChanged -= panel.SetKnockback;
            activePanels.Remove(lifeManager.playerNumber);
        }
    }
}
