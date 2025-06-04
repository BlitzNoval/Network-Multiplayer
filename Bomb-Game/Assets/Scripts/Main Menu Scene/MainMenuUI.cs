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
        // Set default values
        if (playerNameInput != null) playerNameInput.text = "Player" + Random.Range(1000, 9999);
        if (roomNameInput != null) roomNameInput.text = "DefaultRoom";
        if (portInput != null) portInput.text = "7777";
        if (ipAddressInput != null) ipAddressInput.text = "192.168.39.116";

        createButton.interactable = false;
        joinButton.interactable = false;

        // Add input validation listeners
        if (playerNameInput != null) playerNameInput.onValueChanged.AddListener(_ => Validate());
        if (roomNameInput != null) roomNameInput.onValueChanged.AddListener(_ => Validate());
        if (portInput != null) portInput.onValueChanged.AddListener(_ => Validate());
        if (ipAddressInput != null) ipAddressInput.onValueChanged.AddListener(_ => Validate());

        // Add button click listeners
        if (createButton != null) createButton.onClick.AddListener(OnCreateRoom);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinRoom);

        if (invalidRoomPanel != null)
            invalidRoomPanel.SetActive(false);
    }

    bool Validate()
    {
        bool baseOk = playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text) &&
                      roomNameInput != null && !string.IsNullOrWhiteSpace(roomNameInput.text) &&
                      portInput != null && int.TryParse(portInput.text, out int p) && p > 0 && p <= 65535;

        createButton.interactable = baseOk;
        joinButton.interactable = baseOk && ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text);
        return baseOk;
    }

    void OnCreateRoom()
    {
        if (!Validate())
        {
            ShowInvalidRoomPanel();
            Debug.LogWarning("Create room failed: Invalid input fields");
            return;
        }

        // Force reset: stop host if server or client is active
        if (NetworkServer.active || NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();
        }

        var rm = MyRoomManager.Singleton;
        if (rm == null)
        {
            Debug.LogError("MyRoomManager.Singleton is null");
            ShowInvalidRoomPanel();
            return;
        }

        rm.RoomName = roomNameInput.text;
        rm.DesiredRoomName = roomNameInput.text;
        MyRoomPlayer.LocalPlayerName = playerNameInput.text;

        var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
        if (tp == null)
        {
            Debug.LogError("TelepathyTransport component missing on NetworkManager");
            ShowInvalidRoomPanel();
            return;
        }

        if (int.TryParse(portInput.text, out int port))
        {
            tp.port = (ushort)port;
        }
        else
        {
            Debug.LogError("Invalid port entered, using default 7777");
            tp.port = 7777;
        }

        NetworkManager.singleton.StartHost();
        Debug.Log($"Hosting room '{rm.RoomName}' as '{MyRoomPlayer.LocalPlayerName}' on port {tp.port}");
    }

    void OnJoinRoom()
    {
        if (!Validate())
        {
            ShowInvalidRoomPanel();
            Debug.LogWarning("Join room failed: Invalid input fields");
            return;
        }

        // Force reset: stop server and client if active
        if (NetworkServer.active)
        {
            NetworkManager.singleton.StopServer();
        }
        if (NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopClient();
        }

        var rm = MyRoomManager.Singleton;
        if (rm == null)
        {
            Debug.LogError("MyRoomManager.Singleton is null");
            ShowInvalidRoomPanel();
            return;
        }

        rm.DesiredRoomName = roomNameInput.text;
        MyRoomPlayer.LocalPlayerName = playerNameInput.text;
        NetworkManager.singleton.networkAddress = ipAddressInput.text;

        var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
        if (tp == null)
        {
            Debug.LogError("TelepathyTransport component missing on NetworkManager");
            ShowInvalidRoomPanel();
            return;
        }

        if (int.TryParse(portInput.text, out int port))
        {
            tp.port = (ushort)port;
        }
        else
        {
            Debug.LogError("Invalid port entered, using default 7777");
            tp.port = 7777;
        }

        NetworkManager.singleton.StartClient();
        Debug.Log($"Joining room '{rm.DesiredRoomName}' as '{MyRoomPlayer.LocalPlayerName}' on port {tp.port} at IP {NetworkManager.singleton.networkAddress}");
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
        if (countdownText == null)
        {
            Debug.LogWarning("CountdownText is not assigned!");
            invalidRoomPanel.SetActive(false);
            yield break;
        }

        for (int i = 3; i >= 1; i--)
        {
            countdownText.text = $"Closing in {i}...";
            yield return new WaitForSeconds(1f);
        }
        invalidRoomPanel.SetActive(false);
    }
}