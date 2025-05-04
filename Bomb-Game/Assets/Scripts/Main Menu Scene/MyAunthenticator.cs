using Mirror;
using UnityEngine;
using System;

public class MyAuthenticator : NetworkAuthenticator
{
    // UI listens here to know if auth succeeded (true) or failed (false)
    public static event Action<bool> AuthResult;

    // Runs once on the server when it's starting up
    public override void OnStartServer()
    {
        // Tell Mirror: when we get an AuthRequestMessage, call our handler
        // This is the message we defined in AuthMessages.cs
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
    }

    // Runs once on the client when it's starting up
    public override void OnStartClient()
    {
        // Tell Mirror: when we get an AuthResponseMessage back, call our handler 
        NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
    }

    // Called on the client to do the authentication
    public override void OnClientAuthenticate()
    {
        // Only send once we're actually connected but not yet authenticated
        if (NetworkClient.connection != null && !NetworkClient.connection.isAuthenticated)
        {
            // add the room name and player name into our custom message
            var msg = new AuthRequestMessage
            {
                roomName   = MyRoomManager.Singleton.DesiredRoomName,
                playerName = MyRoomPlayer.LocalPlayerName
            };
            Debug.Log($"[Auth] Sending room='{msg.roomName}', player='{msg.playerName}'");
            // send off the message to the server
            NetworkClient.connection.Send(msg);
        }
    }

    // Called on the server when a client sends an AuthRequestMessage
    // This is where we check if the room name matches what the host set
    void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        // Check if the requested room matches what the host set
        bool ok = msg.roomName == MyRoomManager.Singleton.RoomName;
        // Reply with success or failure
        conn.Send(new AuthResponseMessage { success = ok });

        if (ok)
        {
            // Let Mirror proceed with spawning the player
            ServerAccept(conn);

            // Immediately assign the playerName SyncVar so it shows in the room ui
            if (conn.identity != null)
            {
                var p = conn.identity.GetComponent<MyRoomPlayer>();
                if (p != null) p.playerName = msg.playerName;
            }
        }
        else
        {
            // Wrong room name means disconnect the player and they are told to try again
            conn.Disconnect();
        }
    }

    // Called on the client when we get an AuthResponseMessage back from the server
    // This is where we check if the room name matches what the host set
    void OnAuthResponseMessage(AuthResponseMessage msg)
    {
        Debug.Log($"[Auth] Response received: success={msg.success}");
        // Notify anything listening (like MainMenuUI) if it worked
        AuthResult?.Invoke(msg.success);

        if (msg.success)
            ClientAccept();  // let Mirror proceed into the room
        else
            ClientReject();  // block entry and trigger a disconnect
    }

    // We don't rewally need any special server-side steps here, so  just left it
    public override void OnServerAuthenticate(NetworkConnectionToClient conn) {}
}
