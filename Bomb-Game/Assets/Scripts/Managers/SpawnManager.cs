using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new();

    [Header("Respawn Settings")]
    public Transform respawnReference;
    public float respawnOffset = 40f;

    [Header("Reuse Cool-down")]
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
}