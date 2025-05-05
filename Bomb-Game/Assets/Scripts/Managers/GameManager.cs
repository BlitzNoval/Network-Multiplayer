using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject bombPrefab;

    [Header("UI")]
    public GameUI ui;       

    [SyncVar] public bool GameActive;

    readonly List<GameObject> players = new();
    public IReadOnlyList<GameObject> ActivePlayers => players;
    GameObject bomb;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void OnEnable()
    {
        Bomb.OnBombExplodedGlobal += HandleBombExploded;
    }

    void OnDisable()
    {
        Bomb.OnBombExplodedGlobal -= HandleBombExploded;
    }

    public override void OnStartServer()
    {
        StartCoroutine(RoundLoop());
    }

    [Server] IEnumerator RoundLoop()
    {
        GameActive = true;
        yield return Countdown();
        SpawnBomb();

        while (GameActive) yield return null;
        yield return new WaitForSeconds(5f);

        NetworkManager.singleton.StopHost();
    }

    [Server] IEnumerator Countdown()
    {
        // Count down from 9 to 1 (total 9 seconds until bomb spawns)
        for (int i = 9; i > 0; i--)
        {
            RpcCountdown(i.ToString());
            yield return new WaitForSeconds(1f);
        }
        RpcCountdown("GO!!!");
        yield return new WaitForSeconds(1f);
        RpcHideCountdown();
    }

    [ClientRpc] void RpcCountdown(string txt)
    {
        if (ui == null)
        {
            Debug.LogWarning("GameManager.ui is null, countdown text cannot be displayed", this);
            return;
        }
        Debug.Log($"RpcCountdown: Sending '{txt}' to UI", this);
        ui.ShowCountdown(txt);
    }

    [ClientRpc] void RpcHideCountdown()
    {
        if (ui == null)
        {
            Debug.LogWarning("GameManager.ui is null, cannot hide countdown", this);
            return;
        }
        ui.HideCountdown();
    }

    [Server] public void RegisterPlayer(GameObject p)
    {
        if (p != null && !players.Contains(p))
        {
            players.Add(p);
            Debug.Log($"Registered player {p.name}, total players: {players.Count}", this);
        }
    }

    [Server] public void UnregisterPlayer(GameObject p)
    {
        if (p != null && players.Remove(p))
        {
            Debug.Log($"Unregistered player {p.name}, remaining players: {players.Count}", this);
            CheckWinner();
        }
        else if (p == null)
        {
            Debug.LogWarning($"Attempted to unregister null player, current players: {players.Count}", this);
        }
    }

    void CheckWinner()
    {
        int alive = 0;
        GameObject win = null;
        foreach (var p in players)
        {
            if (p == null)
            {
                Debug.LogWarning($"Found null player in list, removing", this);
                players.Remove(p);
                continue;
            }
            var life = p.GetComponent<PlayerLifeManager>();
            if (life && life.currentLives > 0)
            {
                alive++;
                win = p;
            }
        }
        Debug.Log($"CheckWinner: alive players={alive}, win={win?.name}, total players={players.Count}", this);
        if (alive == 1 && win != null)
        {
            string winnerName = win.GetComponent<MyRoomPlayer>()?.playerName;
            if (string.IsNullOrEmpty(winnerName))
            {
                Debug.LogWarning($"Winner {win.name} has no valid playerName, falling back to object name", this);
                winnerName = win.name;
            }
            else
            {
                Debug.Log($"Winner name set to entered name: {winnerName}", this);
            }
            RpcShowWinner(winnerName);
            GameActive = false;
        }
        else if (alive == 0)
        {
            Debug.LogWarning("No players alive, game should end", this);
            GameActive = false; // End game if no players remain
        }
    }

    [Server] public void SpawnBomb()
    {
        if (bomb || players.Count == 0) return;

        // Clean up the players list by removing null or destroyed entries
        players.RemoveAll(p => p == null);
        if (players.Count == 0)
        {
            Debug.LogWarning("No valid players to spawn bomb, aborting", this);
            return;
        }

        GameObject target = players[Random.Range(0, players.Count)];
        if (target == null)
        {
            Debug.LogWarning("Selected target is null after cleanup, aborting", this);
            return;
        }

        bomb = Instantiate(bombPrefab, target.transform.position + Vector3.up, Quaternion.identity);
        NetworkServer.Spawn(bomb);
        bomb.GetComponent<Bomb>().AssignToPlayer(target);
    }

    [Server] void HandleBombExploded()
    {
        bomb = null; // Clear reference to allow respawn
        StartCoroutine(RespawnBombAfterDelay());
    }

    [Server] IEnumerator RespawnBombAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        SpawnBomb();
    }

    [ClientRpc] void RpcShowWinner(string name) => ui?.ShowWinner(name);
}