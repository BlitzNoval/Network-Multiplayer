using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject bombPrefab;

    [Header("UI")]
    public GameUI ui;

    [SyncVar] public bool GameActive;
    [SyncVar(hook = nameof(OnPauseStateChanged))] public bool IsPaused;
    [SyncVar(hook = nameof(OnPauserChanged))] public NetworkIdentity Pauser;

    readonly List<GameObject> players = new();
    public IReadOnlyList<GameObject> ActivePlayers => players;
    private static int nextPlayerNumber = 1;
    GameObject bomb;

    public Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();

    public event Action<bool, bool> IsPausedChanged;
    public event Action<NetworkIdentity, NetworkIdentity> PauserChanged;

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
            return;
        }
        Debug.Log("GameManager initialized", this);
    }

    void OnEnable()
    {
        Bomb.OnBombExplodedGlobal += HandleBombExploded;
        Debug.Log("GameManager OnEnable: Subscribed to events", this);
    }

    void OnDisable()
    {
        Bomb.OnBombExplodedGlobal -= HandleBombExploded;
        Debug.Log("GameManager OnDisable: Unsubscribed from events", this);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Debug.Log("GameManager destroyed", this);
    }

    public override void OnStartServer()
    {
        ResetState();
        GameActive = true;
        StartCoroutine(RoundLoop());
        Debug.Log("OnStartServer: Game started", this);
    }

    [Server]
    public void ResetState()
    {
        nextPlayerNumber = 1;
        players.Clear();
        playerObjects.Clear();
        IsPaused = false;
        Pauser = null;
        bomb = null;
        Debug.Log("ResetState: Cleared game state", this);
    }

    [Server]
    IEnumerator RoundLoop()
    {
        yield return Countdown();
        SpawnBomb();

        while (GameActive)
        {
            if (IsPaused)
            {
                yield return null;
                continue;
            }
            yield return null;
        }
        yield return new WaitForSeconds(5f);

        NetworkManager.singleton.StopHost();
    }

    [Server]
    IEnumerator Countdown()
    {
        for (int i = 9; i > 0; i--)
        {
            while (IsPaused) yield return null;
            RpcCountdown(i.ToString());
            yield return new WaitForSeconds(1f);
        }
        while (IsPaused) yield return null;
        RpcCountdown("GO!!!");
        yield return new WaitForSeconds(1f);
        RpcHideCountdown();
    }

    [ClientRpc]
    void RpcCountdown(string txt)
    {
        if (ui == null)
        {
            Debug.LogWarning("GameManager.ui is null, countdown text cannot be displayed", this);
            return;
        }
        Debug.Log($"RpcCountdown: Sending '{txt}' to UI", this);
        ui.ShowCountdown(txt);
    }

    [ClientRpc]
    void RpcHideCountdown()
    {
        if (ui == null)
        {
            Debug.LogWarning("GameManager.ui is null, cannot hide countdown", this);
            return;
        }
        ui.HideCountdown();
    }

    [Server]
    public void RegisterPlayer(GameObject player)
    {
        if (player == null || players.Contains(player))
        {
            Debug.LogWarning($"RegisterPlayer: Player is null or already registered", this);
            return;
        }

        players.Add(player);
        var lifeManager = player.GetComponent<PlayerLifeManager>();
        if (lifeManager != null)
        {
            lifeManager.PlayerNumber = nextPlayerNumber;
            nextPlayerNumber++;
            if (nextPlayerNumber > 4) nextPlayerNumber = 1;
            Debug.Log($"RegisterPlayer: Assigned PlayerNumber={lifeManager.PlayerNumber} to {player.name}", this);
        }
        else
        {
            Debug.LogError($"RegisterPlayer: PlayerLifeManager missing on {player.name}", this);
        }

        var playerInfo = player.GetComponent<PlayerInfo>();
        if (playerInfo != null)
        {
            playerObjects[playerInfo.playerName] = player;
            Debug.Log($"RegisterPlayer: Added {playerInfo.playerName} to playerObjects", this);
        }
    }

    [Server]
    public void UnregisterPlayer(GameObject p)
    {
        if (p != null && players.Remove(p))
        {
            var playerInfo = p.GetComponent<PlayerInfo>();
            if (playerInfo != null)
                playerObjects.Remove(playerInfo.playerName);
            Debug.Log($"Unregistered player {p.name}, remaining players: {players.Count}", this);
            CheckWinner();
        }
    }

    void CheckWinner()
    {
        int alive = 0;
        GameObject win = null;
        foreach (var p in players.ToArray())
        {
            if (p == null)
            {
                players.Remove(p);
                continue;
            }
            var life = p.GetComponent<PlayerLifeManager>();
            if (life && !life.IsDisconnected && life.CurrentLives > 0)
            {
                alive++;
                win = p;
            }
        }
        if (alive == 1 && win != null)
        {
            string winnerName = win.GetComponent<PlayerInfo>()?.playerName ?? $"Player {win.GetComponent<PlayerLifeManager>().PlayerNumber}";
            Debug.Log($"Winner determined: {winnerName}", this);
            RpcShowWinner(winnerName);
            GameActive = false;
        }
        else if (alive == 0)
        {
            GameActive = false;
        }
    }

    [Server]
    public void SpawnBomb()
    {
        if (bomb || players.Count == 0)
        {
            Debug.LogWarning($"SpawnBomb: Bomb exists or no players (players={players.Count})", this);
            return;
        }

        players.RemoveAll(p => p == null);
        if (players.Count == 0)
        {
            Debug.LogWarning("SpawnBomb: No valid players to spawn bomb", this);
            return;
        }

        GameObject target = players[UnityEngine.Random.Range(0, players.Count)];
        bomb = Instantiate(bombPrefab, target.transform.position + Vector3.up, Quaternion.identity);
        NetworkServer.Spawn(bomb);
        bomb.GetComponent<Bomb>().AssignToPlayer(target);
        Debug.Log($"SpawnBomb: Bomb assigned to {target.name}", this);
    }

    [Server]
    void HandleBombExploded()
    {
        bomb = null;
        StartCoroutine(RespawnBombAfterDelay());
    }

    [Server]
    IEnumerator RespawnBombAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        SpawnBomb();
    }

    [ClientRpc]
    void RpcShowWinner(string name) => ui?.ShowWinner(name);

    [Server]
    public void PauseGame(NetworkIdentity pauser)
    {
        if (!IsPaused)
        {
            IsPaused = true;
            Pauser = pauser;
            Debug.Log($"PauseGame: Paused by {pauser?.netId ?? 0}", this);
        }
    }

    [Server]
    public void ResumeGame(NetworkIdentity pauser)
    {
        if (IsPaused && (pauser == null || Pauser == pauser))
        {
            IsPaused = false;
            Pauser = null;
            Debug.Log("ResumeGame: Game resumed", this);
        }
    }

    [Server]
    void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"OnServerDisconnect: Connection {conn.connectionId} disconnected", this);
        foreach (var player in players.ToArray())
        {
            var netId = player.GetComponent<NetworkIdentity>();
            if (netId != null && netId.connectionToClient == conn)
            {
                UnregisterPlayer(player);
            }
        }
        if (NetworkServer.active && players.Count == 0)
        {
            ResetState();
            NetworkManager.singleton.StopHost();
        }
    }

    [ServerCallback]
    void Update()
    {
        if (IsPaused && Pauser != null && Pauser.connectionToClient == null)
        {
            ResumeGame(null);
            Debug.Log("Update: Pauser disconnected, resuming game", this);
        }
    }

    void OnPauseStateChanged(bool oldValue, bool newValue)
    {
        IsPausedChanged?.Invoke(oldValue, newValue);
    }

    void OnPauserChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
    {
        PauserChanged?.Invoke(oldValue, newValue);
    }

    [Server]
    public GameObject GetNextPlayer(GameObject excludePlayer)
    {
        var alivePlayers = players.Where(p => p != excludePlayer && p.GetComponent<PlayerLifeManager>().CurrentLives > 0).ToList();
        if (alivePlayers.Count == 0) return null;
        return alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];
    }

    [Server]
    public bool IsPlayerActive(GameObject player)
    {
        return players.Contains(player);
    }
}