using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class MapVotingManager : NetworkBehaviour
{
    [Header("Map Configuration")]
    [SerializeField] private MapCollection mapCollection;
    
    [Header("Voting Settings")]
    [SerializeField] private float randomSpinTime = 3f;
    
    // Network synchronized data
    [SyncVar] public string selectedMap = "";
    [SyncVar] public bool votingComplete = false;
    [SyncVar] public bool isSpinning = false;
    
    // Vote tracking
    private readonly SyncDictionary<uint, string> playerVotes = new SyncDictionary<uint, string>();
    
    // Events
    public System.Action<string, string, string> OnVoteCountUpdated; // city, island, ship counts
    public System.Action<string> OnMapSelected;
    public System.Action<string[]> OnTieDetected; // for roulette UI
    public System.Action OnRouletteStart;
    public System.Action<string> OnRouletteComplete;
    public System.Action<int> OnPlayerVoteStatusUpdate; // number of players who have voted
    
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
        
        // Don't auto-finalize voting - let it be triggered manually when players are ready
        // This allows players to change votes until they press ready
        Debug.Log($"Votes so far: {playerVotes.Count}");
    }
    
    [Server]
    public void TriggerVotingFinalization()
    {
        Debug.Log($"TriggerVotingFinalization called - votingComplete: {votingComplete}");
        if (!votingComplete)
        {
            Debug.Log("Starting voting finalization...");
            FinalizeVoting();
        }
        else
        {
            Debug.Log("Voting already complete, skipping finalization");
        }
    }
    
    [Server]
    public void FinalizeVoting()
    {
        if (votingComplete) return;
        
        var result = DetermineWinningMap();
        
        if (result.isTie)
        {
            // Handle tie with roulette
            StartCoroutine(HandleTieWithRoulette(result.tiedMaps));
        }
        else
        {
            // Direct winner
            CompleteVoting(result.winner);
        }
    }
    
    [Server]
    System.Collections.IEnumerator HandleTieWithRoulette(string[] tiedMaps)
    {
        // Show tie notification to all clients
        RpcShowTie(tiedMaps);
        
        // Start roulette spinning
        isSpinning = true;
        RpcStartRoulette();
        
        // Wait for dramatic effect
        yield return new WaitForSeconds(randomSpinTime);
        
        // Pick random winner from tied maps
        string winner = tiedMaps[Random.Range(0, tiedMaps.Length)];
        
        // Complete voting
        CompleteVoting(winner);
    }
    
    [Server]
    void CompleteVoting(string chosenMap)
    {
        selectedMap = chosenMap;
        votingComplete = true;
        isSpinning = false;
        
        Debug.Log($"CompleteVoting: Selected map: {chosenMap}, calling RpcMapSelected");
        
        RpcMapSelected(chosenMap);
    }
    
    [Server]
    (bool isTie, string winner, string[] tiedMaps) DetermineWinningMap()
    {
        if (playerVotes.Count == 0)
        {
            // No votes - choose random
            return (false, GetRandomMap(), null);
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
        var winningMaps = voteCounts.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToArray();
        
        // Check for tie
        if (winningMaps.Length > 1)
        {
            return (true, "", winningMaps);
        }
        
        return (false, winningMaps[0], null);
    }
    
    string GetRandomMap()
    {
        string[] maps = {"City", "Island", "Ship"};
        return maps[Random.Range(0, maps.Length)];
    }
    
    [ClientRpc]
    void RpcMapSelected(string mapName)
    {
        Debug.Log($"RpcMapSelected called with map: {mapName}, subscribers: {OnMapSelected?.GetInvocationList()?.Length ?? 0}");
        OnMapSelected?.Invoke(mapName);
    }
    
    [ClientRpc]
    void RpcShowTie(string[] tiedMaps)
    {
        OnTieDetected?.Invoke(tiedMaps);
    }
    
    [ClientRpc]
    void RpcStartRoulette()
    {
        OnRouletteStart?.Invoke();
    }
    
    [ClientRpc]
    void RpcUpdatePlayerVoteStatus(int totalVotes)
    {
        OnPlayerVoteStatusUpdate?.Invoke(totalVotes);
    }
    
    // Public method to get current vote counts for debugging
    public Dictionary<string, int> GetCurrentVoteCounts()
    {
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
        
        return voteCounts;
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
        
        // Also trigger player vote status update for icons
        int totalVotes = cityVotes + islandVotes + shipVotes;
        RpcUpdatePlayerVoteStatus(totalVotes);
    }
    
    
    [Server]
    public void ResetVoting()
    {
        playerVotes.Clear();
        selectedMap = "";
        votingComplete = false;
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