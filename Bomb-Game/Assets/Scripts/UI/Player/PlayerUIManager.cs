using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance { get; private set; }

    [SerializeField] private PlayerUIPanel[] panels;

    readonly Dictionary<int, PlayerUIPanel> activePanels = new Dictionary<int, PlayerUIPanel>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            foreach (var panel in panels)
                if (panel != null)
                    panel.gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (panels == null || panels.Length == 0 || panels.All(p => p == null))
            panels = GetComponentsInChildren<PlayerUIPanel>(true);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(PlayerLifeManager lifeManager)
    {
        if (lifeManager == null) return;

        int idx = lifeManager.PlayerNumber - 1;
        if (idx < 0 || idx >= panels.Length) return;

        var panel = panels[idx];
        if (panel == null) return;

        var playerInfo = lifeManager.GetComponent<PlayerInfo>();
        string playerName = playerInfo != null
            ? playerInfo.playerName
            : $"P{lifeManager.PlayerNumber}";

        panel.gameObject.SetActive(true);
        panel.Initialize(lifeManager.PlayerNumber);
        panel.SetPlayerName(playerName);
        panel.SetLives(lifeManager.CurrentLives, lifeManager.CurrentLives);
        panel.SetKnockback(0f, lifeManager.PercentageKnockback);

        lifeManager.OnLivesChanged                += panel.SetLives;
        lifeManager.OnKnockbackPercentageChanged  += panel.SetKnockback;

        activePanels[lifeManager.PlayerNumber] = panel;
    }

    public void Unregister(PlayerLifeManager lifeManager)
    {
        if (lifeManager == null) return;

        if (activePanels.TryGetValue(lifeManager.PlayerNumber, out var panel))
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
                lifeManager.OnLivesChanged               -= panel.SetLives;
                lifeManager.OnKnockbackPercentageChanged -= panel.SetKnockback;
            }
            activePanels.Remove(lifeManager.PlayerNumber);
        }
    }

    public void ResetPanels()
    {
        foreach (var panel in activePanels.Values)
            if (panel != null)
                panel.gameObject.SetActive(false);

        activePanels.Clear();
    }

    public PlayerUIPanel GetPanelForPlayer(int playerNumber)
    {
        activePanels.TryGetValue(playerNumber, out PlayerUIPanel panel);
        return panel;
    }
}