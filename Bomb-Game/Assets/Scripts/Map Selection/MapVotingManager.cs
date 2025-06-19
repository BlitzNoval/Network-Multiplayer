using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class MapVotingManager : NetworkBehaviour
{
    [Header("Map Configuration")]
    [SerializeField] private MapCollection mapCollection;
    
    [Header("Voting Settings")]
    [SerializeField] private float votingTimeLimit = 30f;
    
    // Network synchronized data
    [SyncVar] public string selectedMap = "";
    [SyncVar] public bool votingComplete = false;
    
    // Vote tracking
    private readonly SyncDictionary<uint, string> playerVotes = new SyncDictionary<uint, string>();
    
    // Events
    public System.Action<string, string, string> OnVoteCountUpdated; // city, island, ship counts
    public System.Action<string> OnMapSelected;
    
    // Static reference for easy access
    public static MapVotingManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        playerVotes.OnChange += OnPlayerVotesChanged;
    }
    
    
    void OnPlayerVotesChanged(SyncDictionary<uint, string>.Operation op, uint key, string item)
    {
        UpdateVoteCounts();
    }
    
    [Command(requiresAuthority = false)]
    public void CmdVoteForMap(string mapName, NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;
        if (votingComplete) return;
        
        uint playerId = (uint)sender.connectionId;
        
        // Validate map name
        if (mapName != "City" && mapName != "Island" && mapName != "Ship")
        {
            Debug.LogWarning($"Invalid map name: {mapName}");
            return;
        }
        
        // Record the vote
        playerVotes[playerId] = mapName;
        
        Debug.Log($"Player {playerId} voted for {mapName}");
        
        // Check if we should finalize voting
        CheckVotingComplete();
    }
    
    void CheckVotingComplete()
    {
        if (!isServer) return;
        
        // Get total connected players
        int totalPlayers = NetworkServer.connections.Count;
        
        // If everyone has voted, finalize immediately
        if (playerVotes.Count >= totalPlayers)
        {
            FinalizeVoting();
        }
    }
    
    [Server]
    public void FinalizeVoting()
    {
        if (votingComplete) return;
        
        string chosenMap = DetermineWinningMap();
        selectedMap = chosenMap;
        votingComplete = true;
        
        Debug.Log($"Voting complete! Selected map: {chosenMap}");
        
        RpcMapSelected(chosenMap);
    }
    
    [Server]
    string DetermineWinningMap()
    {
        if (playerVotes.Count == 0)
        {
            // No votes - choose random
            return GetRandomMap();
        }
        
        // Count votes for each map
        var voteCounts = new Dictionary<string, int>
        {
            {"City", 0},
            {"Island", 0},
            {"Ship", 0}
        };
        
        foreach (var vote in playerVotes.Values)
        {
            if (voteCounts.ContainsKey(vote))
                voteCounts[vote]++;
        }
        
        // Find the map(s) with the most votes
        int maxVotes = voteCounts.Values.Max();
        var winningMaps = voteCounts.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();
        
        // If tie, choose random from winners
        if (winningMaps.Count > 1)
        {
            return winningMaps[Random.Range(0, winningMaps.Count)];
        }
        
        return winningMaps.First();
    }
    
    string GetRandomMap()
    {
        string[] maps = {"City", "Island", "Ship"};
        return maps[Random.Range(0, maps.Length)];
    }
    
    [ClientRpc]
    void RpcMapSelected(string mapName)
    {
        OnMapSelected?.Invoke(mapName);
    }
    
    void UpdateVoteCounts()
    {
        // Count votes for each map
        int cityVotes = 0, islandVotes = 0, shipVotes = 0;
        
        foreach (var vote in playerVotes.Values)
        {
            switch (vote)
            {
                case "City": cityVotes++; break;
                case "Island": islandVotes++; break;
                case "Ship": shipVotes++; break;
            }
        }
        
        OnVoteCountUpdated?.Invoke(cityVotes.ToString(), islandVotes.ToString(), shipVotes.ToString());
    }
    
    [Server]
    public void StartVotingTimer()
    {
        if (votingTimeLimit > 0)
        {
            Invoke(nameof(FinalizeVoting), votingTimeLimit);
        }
    }
    
    [Server]
    public void ResetVoting()
    {
        playerVotes.Clear();
        selectedMap = "";
        votingComplete = false;
        CancelInvoke(nameof(FinalizeVoting));
    }
    
    public MapSpawnData GetSelectedMapData()
    {
        if (mapCollection == null || string.IsNullOrEmpty(selectedMap))
            return null;
            
        return mapCollection.GetMapByName(selectedMap);
    }
    
    void OnDestroy()
    {
        playerVotes.OnChange -= OnPlayerVotesChanged;
        if (Instance == this)
            Instance = null;
    }
}