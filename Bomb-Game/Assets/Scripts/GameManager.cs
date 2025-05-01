using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public GameObject bombPrefab;
    private GameObject bombInstance;

    void Start()
    {
        SpawnBomb();
    }

    void SpawnBomb()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0) return;

        GameObject randomPlayer = players[Random.Range(0, players.Length)];
        bombInstance = Instantiate(bombPrefab, Vector3.zero, Quaternion.identity);
        bombInstance.GetComponent<Bomb>().AssignToPlayer(randomPlayer);
        SetupPlayerInputs(players);
    }

   void SetupPlayerInputs(GameObject[] players) // Adjust method signature as needed
    {
        foreach (GameObject player in players)
        {
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            PlayerBombHandler handler = player.GetComponent<PlayerBombHandler>();
            if (playerInput != null && handler != null)
            {
                // Bind SwapBomb action
                playerInput.actions.FindAction("SwapBomb").performed += ctx => handler.OnSwapBomb(ctx.ReadValueAsButton());
                // Bind Throw action
                playerInput.actions.FindAction("Throw").performed += ctx => handler.OnThrow(ctx.ReadValueAsButton());
            }
        }
    }
}