using UnityEngine;
using Mirror;
using System;

public class MyRoomPlayer : NetworkRoomPlayer
{
    // this is the name this player chose before joining the room
    // defaults to "Guest" so nobody crashes if they forget to type one
    public static string LocalPlayerName = "Guest";

    // sync the playerName across all clients
    // hook means OnPlayerNameChanged runs whenever it updates
    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName = "Guest";

    // these events are how our UI knows when stuff happens in the lobby
    public static event Action<MyRoomPlayer> LocalPlayerCreated;   // triggered when our local player object is ready
    public static event Action<MyRoomPlayer> ClientEntered;        // trigers when any player enters
    public static event Action<MyRoomPlayer> ClientReadyChanged;   // trigger when someone toggles ready/not-ready
    public static event Action<MyRoomPlayer> ClientLeft;           // trigger when someone leaves

    // track our own ready toggle locally
    private bool localReady = false;

    // shorthand to expose base class's ready flag and slot index to com with server
    // this is the slot index of this player in the room (0 = first player, 1 = second, etc) 
    public bool PlayerReady => readyToBegin;
    public int  PlayerIndex => index;

    // called once on our own client when our player object spawns
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        // send the player chosen name up to the server
        CmdSetPlayerName(LocalPlayerName);
        // tell UI "yo, my local player is here now!"
        LocalPlayerCreated?.Invoke(this);
    }

    // this is called on clients when the server updates playerName
    void OnPlayerNameChanged(string oldName, string newName)
    {
        // reuse ClientEntered to make UI refresh our entry
        ClientEntered?.Invoke(this);
    }

    // commands run on the server... here we set the SyncVar playerName
    [Command]
    void CmdSetPlayerName(string name)
    {
        playerName = name;
    }

    // called on all clients when any player joins the room to refresgh UI
    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        ClientEntered?.Invoke(this);
    }

    // called on all clients when someone's ready state switches
    public override void ReadyStateChanged(bool oldReady, bool newReady)
    {
        base.ReadyStateChanged(oldReady, newReady);
        ClientReadyChanged?.Invoke(this);
    }

    // called on all clients when someone leaves the room 
    public override void OnClientExitRoom()
    {
        base.OnClientExitRoom();
        ClientLeft?.Invoke(this);
    }

    // hook this up to the "Ready" button: toggles our ready flag and sends it to the server
    // the server then updates all clients with the new state
    public void OnReadyButtonClicked()
    {
        if (!isLocalPlayer) return;  // only the player control their own ready
        localReady = !localReady;
        CmdChangeReadyState(localReady);
    }

    // hook this up to the "Leave" button: host shuts down, clients just drop out of the room
    public void OnLeaveButtonClicked()
    {
        if (!isLocalPlayer) return;  // only we decide to leave the room 
        if (isServer)
            NetworkManager.singleton.StopHost();
        else
            NetworkManager.singleton.StopClient();
    }
}
