using UnityEngine;
using Mirror;

public class MyRoomManager : NetworkRoomManager
{
    public static MyRoomManager Singleton { get; private set; }

    [Header("Room Matching")]
    public string RoomName = "DefaultRoom";
    [HideInInspector] public string DesiredRoomName;

    //-----------------------------------------------------------------------

    public override void Awake()
    {
        base.Awake();
        Singleton = this;
    }

    // ------------------ Room-scene (lobby) callbacks ----------------------

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // let base create the room-player prefab
        base.OnServerAddPlayer(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null && GameManager.Instance != null)
            GameManager.Instance.UnregisterPlayer(conn.identity.gameObject);

        base.OnServerDisconnect(conn);
    }

    // ------------------ Game-scene callback --------------------------------
    // Called for *each* player as the game scene loads
    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn,
                                                          GameObject roomPlayer,
                                                          GameObject gamePlayer)
    {
        // place the avatar on a free spawn-point
        if (SpawnManager.Instance != null && SpawnManager.Instance.spawnPoints.Count > 0)
        {
            int idx = SpawnManager.Instance.ChooseSpawnIndex();
            Transform pt = SpawnManager.Instance.spawnPoints[idx];
            gamePlayer.transform.SetPositionAndRotation(pt.position, pt.rotation);
        }

        // track on server
        GameManager.Instance?.RegisterPlayer(gamePlayer);

        return true;        // keep default behaviour
    }

    // ------------------ Client helper -------------------------------------
    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();

#if UNITY_2023_1_OR_NEWER
        var menuUI = Object.FindFirstObjectByType<MainMenuUI>();
#else
        var menuUI = Object.FindObjectOfType<MainMenuUI>();
#endif
        if (menuUI != null) menuUI.ShowInvalidRoomPanel();
    }
}