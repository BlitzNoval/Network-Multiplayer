using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameObject bombPrefab;
    public GameObject bombInstance;
    public Scene parallelScene;

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
        parallelScene = SceneManager.CreateScene("ParallelScene", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SpawnBomb();
    }

    void SpawnBomb()
    {
        if (bombInstance != null)
        {
            Debug.LogWarning("Bomb already exists!");
            return;
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0)
        {
            Debug.LogError("No players found to assign bomb!");
            return;
        }

        GameObject randomPlayer = players[Random.Range(0, players.Length)];
        bombInstance = Instantiate(bombPrefab, Vector3.zero, Quaternion.identity);
        Bomb bombScript = bombInstance.GetComponent<Bomb>();
        if (bombScript != null)
        {
            bombScript.AssignToPlayer(randomPlayer);
            SetupPlayerInputs(players);
        }
        else
        {
            Debug.LogError("Bomb script missing on instantiated bomb!");
        }
    }

    void SetupPlayerInputs(GameObject[] players)
    {
        foreach (GameObject player in players)
        {
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            PlayerBombHandler handler = player.GetComponent<PlayerBombHandler>();
            if (playerInput != null && handler != null)
            {
                playerInput.actions.FindAction("SwapBomb").performed += ctx => handler.OnSwapBomb(ctx.ReadValueAsButton());
                playerInput.actions.FindAction("Throw").started += ctx => handler.OnThrow(true); // Pressed
                playerInput.actions.FindAction("Throw").canceled += ctx => handler.OnThrow(false); // Released
            }
        }
    }
}