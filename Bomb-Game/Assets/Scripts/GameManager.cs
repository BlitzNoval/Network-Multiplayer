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
    private readonly List<GameObject> activePlayers = new();

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
            activePlayers.Add(player);
    }

    public void UnregisterPlayer(GameObject player)
    {
        activePlayers.Remove(player);
    }

    // Handle bomb explosion
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
}
