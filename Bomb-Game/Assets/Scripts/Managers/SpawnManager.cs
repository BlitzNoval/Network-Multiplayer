using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Points (fill in as many as you like)")]
    [Tooltip("Transforms marking where players may spawn.")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Cooldown Settings")]
    [Tooltip("Seconds after a spawn point is used before it can be reused.")]
    public float pointCooldown = 1f;

    // Tracks when each point was last used
    private float[] lastUsedTime;

    // Event: (playerGameObject, spawnPointTransform)
    [System.Serializable]
    public class PlayerSpawnEvent : UnityEvent<GameObject, Transform> { }

    /// <summary>
    /// Fires when a Player enters the MapOut trigger.
    /// Default listener will teleport locally.
    /// Networking code should subscribe instead for server-side spawning.
    /// </summary>
    public PlayerSpawnEvent OnPlayerSpawn = new PlayerSpawnEvent();

    void Awake()
    {
        // Initialize so all spawn points are immediately available
        lastUsedTime = new float[spawnPoints.Count];
        for (int i = 0; i < lastUsedTime.Length; i++)
            lastUsedTime[i] = -pointCooldown;

        // Add default local-teleport listener
        OnPlayerSpawn.AddListener(DefaultTeleport);
    }

    void OnValidate()
    {
        // Ensure collider is a trigger
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    // Handle player entering trigger (intended for server-side processing when networked)
     // SpawnManager.cs (partial)
   public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerLifeManager lifeManager = other.GetComponent<PlayerLifeManager>();
        if (lifeManager != null && !lifeManager.IsDead)
        {
            lifeManager.HandleDeath();
            
            if (lifeManager.CurrentLives > 0)
            {
                int idx = ChooseSpawnIndex();
                Transform pt = spawnPoints[idx];
                OnPlayerSpawn.Invoke(other.gameObject, pt);
                lastUsedTime[idx] = Time.time;
            }
        }
    }

    // Choose a spawn index (intended for server-side processing when networked)
    public int ChooseSpawnIndex()
    {
        float now = Time.time;
        var free = new List<int>();

        // Collect all points off-cooldown
        for (int i = 0; i < lastUsedTime.Length; i++)
            if (now - lastUsedTime[i] >= pointCooldown)
                free.Add(i);

        if (free.Count > 0)
        {
            // Random choice among free points
            return free[Random.Range(0, free.Count)];
        }
        else
        {
            // All busy → pick the one that'll free up soonest
            float bestRemain = float.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < lastUsedTime.Length; i++)
            {
                float rem = pointCooldown - (now - lastUsedTime[i]);
                if (rem < bestRemain)
                {
                    bestRemain = rem;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }
    }

    // Default, local-only teleport handler
    private void DefaultTeleport(GameObject player, Transform spawnPoint)
    {
        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
    }
}