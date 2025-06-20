using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Linq;
using TMPro;
using System.Net;

public class RoomUI : MonoBehaviour
{
    [Header("UI References")]
    public Button readyButton;             
    public Button leaveButton;           
    public Transform playerListContainer;  
    public PlayerListItem playerListItemPrefab; 

    [Header("Room Info Display")]
    public TMP_Text roomNameText;           
    public TMP_Text portText;            
    public TMP_Text hostIPText;

    private NetworkRoomManager roomManager;
    private MyRoomPlayer localPlayer;

    void Awake()
    {
        roomManager = NetworkManager.singleton as NetworkRoomManager;
        readyButton.onClick.AddListener(OnReadyClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    void OnEnable()
    {
        MyRoomPlayer.LocalPlayerCreated += OnLocalPlayerCreated;
        MyRoomPlayer.ClientEntered += RefreshPlayerList;
        MyRoomPlayer.ClientReadyChanged += OnClientReadyChanged;
        MyRoomPlayer.ClientLeft += RefreshPlayerList;
    }

    void OnDisable()
    {
        MyRoomPlayer.LocalPlayerCreated -= OnLocalPlayerCreated;
        MyRoomPlayer.ClientEntered -= RefreshPlayerList;
        MyRoomPlayer.ClientReadyChanged -= OnClientReadyChanged;
        MyRoomPlayer.ClientLeft -= RefreshPlayerList;
    }

    void Start()
    {
        RefreshPlayerList();
        UpdateRoomInfoDisplay();
    }

    void OnLocalPlayerCreated(MyRoomPlayer player)
    {
        localPlayer = player;
        UpdateReadyButtonText(player.PlayerReady);
    }

    void OnClientReadyChanged(MyRoomPlayer player)
    {
        if (player == localPlayer)
            UpdateReadyButtonText(player.PlayerReady);
        RefreshPlayerList();
    }

    void OnReadyClicked() => localPlayer?.OnReadyButtonClicked();
    void OnLeaveClicked() => localPlayer?.OnLeaveButtonClicked();

    void RefreshPlayerList(MyRoomPlayer _ = null)
    {
        foreach (Transform t in playerListContainer)
            Destroy(t.gameObject);

        var slots = roomManager.roomSlots.ToList();
        int maxSlots = roomManager.maxConnections;

        for (int i = 0; i < maxSlots; i++)
        {
            var entry = Instantiate(playerListItemPrefab, playerListContainer);
            if (i < slots.Count && slots[i] is MyRoomPlayer p)
                entry.Set(p.playerName, p.PlayerReady);
            else
                entry.Set("Empty Slot", false);
        }
    }

    void UpdateReadyButtonText(bool ready)
    {
        var label = ready ? "Unready" : "Ready";
        var txt = readyButton.GetComponentInChildren<TMP_Text>();
        if (txt != null)
            txt.text = label;
    }

    public void UpdateRoomInfoDisplay()
    {
        if (roomNameText != null)
        {
            bool isHost = NetworkServer.active;
            string nameToShow = isHost ? MyRoomManager.Singleton.RoomName : MyRoomManager.Singleton.DesiredRoomName;
            roomNameText.text = $"Room: {nameToShow}";
        }

        var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
        if (portText != null && tp != null)
            portText.text = $"Port: {tp.port}";

        if (hostIPText != null)
        {
            if (NetworkServer.active)
                hostIPText.text = $"Host IP: {GetLocalIPAddress()}";
            else
                hostIPText.text = $"Connected to: {NetworkManager.singleton.networkAddress}";
        }
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
                return ip.ToString();
        }
        return "Unknown";
    }
}