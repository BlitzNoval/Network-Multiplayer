using Mirror;
using UnityEngine;
using System.Collections;
using System.Linq;

public class HostMigrationManager : NetworkBehaviour
{
    [TargetRpc]
    public void TargetBecomeHost(NetworkConnectionToClient target, string room, ushort port)
    {
        StartCoroutine(SwapToHost(room, port));
    }

    IEnumerator SwapToHost(string room, ushort port)
    {
        yield return null;
        NetworkManager.singleton.StopClient();
        NetworkManager.singleton.GetComponent<TelepathyTransport>().port = port;
        NetworkManager.singleton.StartHost();
        MyRoomManager.Singleton.RoomName = room;
    }

    public static void ElectAndNotify()
{
    var conns = NetworkServer.connections.Values.ToList();
    if (conns.Count <= 1) return;

    var next = conns.OrderBy(c => c.lastMessageTime).First();
    var transport = NetworkManager.singleton.GetComponent<TelepathyTransport>();
    if (transport != null)
    {
        ushort newPort = (ushort)(transport.port + 1);
        next.identity.GetComponent<HostMigrationManager>().TargetBecomeHost(next, MyRoomManager.Singleton.RoomName, newPort);
    }
    else
    {
        Debug.LogError("TelepathyTransport component not found on NetworkManager. Ensure the correct transport is attached.");
    }
}
}