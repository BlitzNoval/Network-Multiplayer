using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Bomb Settings")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform bombSpawnPoint;

    private GameObject bombInstance;
    private Scene parallelScene;
    public readonly List<GameObject> activePlayers = new();
    private int nextPlayerID = 1;
    public Dictionary<GameObject, int> playerIDs = new Dictionary<GameObject, int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (bombPrefab == null)
        {
            Debug.LogError("Bomb prefab not assigned in GameManager.");
            return;
        }

        if (bombSpawnPoint == null)
        {
            Debug.LogWarning("No bomb spawn point set — using Vector3.zero as fallback.");
            bombSpawnPoint = new GameObject("BombSpawnPoint").transform;
            bombSpawnPoint.position = Vector3.zero;
        }

        parallelScene = SceneManager.CreateScene("ParallelScene", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SpawnBomb();
    }

    private void OnEnable()
    {
        Bomb.OnBombExplodedGlobal += OnBombExploded;
    }

    private void OnDisable()
    {
        Bomb.OnBombExplodedGlobal -= OnBombExploded;
    }

    public void RegisterPlayer(GameObject player)
{
    if (!activePlayers.Contains(player))
    {
        activePlayers.Add(player);
        int id = nextPlayerID++;
        playerIDs[player] = id;
        var lifeManager = player.GetComponent<PlayerLifeManager>();
        if (lifeManager != null)
        {
            lifeManager.SetPlayerID(id);
        }
        Debug.Log($"Registered player {id}, total players: {activePlayers.Count}");
    }
}

    public void UnregisterPlayer(GameObject player)
    {
        activePlayers.Remove(player);
        playerIDs.Remove(player);
    }

    private void OnBombExploded()
    {
        bombInstance = null;
        SpawnBomb();
    }

    public void SpawnBomb()
    {
        if (bombInstance != null)
        {
            Debug.LogWarning("Bomb already exists.");
            return;
        }

        if (activePlayers.Count == 0)
        {
            Debug.LogError("No players available to assign the bomb.");
            return;
        }

        GameObject randomPlayer = activePlayers[Random.Range(0, activePlayers.Count)];
        bombInstance = Instantiate(bombPrefab, bombSpawnPoint.position, bombSpawnPoint.rotation);

        if (bombInstance.TryGetComponent(out Bomb bombScript))
        {
            bombScript.AssignToPlayer(randomPlayer);
        }
        else
        {
            Debug.LogError("Spawned bomb is missing Bomb component.");
        }
    }

    public void ReassignOrphanedBomb(Bomb bomb)
    {
        GameObject nearestPlayer = FindNearestPlayer(bomb.transform.position);
        
        if (nearestPlayer != null)
        {
            bomb.AssignToPlayer(nearestPlayer);
        }
        else
        {
            bomb.TriggerImmediateExplosion();
        }
    }

    private GameObject FindNearestPlayer(Vector3 position)
    {
        GameObject nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (var player in activePlayers)
        {
            float dist = Vector3.Distance(position, player.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = player;
            }
        }
        return nearest;
    }
}