using System.Collections;
using Mirror;
using UnityEngine;
using System;

[RequireComponent(typeof(PlayerMovement), typeof(Collider), typeof(Rigidbody))]
public class PlayerLifeManager : NetworkBehaviour
{
    [Header("Lives")]
    [SyncVar] [SerializeField] int   maxLives           = 3;
    [SyncVar] [SerializeField] float respawnDelay      = 2f;

    [SyncVar] public bool IsDisconnected;

    public void SetMaxLives(int v)            => maxLives           = v;
    public void SetRespawnDelay(float v)      => respawnDelay       = v;

    [SyncVar] public bool  IsDead;
    [SyncVar] public float TotalHoldTime;
    [SyncVar] public int   KnockbackHitCount;

    [SyncVar] public int   PlayerNumber;

    [SyncVar(hook = nameof(OnCurrentLivesChanged))]
    public int CurrentLives;
    [SyncVar(hook = nameof(OnKnockbackMultiplierChanged))]
    public float KnockbackMultiplier = 1f;

    public event Action<int,int>     OnLivesChanged;
    public event Action<float,float> OnKnockbackChanged;

    PlayerBombHandler bombHandler;
    PlayerMovement    movement;
    Collider          col;
    Rigidbody         rb;

    private bool isRespawning; // Server-side flag to prevent multiple death triggers
    private float lastRespawnTime; // Time when the last respawn occurred
    private const float gracePeriod = 0.5f; // Grace period in seconds after respawn

    void Awake()
    {
        bombHandler = GetComponent<PlayerBombHandler>();
        movement    = GetComponent<PlayerMovement>();
        col         = GetComponent<Collider>();
        rb          = GetComponent<Rigidbody>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        CurrentLives        = maxLives;
        IsDead              = false;
        IsDisconnected      = false;
        KnockbackMultiplier = 1f;
        TotalHoldTime       = 0f;
        KnockbackHitCount   = 0;
        isRespawning        = false;
        lastRespawnTime     = -gracePeriod; // Initialize to allow immediate checks
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.Register(this);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.Unregister(this);
    }

    void FixedUpdate()
    {
        if (!isServer || IsDead || IsDisconnected || isRespawning || (GameManager.Instance != null && GameManager.Instance.IsPaused))
            return;

        // Skip threshold check during grace period after respawn
        if (Time.time - lastRespawnTime < gracePeriod)
            return;

        if (SpawnManager.Instance != null && SpawnManager.Instance.respawnReference != null)
        {
            float referenceY = SpawnManager.Instance.respawnReference.position.y;
            float threshold = referenceY - SpawnManager.Instance.respawnOffset;
            if (transform.position.y < threshold)
            {
                RpcLogToClient($"Player {gameObject.name} below threshold: position={transform.position}, threshold={threshold}, IsDead={IsDead}");
                HandleDeath();
            }
        }
        else
        {
            RpcLogWarningToClient("SpawnManager.Instance or respawnReference is null");
        }

        if (bombHandler?.CurrentBomb != null && bombHandler.CurrentBomb.Holder == gameObject)
        {
            TotalHoldTime += Time.deltaTime;
            UpdateKnockbackMultiplier();
        }
    }

    [ClientRpc]
    void RpcLogToClient(string message)
    {
        Debug.Log(message, this);
    }

    [ClientRpc]
    void RpcLogWarningToClient(string message)
    {
        Debug.LogWarning(message, this);
    }

    [Server]
    void UpdateKnockbackMultiplier()
    {
        float baseMult     = 1f;
        float holdFactor   = 0.1f;
        float hitFactor    = 0.2f;
        float mult         = baseMult *
                             (1 + holdFactor * TotalHoldTime) *
                             Mathf.Pow(1 + hitFactor * KnockbackHitCount, 2);
        KnockbackMultiplier = Mathf.Min(mult, 4f);
    }

    void OnCurrentLivesChanged(int oldLives, int newLives)
        => OnLivesChanged?.Invoke(oldLives, newLives);

    void OnKnockbackMultiplierChanged(float oldM, float newM)
        => OnKnockbackChanged?.Invoke(oldM, newM);

