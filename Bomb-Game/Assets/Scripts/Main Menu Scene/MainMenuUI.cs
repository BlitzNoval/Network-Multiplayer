using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Mirror;
using System.Collections;

public class MainMenuUI : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField playerNameInput; 
    public TMP_InputField roomNameInput;   
    public TMP_InputField portInput;       
    public TMP_InputField ipAddressInput;

    [Header("Buttons")]
    public Button createButton;          
    public Button joinButton;            

    [Header("Invalid Room Panel")]
    public GameObject invalidRoomPanel;  
    public TMP_Text countdownText;       

    void Start()
    {
        createButton.interactable = false;
        joinButton.interactable   = false;

        playerNameInput.onValueChanged.AddListener(_ => Validate());
        roomNameInput.onValueChanged.AddListener(_ => Validate());
        portInput.onValueChanged.AddListener(_ => Validate());
        ipAddressInput.onValueChanged.AddListener(_ => Validate()); // NEW

        createButton.onClick.AddListener(OnCreateRoom);
        joinButton.onClick.AddListener(OnJoinRoom);

        if (invalidRoomPanel != null)
            invalidRoomPanel.SetActive(false);
    }

    void Validate()
    {
        bool baseOk = !string.IsNullOrWhiteSpace(playerNameInput.text) &&
                      !string.IsNullOrWhiteSpace(roomNameInput.text) &&
                      int.TryParse(portInput.text, out int p) && p > 0 && p <= 65535;

        // Create button: only needs player name, room name, and port
        createButton.interactable = baseOk;

        joinButton.interactable = baseOk && !string.IsNullOrWhiteSpace(ipAddressInput.text);
    }

    void OnCreateRoom()
    {
        var rm = MyRoomManager.Singleton;
        rm.RoomName = roomNameInput.text;
        rm.DesiredRoomName = roomNameInput.text;
        MyRoomPlayer.LocalPlayerName = playerNameInput.text;

        if (int.TryParse(portInput.text, out int port))
        {
            var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
            tp.port = (ushort)port;
        }
        else
        {
            Debug.LogError("Invalid port entered!");
        }

        NetworkManager.singleton.StartHost();
        Debug.Log($"Hosting room '{rm.RoomName}' as '{MyRoomPlayer.LocalPlayerName}' on port {portInput.text}");
    }

    void OnJoinRoom()
    {
        MyRoomManager.Singleton.DesiredRoomName = roomNameInput.text;
        MyRoomPlayer.LocalPlayerName = playerNameInput.text;

        if (int.TryParse(portInput.text, out int port))
        {
            var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
            tp.port = (ushort)port;
        }
        else
        {
            Debug.LogError("Invalid port entered!");
        }

        NetworkManager.singleton.networkAddress = ipAddressInput.text;

        NetworkManager.singleton.StartClient();
        Debug.Log($"Joining room '{roomNameInput.text}' as '{playerNameInput.text}' on port {portInput.text} at IP {ipAddressInput.text}");
    }

    public void ShowInvalidRoomPanel()
    {
        if (invalidRoomPanel == null)
        {
            Debug.LogError("InvalidRoomPanel is not assigned!");
            return;
        }
        invalidRoomPanel.SetActive(true);
        StartCoroutine(CountdownAndHide());
    }

    IEnumerator CountdownAndHide()
    {
        for (int i = 3; i >= 1; i--)
        {
            countdownText.text = $"Closing in {i}...";
            yield return new WaitForSeconds(1f);
        }
        invalidRoomPanel.SetActive(false);
    }
}