using Mirror;
using UnityEngine;
using System.Linq;

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

    public override void Awake()
    {
        base.Awake();
        Singleton = this;
        
        // Only subscribe to voting events if we're in the Room scene
        if (Utils.IsSceneActive(RoomScene))
        {
            // Subscribe to map voting events - use delayed subscription since MapVotingManager may not be ready yet
            StartCoroutine(SubscribeToVotingEvents());
            
            // Also try immediate subscription in case the coroutine fails
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

    System.Collections.IEnumerator SubscribeToVotingEvents()
    {
        Debug.Log("SubscribeToVotingEvents coroutine started");
        
        // Wait a frame for all components to initialize
        yield return null;
        
        TrySubscribeToVotingEvents();
    }
    
    void TrySubscribeToVotingEvents()
    {
        // Only try to subscribe if we're in the Room scene
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
            // Check if already subscribed to avoid double subscription
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
        
        // When all players are ready, trigger voting finalization
        MapVotingManager votingManager = MapVotingManager.Instance;
        if (votingManager == null)
        {
            votingManager = FindObjectOfType<MapVotingManager>();
        }
        
        if (votingManager != null)
        {
            Debug.Log("Found MapVotingManager - triggering finalization");
            
            // Make sure we're subscribed before triggering finalization
            TrySubscribeToVotingEvents();
            
            votingManager.TriggerVotingFinalization();
        }
        else
        {
            Debug.Log("No MapVotingManager found - starting game with default map");
            // No voting system active, start game normally
            base.OnRoomServerPlayersReady();
        }
    }
    
    void OnMapSelected(string mapName)
    {
        selectedMapName = mapName;
        Debug.Log($"MyRoomManager.OnMapSelected called with map: {mapName}, NetworkServer.active: {NetworkServer.active}, GameplayScene: {GameplayScene}", this);
        
        // Start game with delay for better UX
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
        
        // When room scene loads, try to subscribe to voting events
        if (sceneName == RoomScene)
        {
            Debug.Log("Room scene loaded - attempting to subscribe to voting events");
            Invoke(nameof(TrySubscribeToVotingEvents), 0.5f);
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