    [Server]
    public void HandleDeath()
    {
        if (IsDead || isRespawning) return;
        RpcLogToClient($"HandleDeath called for {gameObject.name}, CurrentLives={CurrentLives}");
        isRespawning = true;
        IsDead = true;
        CurrentLives--;
        if (CurrentLives <= 0) { FinalDeath(); return; }
        StartCoroutine(RespawnRoutine());
    }

    [Server]
    IEnumerator RespawnRoutine()
    {
        RpcLogToClient($"RespawnRoutine started for {gameObject.name}");
        SetAliveState(false, true);
        yield return new WaitForSeconds(respawnDelay);

        while (SpawnManager.Instance == null || SpawnManager.Instance.GetNextSpawnPoint() == null)
        {
            RpcLogWarningToClient("Waiting for SpawnManager or spawn point");
            yield return new WaitForSeconds(0.1f);
        }

        var spawn = SpawnManager.Instance.GetNextSpawnPoint();
        float threshold = SpawnManager.Instance.respawnReference.position.y - SpawnManager.Instance.respawnOffset;
        RpcLogToClient($"Teleporting to spawn point: position={spawn.position}, threshold={threshold}");
        if (spawn.position.y < threshold)
        {
            RpcLogErrorToClient("Spawn point is below the threshold!");
        }

        Vector3 newPosition = spawn.position + Vector3.up * 0.5f;
        rb.position = newPosition;
        rb.rotation = spawn.rotation;
        rb.linearVelocity = Vector3.zero;
        RpcTeleport(newPosition, spawn.rotation);
        RpcLogToClient($"After teleport on server: position={rb.position}");

        KnockbackMultiplier = 1f;
        TotalHoldTime       = 0f;
        KnockbackHitCount   = 0;
        SetAliveState(true, false);
        isRespawning = false;
        lastRespawnTime = Time.time; // Set the last respawn time for grace period
        RpcLogToClient($"RespawnRoutine completed for {gameObject.name}");
    }

    [ClientRpc]
    void RpcTeleport(Vector3 position, Quaternion rotation)
    {
        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
        }
        transform.position = position;
        transform.rotation = rotation;
        Debug.Log($"Client teleported to {position}", this);
    }

    [ClientRpc]
    void RpcLogErrorToClient(string message)
    {
        Debug.LogError(message, this);
    }

    [Server]
    void FinalDeath()
    {
        if (bombHandler?.CurrentBomb != null)
        {
            Bomb bomb = bombHandler.CurrentBomb;
            bomb.ResetTimer();
            GameObject nextPlayer = GameManager.Instance.GetNextPlayer(gameObject);
            if (nextPlayer != null)
                bomb.AssignToPlayer(nextPlayer);
            else
                bomb.TriggerImmediateExplosion();
        }
        GameManager.Instance?.UnregisterPlayer(gameObject);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    public void RegisterKnockbackHit()
    {
        KnockbackHitCount++;
        UpdateKnockbackMultiplier();
    }

    [TargetRpc]
    public void TargetApplyKnockback(NetworkConnectionToClient _, Vector3 force)
    {
        movement.enabled = false;
        rb.AddForce(force, ForceMode.Impulse);
        StartCoroutine(ReEnableMovement(0.5f));
    }

    IEnumerator ReEnableMovement(float delay)
    {
        yield return new WaitForSeconds(delay);
        movement.enabled = true;
    }

    [Server]
    void SetAliveState(bool alive, bool triggerMode)
    {
        IsDead           = !alive;
        movement.enabled = alive;
        col.enabled      = true;
        col.isTrigger    = triggerMode;
        rb.isKinematic   = !alive;
        if (!rb.isKinematic)
            rb.linearVelocity = Vector3.zero;

        RpcSetAliveState(alive, triggerMode);
    }

    [ClientRpc]
    void RpcSetAliveState(bool alive, bool triggerMode)
    {
        movement.enabled = alive;
        col.isTrigger    = triggerMode;
        if (rb != null)
        {
            rb.isKinematic = !alive;
            if (!rb.isKinematic)
                rb.linearVelocity = Vector3.zero;
        }
    }
}