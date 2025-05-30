using UnityEngine;
using TMPro;
using Mirror;

public class PlayerNameDisplay : NetworkBehaviour
{
    [SerializeField] private GameObject namePanel;

    public override void OnStartClient()
    {
        base.OnStartClient();
        var playerInfo = GetComponent<PlayerInfo>();
        if (playerInfo != null && namePanel != null)
        {
            namePanel.GetComponentInChildren<TextMeshPro>().text = playerInfo.playerName;
            if (isLocalPlayer)
            {
                namePanel.SetActive(false);
            }
        }
    }
}