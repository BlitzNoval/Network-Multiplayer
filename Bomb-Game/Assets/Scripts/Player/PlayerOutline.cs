using UnityEngine;
using Mirror;

public class PlayerOutline : NetworkBehaviour
{
    [SerializeField] private Outline outline;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        if (outline != null)
        {
            outline.enabled = true;
        }
        else
        {
            Debug.LogError("Outline component is missing on the player prefab.");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (outline != null && !isLocalPlayer)
        {
            outline.enabled = false;
        }
    }
}