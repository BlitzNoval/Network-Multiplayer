using UnityEngine;
using Mirror;
using System;

public class MyRoomPlayer : NetworkRoomPlayer
{
    public static string LocalPlayerName = "Guest";

    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName = "Guest";

    public static event Action<MyRoomPlayer> LocalPlayerCreated;
    public static event Action<MyRoomPlayer> ClientEntered;
    public static event Action<MyRoomPlayer> ClientReadyChanged;
    public static event Action<MyRoomPlayer> ClientLeft;

    private bool localReady = false;

 
    public bool PlayerReady => readyToBegin;
    public int  PlayerIndex => index;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        CmdSetPlayerName(LocalPlayerName);
        LocalPlayerCreated?.Invoke(this);
    }

    void OnPlayerNameChanged(string oldName, string newName)
    {
        ClientEntered?.Invoke(this);
    }

    [Command]
    void CmdSetPlayerName(string name)
    {
        playerName = name;
    }

    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        ClientEntered?.Invoke(this);
    }

    public override void ReadyStateChanged(bool oldReady, bool newReady)
    {
        base.ReadyStateChanged(oldReady, newReady);
        ClientReadyChanged?.Invoke(this);
    }

 
    public override void OnClientExitRoom()
    {
        base.OnClientExitRoom();
        ClientLeft?.Invoke(this);
    }

    public void OnReadyButtonClicked()
    {
        if (!isLocalPlayer) return;
        localReady = !localReady;
        CmdChangeReadyState(localReady);
    }

    public void OnLeaveButtonClicked()
    {
        if (!isLocalPlayer) return; 
        if (isServer)
            NetworkManager.singleton.StopHost();
        else
            NetworkManager.singleton.StopClient();
    }
}