using Mirror;
using UnityEngine;

public class MyRoomManager : NetworkRoomManager
{
    public static MyRoomManager Singleton { get; private set; }

    [Header("Room Matching")]
    public string RoomName = "DefaultRoom";
    [HideInInspector] public string DesiredRoomName;
    
    [Header("Map Selection")]
    [HideInInspector] public string selectedMapName = "";
    public static string SelectedMap => Singleton?.selectedMapName ?? "";

    public override void Awake()
    {
        base.Awake();
        Singleton = this;
        
        // Subscribe to map voting events
        MapVotingManager votingManager = FindObjectOfType<MapVotingManager>();
        if (votingManager != null)
        {
            votingManager.OnMapSelected += OnMapSelected;
        }
        
        Debug.Log("MyRoomManager Awake: Singleton set", this);
    }

    public override void OnDestroy()
    {
        if (Singleton == this)
            Singleton = null;
        base.OnDestroy();
        Debug.Log("MyRoomManager destroyed", this);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (!MyAuthenticator.connectionToPlayerName.TryGetValue(conn, out string playerName))
        {
            Debug.LogWarning($"OnServerAddPlayer: No playerName for connection {conn.connectionId}", this);
            playerName = $"Player_{conn.connectionId}";
        }
        Debug.Log($"OnServerAddPlayer: PlayerName={playerName}, GameActive={GameManager.Instance?.GameActive}, ConnId={conn.connectionId}", this);

        if (GameManager.Instance != null && GameManager.Instance.GameActive && 
            GameManager.Instance.playerObjects.TryGetValue(playerName, out GameObject existingPlayer))
        {
            var lifeManager = existingPlayer.GetComponent<PlayerLifeManager>();
            var playerInfo = existingPlayer.GetComponent<PlayerInfo>();
            if (lifeManager != null && lifeManager.IsDisconnected)
            {
                if (playerInfo != null && string.IsNullOrEmpty(playerInfo.playerName))
                {
                    playerInfo.playerName = playerName;
                    Debug.Log($"Reconnected player: Set PlayerInfo.playerName={playerName} for {existingPlayer.name}", this);
                }
                NetworkServer.AddPlayerForConnection(conn, existingPlayer);
                lifeManager.IsDisconnected = false;
                var netId = existingPlayer.GetComponent<NetworkIdentity>();
                if (netId != null)
                    netId.AssignClientAuthority(conn);
                Debug.Log($"Reconnected player {playerName} to {existingPlayer.name}, netId={netId.netId}", this);
                return;
            }
        }

        base.OnServerAddPlayer(conn);
        Debug.Log($"OnServerAddPlayer: Added new player {playerName}", this);
    }

    

    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn,
                                                          GameObject roomPlayer,
                                                          GameObject gamePlayer)
    {
        var roomP = roomPlayer.GetComponent<MyRoomPlayer>();
        if (roomP != null)
        {
            var playerInfo = gamePlayer.GetComponent<PlayerInfo>();
            if (playerInfo != null)
            {
                playerInfo.playerName = roomP.playerName;
                Debug.Log($"OnRoomServerSceneLoadedForPlayer: Set PlayerInfo.playerName={roomP.playerName} for {gamePlayer.name}", this);
            }
            else
            {
                Debug.LogWarning($"PlayerInfo component missing on {gamePlayer.name}", this);
            }
        }
        else
        {
            Debug.LogWarning($"MyRoomPlayer component missing on {roomPlayer.name}", this);
        }

        if (SpawnManager.Instance != null && SpawnManager.Instance.spawnPoints.Count > 0)
        {
            int idx = SpawnManager.Instance.ChooseSpawnIndex();
            Transform pt = SpawnManager.Instance.spawnPoints[idx];
            gamePlayer.transform.SetPositionAndRotation(pt.position, pt.rotation);
            Debug.Log($"Spawned {gamePlayer.name} at spawn point {idx}, position={pt.position}", this);
        }
        else
        {
            Debug.LogWarning("SpawnManager.Instance or spawnPoints missing", this);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(gamePlayer);
            Debug.Log($"Registered {gamePlayer.name} with GameManager", this);
        }
        else
        {
            Debug.LogError("GameManager.Instance is null in OnRoomServerSceneLoadedForPlayer", this);
        }

        return true;
    }

    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();
#if UNITY_2023_1_OR_NEWER
        var menuUI = Object.FindFirstObjectByType<MainMenuUI>();
#else
        var menuUI = Object.FindObjectOfType<MainMenuUI>();
#endif
        if (menuUI != null)
            menuUI.ShowInvalidRoomPanel();
        Debug.Log("OnRoomClientDisconnect: Client disconnected", this);
    }

    void OnMapSelected(string mapName)
    {
        selectedMapName = mapName;
        Debug.Log($"MyRoomManager: Map selected - {mapName}", this);
        
        // Auto-start game when map is selected (optional - you can remove this if you want manual start)
        if (NetworkServer.active && allPlayersReady)
        {
            ServerChangeScene(GameplayScene);
        }
    }
    
    public override void OnStopHost()
    {
        base.OnStopHost();
        selectedMapName = ""; // Reset selected map
        if (GameManager.Instance != null)
            GameManager.Instance.ResetState();
        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.ResetPanels();
        Debug.Log("OnStopHost: Reset GameManager and PlayerUIManager", this);
    }
}