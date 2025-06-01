using UnityEngine;
using TMPro;
using Mirror;

public class PlayerNameDisplay : NetworkBehaviour
{
    [SerializeField] private GameObject namePanel;
    [SerializeField] private Billboard billboard; // Optional: Reference to Billboard for setup

    public override void OnStartClient()
    {
        base.OnStartClient();
        var lifeManager = GetComponent<PlayerLifeManager>();
        if (lifeManager != null && namePanel != null)
        {
            string playerTag = $"P{lifeManager.PlayerNumber}";
            SetPlayerTag(playerTag);
            if (isLocalPlayer)
            {
                namePanel.SetActive(false); // Hide tag for local player
            }
            // Optional: Assign playerTransform to Billboard
            if (billboard != null)
            {
                billboard.playerTransform = transform; // Set to the player's transform
            }
        }
        else
        {
            Debug.LogError("PlayerLifeManager or namePanel is missing.", this);
        }
    }

    public void SetPlayerTag(string tag)
    {
        if (namePanel != null)
        {
            var textComponent = namePanel.GetComponentInChildren<TextMeshPro>();
            if (textComponent != null)
            {
                textComponent.text = tag;
            }
            else
            {
                Debug.LogError("TextMeshPro component not found in namePanel.", this);
            }
        }
    }
}