using UnityEngine;
using TMPro;

public class PlayerListItem : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text readyText;

    public void Set(string playerName, bool ready)
    {
        nameText.text  = playerName;
        readyText.text = ready ? "Ready" : "Not Ready";
    }
}
