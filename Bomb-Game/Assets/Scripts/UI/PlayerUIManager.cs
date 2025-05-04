using UnityEngine;
using System.Collections;

public class PlayerUIManager : MonoBehaviour
{
    public PlayerUIPanel player1Panel;
    public PlayerUIPanel player2Panel;
    public PlayerUIPanel player3Panel;
    public PlayerUIPanel player4Panel;

    private PlayerUIPanel[] panels;

    void Awake()
    {
        panels = new PlayerUIPanel[] { player1Panel, player2Panel, player3Panel, player4Panel };
    }

    void Start()
    {
        StartCoroutine(SetupUI());
    }

    private IEnumerator SetupUI()
{
    yield return null; // Wait one frame to ensure all players are registered

    if (GameManager.Instance != null)
    {
        int playerCount = GameManager.Instance.activePlayers.Count;
        Debug.Log($"Setting up UI for {playerCount} players");

        for (int i = 0; i < panels.Length; i++)
        {
            if (i < playerCount)
            {
                panels[i].gameObject.SetActive(true);
                panels[i].Initialize(i); // Call Initialize with playerID (0-based)
            }
            else
            {
                panels[i].gameObject.SetActive(false);
            }
        }

        foreach (var player in GameManager.Instance.activePlayers)
        {
            var lifeManager = player.GetComponent<PlayerLifeManager>();
            if (lifeManager != null)
            {
                lifeManager.OnLifeChanged += (id, lives) => UpdatePlayerLives(id, lives);
                lifeManager.OnPlayerEliminated += HandlePlayerEliminated;
                UpdatePlayerLives(lifeManager.PlayerID, lifeManager.CurrentLives);
            }
        }
    }
    else
    {
        Debug.LogError("GameManager instance is null");
    }
}

    private void UpdatePlayerLives(int playerID, int lives)
    {
        int index = playerID - 1;
        if (index >= 0 && index < panels.Length)
        {
            panels[index].SetLives(lives);
        }
    }

    private void HandlePlayerEliminated(int playerID)
    {
        // Optional: Gray out panel or add elimination visuals here
    }
}