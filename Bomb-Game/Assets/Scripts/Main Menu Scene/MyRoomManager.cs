using Mirror;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public class MyRoomManager : NetworkRoomManager
{
    public static MyRoomManager Singleton { get; private set; }

    [Header("Room Matching")]
    public string RoomName = "DefaultRoom";
    [HideInInspector] public string DesiredRoomName;
    
    [Header("Map Selection")]
    [HideInInspector] public string selectedMapName = "";
    [SerializeField] private float gameStartDelay = 2f;
    public static string SelectedMap => Singleton?.selectedMapName ?? "";

    public static readonly Dictionary<string, PlayerLifeManager> ghosts = new();

    public override void Awake()
    {
        base.Awake();
        Singleton = this;
        
        if (Utils.IsSceneActive(RoomScene))
        {
            StartCoroutine(SubscribeToVotingEvents());
            Invoke(nameof(TrySubscribeToVotingEvents), 1f);
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

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null && conn.identity.TryGetComponent(out PlayerLifeManager life))
        {
            life.IsDisconnected = true;
            life.SetAliveState(false, true);
            conn.identity.RemoveClientAuthority();
            ghosts[life.GetComponent<PlayerInfo>().playerName] = life;

            if (life.bombHandler?.CurrentBomb != null)
            {
                life.bombHandler.CurrentBomb.ThrowBomb(Vector3.zero, false);
            }

            life.StartGhostTimeout();
        }
        base.OnServerDisconnect(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (!MyAuthenticator.connectionToPlayerName.TryGetValue(conn, out string playerName))
        {
            Debug.LogWarning($"OnServerAddPlayer: No playerName for connection {conn.connectionId}", this);
            playerName = $"Player_{conn.connectionId}";
        }
        Debug.Log($"OnServerAddPlayer: PlayerName={playerName}, GameActive={GameManager.Instance?.GameActive}, ConnId={conn.connectionId}", this);

        if (ghosts.TryGetValue(playerName, out PlayerLifeManager ghost) && ghost.IsDisconnected)
        {
            NetworkServer.AddPlayerForConnection(conn, ghost.gameObject);
            ghost.IsDisconnected = false;
            ghost.GetComponent<NetworkIdentity>().AssignClientAuthority(conn);
            ghosts.Remove(playerName);
            Debug.Log($"Reconnected player {playerName} to {ghost.gameObject.name}", this);
            return;
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

        // No need to assign EmoticonSelectionUI anymore - using shared SimpleEmoticonPanel
        Debug.Log($"Player {gamePlayer.name} spawned successfully - using shared emoticon system", this);

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

    System.Collections.IEnumerator SubscribeToVotingEvents()
    {
        Debug.Log("SubscribeToVotingEvents coroutine started");
        yield return null;
        TrySubscribeToVotingEvents();
    }
    
    void TrySubscribeToVotingEvents()
    {
        if (!Utils.IsSceneActive(RoomScene))
        {
            Debug.Log("Not in Room scene, skipping MapVotingManager subscription");
            return;
        }
        
        Debug.Log("TrySubscribeToVotingEvents called");
        
        MapVotingManager votingManager = MapVotingManager.Instance;
        if (votingManager == null)
        {
            Debug.Log("MapVotingManager.Instance is null, trying FindObjectOfType");
            votingManager = FindObjectOfType<MapVotingManager>();
        }
        
        if (votingManager != null)
        {
            var existingSubscribers = votingManager.OnMapSelected?.GetInvocationList();
            bool alreadySubscribed = existingSubscribers?.Any(d => d.Target == this) ?? false;
            
            if (!alreadySubscribed)
            {
                votingManager.OnMapSelected += OnMapSelected;
                Debug.Log($"Successfully subscribed to MapVotingManager events. VotingManager: {votingManager.name}, OnMapSelected subscribers: {votingManager.OnMapSelected?.GetInvocationList()?.Length ?? 0}");
            }
            else
            {
                Debug.Log("Already subscribed to MapVotingManager events");
            }
        }
        else
        {
            Debug.LogError("MapVotingManager not found for event subscription!");
        }
    }
    
    public override void OnRoomServerPlayersReady()
    {
        Debug.Log("OnRoomServerPlayersReady called - triggering map vote finalization");
        
        MapVotingManager votingManager = MapVotingManager.Instance;
        if (votingManager == null)
        {
            votingManager = FindObjectOfType<MapVotingManager>();
        }
        
        if (votingManager != null)
        {
            Debug.Log("Found MapVotingManager - triggering finalization");
            TrySubscribeToVotingEvents();
            votingManager.TriggerVotingFinalization();
        }
        else
        {
            Debug.Log("No MapVotingManager found - starting game with default map");
            base.OnRoomServerPlayersReady();
        }
    }
    
    void OnMapSelected(string mapName)
    {
        selectedMapName = mapName;
        Debug.Log($"MyRoomManager.OnMapSelected called with map: {mapName}, NetworkServer.active: {NetworkServer.active}, GameplayScene: {GameplayScene}", this);
        
        if (NetworkServer.active)
        {
            Debug.Log($"Starting delayed game transition. Waiting {gameStartDelay} seconds...");
            StartCoroutine(DelayedGameStart());
        }
        else
        {
            Debug.LogWarning("NetworkServer not active, cannot change scene");
        }
    }
    
    System.Collections.IEnumerator DelayedGameStart()
    {
        yield return new WaitForSeconds(gameStartDelay);
        
        if (NetworkServer.active)
        {
            Debug.Log($"Calling ServerChangeScene with scene: {GameplayScene}");
            ServerChangeScene(GameplayScene);
            Debug.Log("ServerChangeScene called successfully");
        }
        else
        {
            Debug.LogWarning("NetworkServer not active during delayed start, cannot change scene");
        }
    }
    
    public override void OnRoomServerSceneChanged(string sceneName)
    {
        base.OnRoomServerSceneChanged(sceneName);
        
        if (sceneName == RoomScene)
        {
            Debug.Log("Room scene loaded - attempting to subscribe to voting events");
            Invoke(nameof(TrySubscribeToVotingEvents), 0.5f);
        }
    }
    
    public override void OnStopHost()
    {
        base.OnStopHost();
        selectedMapName = "";
        if (GameManager.Instance != null)
            GameManager.Instance.ResetState();
        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.ResetPanels();
        Debug.Log("OnStopHost: Reset GameManager and PlayerUIManager", this);
    }
}