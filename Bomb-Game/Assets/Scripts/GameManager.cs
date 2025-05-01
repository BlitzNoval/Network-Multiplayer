using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // Singleton
    public GameObject bombPrefab; // Assign BombPrefab in Inspector
    public GameObject bombInstance; // Tracks the single bomb

    void Awake()
    {
        // Singleton setup
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
        SpawnBomb();
    }

    void SpawnBomb()
    {
        if (bombInstance != null)
        {
            Debug.LogWarning("Bomb already exists!");
            return;
        }

        // Find all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0)
        {
            Debug.LogError("No players found to assign bomb!");
            return;
        }

        // Pick a random player
        GameObject randomPlayer = players[Random.Range(0, players.Length)];

        // Spawn bomb
        bombInstance = Instantiate(bombPrefab, Vector3.zero, Quaternion.identity);
        Bomb bombScript = bombInstance.GetComponent<Bomb>();
        if (bombScript != null)
        {
            bombScript.AssignToPlayer(randomPlayer);
            SetupPlayerInputs(players, bombScript);
        }
        else
        {
            Debug.LogError("Bomb script missing on instantiated bomb!");
        }
    }

    void SetupPlayerInputs(GameObject[] players, Bomb bombScript)
    {
        foreach (GameObject player in players)
        {
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                // Dynamically assign SwapBomb event
                playerInput.actions.FindAction("SwapBomb").performed += ctx => bombScript.OnSwapBomb(ctx.ReadValueAsButton());
            }
        }
    }
}