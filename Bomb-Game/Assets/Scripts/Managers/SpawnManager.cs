using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpawnManager : NetworkBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new();
    [Header("Reuse Cool-down")]
    public float pointCooldown = 1f;

    float[] lastUsed;
    private List<int> recentlyUsed = new List<int>();
    private Collider mapOutCollider;

    void Awake()
    {
        Initialize();
        Debug.Log($"SpawnManager Awake on {gameObject.name}, tag={gameObject.tag}, isTrigger={GetComponent<Collider>().isTrigger}, Instance set at {Time.time}", this);
    }

    void OnEnable()
    {
        mapOutCollider = GetComponent<Collider>();
        mapOutCollider.isTrigger = true; // Re-apply trigger on enable
        Debug.Log($"SpawnManager OnEnable on {gameObject.name}, tag={gameObject.tag}, isTrigger={mapOutCollider.isTrigger}, time={Time.time}", this);
    }

    public override void OnStartServer()
    {
        Initialize();
        Debug.Log($"SpawnManager OnStartServer on {gameObject.name}, tag={gameObject.tag}, isTrigger={GetComponent<Collider>().isTrigger}, Instance set at {Time.time}", this);
    }

    void Initialize()
    {
        Instance = this;
        lastUsed = new float[spawnPoints.Count];
        for (int i = 0; i < lastUsed.Length; i++) lastUsed[i] = -pointCooldown;
        mapOutCollider = GetComponent<Collider>();
        mapOutCollider.isTrigger = true; // Ensure trigger is set
    }

    [ServerCallback]
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"OnTriggerEnter: collider={other.gameObject.name}, tag={other.gameObject.tag}, self tag={gameObject.tag}, time={Time.time}", this);

        // Check if the colliding object is the player and this is the MapOut platform
        if (other.CompareTag("Player") && gameObject.CompareTag("MapOut"))
        {
            var life = other.GetComponent<PlayerLifeManager>();
            Debug.Log($"PlayerLifeManager check: found={life != null}, IsDead={life?.IsDead ?? true}, time={Time.time}", this);

            if (life == null)
            {
                Debug.LogError($"PlayerLifeManager component not found on {other.gameObject.name}", this);
                return;
            }

            if (life && !life.IsDead)
            {
                Debug.Log($"Player {other.gameObject.name} hit MapOut platform, triggering HandleDeath, time={Time.time}", this);
                life.HandleDeath();

                if (life.currentLives > 0)
                {
                    int idx = ChooseSpawnIndex();
                    Transform pt = spawnPoints[idx];
                    other.transform.SetPositionAndRotation(pt.position, pt.rotation);
                    lastUsed[idx] = Time.time;
                    Debug.Log($"Respawned {other.gameObject.name} at spawn point {idx}, position={pt.position}, time={Time.time}", this);
                }
            }
            else
            {
                Debug.LogWarning($"Player {other.gameObject.name} is dead or null, skipping HandleDeath, time={Time.time}", this);
            }
        }
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
            if (recentlyUsed.Count > spawnPoints.Count / 2) // Allow reuse after half the points are used
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

    [Server] public Transform GetNextSpawnPoint()
    {
        int idx = ChooseSpawnIndex();
        return spawnPoints[idx];
    }
}