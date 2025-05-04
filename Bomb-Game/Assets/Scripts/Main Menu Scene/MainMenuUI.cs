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

    [Header("Buttons")]
    public Button createButton;          
    public Button joinButton;            

    [Header("Invalid Room Panel")]
    public GameObject invalidRoomPanel;  
    public TMP_Text    countdownText;       

    void Start()
    {
        // start with buttons disabled until all fields have valid input so players cant try join or create a room without entering the required info
        createButton.interactable = false;
        joinButton.interactable   = false;

        // watches the inputs, will run Validate() whenever any text changes 
        playerNameInput.onValueChanged.AddListener(_ => Validate());
        roomNameInput  .onValueChanged.AddListener(_ => Validate());
        portInput      .onValueChanged.AddListener(_ => Validate());

        // connects the button clicks to our methods
        createButton.onClick.AddListener(OnCreateRoom);
        joinButton  .onClick.AddListener(OnJoinRoom);

        // make sure our "room not found" panel is hidden at first (just in case its set in the inspector anyways)
        if (invalidRoomPanel != null)
            invalidRoomPanel.SetActive(false);
    }

    // check that name, room, and port are all filled in and tyhe port is valid
    void Validate()
    {
        bool ok =
            !string.IsNullOrWhiteSpace(playerNameInput.text) &&  // name isn't blank
            !string.IsNullOrWhiteSpace(roomNameInput.text)   &&  // room isn't blank
            int.TryParse(portInput.text, out int p)           &&  // port is a number
            p > 0 && p <= 65535;                                // and in valid range (learnt about ports for this lol)

        createButton.interactable = ok;
        joinButton.interactable   = ok;
    }

    // host a new game with the players entered name, room name and port
    void OnCreateRoom()
    {
        var rm = MyRoomManager.Singleton;
        rm.RoomName        = roomNameInput.text;    // tell the manager the chosen room name
        rm.DesiredRoomName = roomNameInput.text;    // same for DesiredRoomName
        MyRoomPlayer.LocalPlayerName = playerNameInput.text; // set our local name (player name)

        // set the port for the TelepathyTransport component (this is the port the server will listen on and clients will connect to)
        if (int.TryParse(portInput.text, out int port))
        {
            var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
            tp.port = (ushort)port; // Mirror wants ushort here so its here haha
        }
        else
        {
            Debug.LogError("Invalid port entered!");
        }

        // now actually start hosting
        NetworkManager.singleton.StartHost();
        Debug.Log($"Hosting room '{rm.RoomName}' as '{MyRoomPlayer.LocalPlayerName}' on port {portInput.text}");
    }

    // join an existing game
    void OnJoinRoom()
    {
        MyRoomManager.Singleton.DesiredRoomName = roomNameInput.text; // tell the manager which room we want
        MyRoomPlayer.LocalPlayerName            = playerNameInput.text; // set our local name

        // same port parsing as above, but this time we set the port for the client to connect to
        if (int.TryParse(portInput.text, out int port))
        {
            var tp = NetworkManager.singleton.GetComponent<TelepathyTransport>();
            tp.port = (ushort)port;
        }
        else
        {
            Debug.LogError("Invalid port entered!");
        }

        // connect as client
        NetworkManager.singleton.StartClient();
        Debug.Log($"Joining room '{roomNameInput.text}' as '{playerNameInput.text}' on port {portInput.text}");
    }

    // make this public so MyRoomManager can call it if connection/auth fails
    public void ShowInvalidRoomPanel()
    {
        if (invalidRoomPanel == null)
        {
            Debug.LogError("InvalidRoomPanel is not assigned!");
            return;
        }
        // show the panel and start the countdown till the panel disappears
        invalidRoomPanel.SetActive(true);
        StartCoroutine(CountdownAndHide());
    }

    // simple 3-2-1 countdown before hiding the panel again (simple player feedback)
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
