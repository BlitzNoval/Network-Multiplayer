using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;   
 
public class MapVotingManager : NetworkBehaviour
{
    [SerializeField] private MapCollection mapCollection;
    
    [SerializeField] private float randomSpinTime = 3f;
    
    [SyncVar] public string selectedMap = "";
    [SyncVar] public bool votingComplete = false;
    [SyncVar] public bool isSpinning = false;
    
    private readonly SyncDictionary<uint, string> playerVotes = new SyncDictionary<uint, string>();
    
    public System.Action<string, string, string> OnVoteCountUpdated;
    public System.Action<string> OnMapSelected;
    public System.Action<string[]> OnTieDetected;
    public System.Action OnRouletteStart;
    public System.Action<string> OnRouletteComplete;
    public System.Action<int> OnPlayerVoteStatusUpdate;
    
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
        
        if (mapName != "City" && mapName != "Island" && mapName != "Ship")
        {
            Debug.LogWarning($"Invalid map name: {mapName}");
            return;
        }
        
        playerVotes[playerId] = mapName;
        
        Debug.Log($"Player {playerId} voted for {mapName}");
        
        CheckVotingComplete();
    }
    
    void CheckVotingComplete()
    {
        if (!isServer) return;
        
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
            StartCoroutine(HandleTieWithRoulette(result.tiedMaps));
        }
        else
        {
            CompleteVoting(result.winner);
        }
    }
    
    [Server]
    System.Collections.IEnumerator HandleTieWithRoulette(string[] tiedMaps)
    {
        RpcShowTie(tiedMaps);
        
        isSpinning = true;
        RpcStartRoulette();
        
        yield return new WaitForSeconds(randomSpinTime);
        
        string winner = tiedMaps[Random.Range(0, tiedMaps.Length)];
        
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
            return (false, GetRandomMap(), null);
        }
        
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
        
        int maxVotes = voteCounts.Values.Max();
        var winningMaps = voteCounts.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToArray();
        
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