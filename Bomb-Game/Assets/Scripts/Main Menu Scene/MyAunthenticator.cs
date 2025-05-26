using Mirror;
using System.Collections.Generic;
using System;
using UnityEngine;

public class MyAuthenticator : NetworkAuthenticator
{
    public static Dictionary<NetworkConnectionToClient, string> connectionToPlayerName = new Dictionary<NetworkConnectionToClient, string>();

    public static event Action<bool> AuthResult;

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
    }

    public override void OnClientAuthenticate()
    {
        if (NetworkClient.connection != null && !NetworkClient.connection.isAuthenticated)
        {
            var msg = new AuthRequestMessage
            {
                roomName   = MyRoomManager.Singleton.DesiredRoomName,
                playerName = MyRoomPlayer.LocalPlayerName
            };
            Debug.Log($"[Auth] Sending room='{msg.roomName}', player='{msg.playerName}'");
            NetworkClient.connection.Send(msg);
        }
    }

    void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        bool ok = msg.roomName == MyRoomManager.Singleton.RoomName;
        conn.Send(new AuthResponseMessage { success = ok });

        if (ok)
        {
            connectionToPlayerName[conn] = msg.playerName; // Store player name
            ServerAccept(conn);
            if (conn.identity != null)
            {
                var p = conn.identity.GetComponent<MyRoomPlayer>();
                if (p != null) p.playerName = msg.playerName;
            }
        }
        else
        {
            conn.Disconnect();
        }
    }

    void OnAuthResponseMessage(AuthResponseMessage msg)
    {
        Debug.Log($"[Auth] Response received: success={msg.success}");
        AuthResult?.Invoke(msg.success);
        if (msg.success) ClientAccept();
        else ClientReject();
    }

    public override void OnServerAuthenticate(NetworkConnectionToClient conn) {}
}