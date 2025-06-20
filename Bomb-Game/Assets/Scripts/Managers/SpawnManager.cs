using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public List<Transform> spawnPoints = new();

    public Transform respawnReference;
    public float respawnOffset = 40f;
    
    public MapCollection mapCollection;
    public Transform currentFloorReference;
    

    public float pointCooldown = 1f;

    float[] lastUsed;
    private List<int> recentlyUsed = new List<int>();

    void Awake()
    {
        Initialize();
        if (respawnReference == null)
            Debug.LogError("respawnReference is not set in SpawnManager", this);
        Debug.Log($"SpawnManager Awake on {gameObject.name}, Instance set at {Time.time}", this);
    }

    public override void OnStartServer()
    {
        Initialize();
        UpdateSpawnPointsForSelectedMap();
        Debug.Log($"SpawnManager OnStartServer on {gameObject.name}, Instance set at {Time.time}", this);
    }

    void Initialize()
    {
        Instance = this;
        lastUsed = new float[spawnPoints.Count];
        for (int i = 0; i < lastUsed.Length; i++) lastUsed[i] = -pointCooldown;
    }

    public int ChooseSpawnIndex()
    {
        float now = Time.time;
        var free = new List<int>();

        for (int i = 0; i < lastUsed.Length; ++i)
        {
            if (now - lastUsed[i] >= pointCooldown && !recentlyUsed.Contains(i))
                free.Add(i);
        }

        if (free.Count > 0)
        {
            int idx = free[Random.Range(0, free.Count)];
            recentlyUsed.Add(idx);
            if (recentlyUsed.Count > spawnPoints.Count / 2)
                recentlyUsed.RemoveAt(0);
            lastUsed[idx] = Time.time;
            return idx;
        }

        float maxTime = float.MinValue;
        int best = 0;
        for (int i = 0; i < lastUsed.Length; ++i)
        {
            if (now - lastUsed[i] > maxTime)
            {
                maxTime = now - lastUsed[i];
                best = i;
            }
        }
        lastUsed[best] = Time.time;
        return best;
    }

    [Server]
    public Transform GetNextSpawnPoint()
    {
        int idx = ChooseSpawnIndex();
        return spawnPoints[idx];
    }
    
    public void UpdateSpawnPointsForSelectedMap()
    {
        string selectedMap = MyRoomManager.SelectedMap;
        UpdateSpawnPointsForMap(selectedMap);
    }
    
    public void UpdateSpawnPointsForMap(string selectedMap)
    {
        if (string.IsNullOrEmpty(selectedMap))
        {
            Debug.LogWarning("No map selected, using default spawn positions");
            return;
        }
        
        if (mapCollection == null)
        {
            Debug.LogError("MapCollection not assigned to SpawnManager!");
            return;
        }
        
        var mapData = mapCollection.GetMapByName(selectedMap);
        if (mapData == null)
        {
            Debug.LogError($"Map data not found for: {selectedMap}");
            return;
        }
        
        if (mapData.spawnPositions == null || mapData.spawnPositions.Length != 4)
        {
            Debug.LogError($"Invalid spawn positions for map: {selectedMap}. Expected 4 positions, got {mapData.spawnPositions?.Length ?? 0}");
            return;
        }
        
        for (int i = 0; i < spawnPoints.Count && i < mapData.spawnPositions.Length; i++)
        {
            if (spawnPoints[i] != null)
            {
                spawnPoints[i].position = mapData.spawnPositions[i];
                Debug.Log($"Updated Spawn {i + 1} position to {mapData.spawnPositions[i]} for map {selectedMap}");
            }
        }
        
        if (mapData.floorReference != null)
        {
            GameObject existingFloor = GameObject.FindGameObjectWithTag("Floor");
            if (existingFloor != null)
            {
                existingFloor.transform.position = mapData.floorReference.transform.position;
                existingFloor.transform.rotation = mapData.floorReference.transform.rotation;
                existingFloor.transform.localScale = mapData.floorReference.transform.localScale;
                Debug.Log($"Updated floor reference for map: {selectedMap}");
            }
            else
            {
                Debug.LogWarning("No GameObject with 'Floor' tag found to update");
            }
        }
        
        Debug.Log($"Updated spawn points for map: {selectedMap}");
    }


    void OnDrawGizmos()
    {        
        if (respawnReference != null)
        {
            Vector3 thresholdPos = respawnReference.position - Vector3.up * respawnOffset;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(thresholdPos, new Vector3(100f, 0.1f, 100f));
        }
    }
}