using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Linq;
using TMPro;

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

    // caches the room manager so we can read roomSlots, maxConnections and all that important stuff
    private NetworkRoomManager roomManager;
    private MyRoomPlayer       localPlayer; // reference to *our* player object (the client that’s running this script) 

    void Awake()
    {
        // grab the room manager so we can access its properties
        // this is a bit of a hack, but it’s the easiest way to get the room manager in this case 
        roomManager = NetworkManager.singleton as NetworkRoomManager;

        // set up the buttons to call our functions when clicked
        readyButton.onClick.AddListener(OnReadyClicked);
        leaveButton.onClick .AddListener(OnLeaveClicked);
    }

    void OnEnable()
    {
        // subscribe to events from MyRoomPlayer so we can react to them
        MyRoomPlayer.LocalPlayerCreated  += OnLocalPlayerCreated;
        MyRoomPlayer.ClientEntered       += RefreshPlayerList;
        MyRoomPlayer.ClientReadyChanged  += OnClientReadyChanged;
        MyRoomPlayer.ClientLeft          += RefreshPlayerList;
    }
                                                                                //mostly understadn this code, but not all of it lol
    void OnDisable()
    {
        // unsubscribe when this UI goes away
        MyRoomPlayer.LocalPlayerCreated  -= OnLocalPlayerCreated;
        MyRoomPlayer.ClientEntered       -= RefreshPlayerList;
        MyRoomPlayer.ClientReadyChanged  -= OnClientReadyChanged;
        MyRoomPlayer.ClientLeft          -= RefreshPlayerList;
    }

    void Start()
    {
        // initial setup fills the list and displays room info 
        RefreshPlayerList();
        UpdateRoomInfoDisplay();
    }

    // called when our local MyRoomPlayer is ready for us to hook up
    void OnLocalPlayerCreated(MyRoomPlayer player)
    {
        localPlayer = player;
        // set the ready button label correctly from the start to show the switch state for polayers
        UpdateReadyButtonText(player.PlayerReady);
    }

    // someone (maybe us, maybe another player) toggled ready
    void OnClientReadyChanged(MyRoomPlayer player)
    {
        // if it was us, update our button text
        if (player == localPlayer)
            UpdateReadyButtonText(player.PlayerReady);

        // and always refresh the whole list to show everyone’s status
        RefreshPlayerList();
    }

    // these are the functions that are called when the buttons are clicked
    void OnReadyClicked() => localPlayer?.OnReadyButtonClicked();
    void OnLeaveClicked() => localPlayer?.OnLeaveButtonClicked();

    // completely rebuilds the player list UI
    void RefreshPlayerList(MyRoomPlayer _ = null)
    {
        // clear out old entries
        foreach (Transform t in playerListContainer)
            Destroy(t.gameObject);

        // grab the current list of room slots
        var slots = roomManager.roomSlots.ToList();
        int maxSlots = roomManager.maxConnections;

        // spawn a row for each potential slot (max is set to 4 so there will alsways bne 4 slots)
        for (int i = 0; i < maxSlots; i++)
        {
            var entry = Instantiate(playerListItemPrefab, playerListContainer);
            if (i < slots.Count && slots[i] is MyRoomPlayer p)
                entry.Set(p.playerName, p.PlayerReady); // show actual player
            else
                entry.Set("Empty Slot", false);         // placeholder for empty slots
        }
    }

    // update the ready button’s text so it shows the *action* the polayer will take if clicked
    void UpdateReadyButtonText(bool ready)
    {
        // if we’re already ready, button says “Unready”, otherwise “Ready”
        var label = ready ? "Unready" : "Ready";
        var txt   = readyButton.GetComponentInChildren<TMP_Text>();
        if (txt != null)
            txt.text = label;
    }

    // call this whenever the room name or port changes (e.g. start or rename)
    public void UpdateRoomInfoDisplay()
    {
        // show host vs client room name
        if (roomNameText != null)
        {
            bool isHost = NetworkServer.active;
            string nameToShow = isHost
                ? MyRoomManager.Singleton.RoomName
                : MyRoomManager.Singleton.DesiredRoomName;
            roomNameText.text = $"Room: {nameToShow}";
        }

        // this is the port that the server is listening on, and the client will connect to.. sets the port so the player can tell other players what port to connect to
        var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
        if (portText != null && tp != null)
            portText.text = $"Port: {tp.port}";
    }
}
