using System.Collections;
using Mirror;
using UnityEngine;
using System;
using TMPro;

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

    [SyncVar(hook = nameof(OnPlayerNumberChanged))] public int PlayerNumber;

    [SyncVar(hook = nameof(OnCurrentLivesChanged))] public int CurrentLives;

    [Header("Knockback Settings")]
    [SyncVar(hook = nameof(OnPercentageKnockbackChanged))] 
    private float percentageKnockback = 0f;
    [SerializeField] private float baseKnockbackIncreaseRate = 10f; // Base rate per second
    [SerializeField] private float maxKnockbackPercentage = 350f; // Increased to 350%
    [SerializeField] private float[] milestoneMultipliers = { 1f, 1.2f, 1.4f, 1.6f }; // At 0%, 100%, 200%, 300% - reduced from 1.5, 2, 2.5
    
    // Properties
    public float KnockbackMultiplier => 1f + (percentageKnockback / 100f);
    public float PercentageKnockback => percentageKnockback;

    public event Action<int,int>     OnLivesChanged;
    public event Action<float,float> OnKnockbackPercentageChanged;

    PlayerBombHandler bombHandler;
    PlayerMovement    movement;
    Collider          col;
    Rigidbody         rb;

    private bool isRespawning; // Server-side flag to prevent multiple death triggers
    private float lastRespawnTime; // Time when the last respawn occurred
    private const float gracePeriod = 0.5f; // Grace period in seconds after respawn
    private bool isHoldingBomb;
    private float lastKnockbackTime;

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
        percentageKnockback = 0f;
        TotalHoldTime       = 0f;
        KnockbackHitCount   = 0;
        isRespawning        = false;
        lastRespawnTime     = -gracePeriod; // Initialize to allow immediate checks
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateNameTag(); // Initial tag update
        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.Register(this);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.Unregister(this);
    }

    void Update()
    {
        if (!isServer) return;
        
        // Update knockback percentage while holding bomb
        if (bombHandler != null && bombHandler.CurrentBomb != null)
        {
            if (bombHandler.CurrentBomb.Holder == gameObject)
            {
                if (!isHoldingBomb)
                {
                    isHoldingBomb = true;
                }
                
                // Calculate rate multiplier based on current percentage milestones
                float rateMultiplier = 1f;
                if (percentageKnockback >= 300f) rateMultiplier = milestoneMultipliers[3];
                else if (percentageKnockback >= 200f) rateMultiplier = milestoneMultipliers[2];
                else if (percentageKnockback >= 100f) rateMultiplier = milestoneMultipliers[1];
                else rateMultiplier = milestoneMultipliers[0];
                
                // Increase knockback percentage with milestone multiplier
                float increase = baseKnockbackIncreaseRate * rateMultiplier * Time.deltaTime;
                SetKnockbackPercentage(Mathf.Min(percentageKnockback + increase, maxKnockbackPercentage));
            }
            else if (isHoldingBomb)
            {
                isHoldingBomb = false;
            }
        }
        else if (isHoldingBomb)
        {
            isHoldingBomb = false;
        }
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

        // Update total hold time (legacy system - kept for compatibility)
        if (bombHandler?.CurrentBomb != null && bombHandler.CurrentBomb.Holder == gameObject)
        {
            TotalHoldTime += Time.fixedDeltaTime;
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
    public void SetKnockbackPercentage(float newPercentage)
    {
        percentageKnockback = Mathf.Clamp(newPercentage, 0f, maxKnockbackPercentage);
    }

    void OnCurrentLivesChanged(int oldLives, int newLives)
        => OnLivesChanged?.Invoke(oldLives, newLives);

    void OnPercentageKnockbackChanged(float oldValue, float newValue)
    {
        OnKnockbackPercentageChanged?.Invoke(oldValue, newValue);
    }

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

        // Reset knockback values on respawn
        percentageKnockback = 0f;
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
        lastKnockbackTime = Time.time;
        // The new percentage-based system handles knockback scaling
    }
    
    [Server]
    public void AddExplosionKnockbackPercentage(int sector)
    {
        // Add percentage based on sector (S1=40%, S2=30%, S3=20%, S4=10%)
        float percentageToAdd = 0f;
        switch (sector)
        {
            case 1: percentageToAdd = 40f; break;
            case 2: percentageToAdd = 30f; break;
            case 3: percentageToAdd = 20f; break;
            case 4: percentageToAdd = 10f; break;
        }
        
        if (percentageToAdd > 0)
        {
            SetKnockbackPercentage(Mathf.Min(percentageKnockback + percentageToAdd, maxKnockbackPercentage));
        }
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

    void OnPlayerNumberChanged(int oldNumber, int newNumber)
    {
        UpdateNameTag();
    }

    void UpdateNameTag()
    {
        var nameDisplay = GetComponent<PlayerNameDisplay>();
        if (nameDisplay != null)
        {
            string playerTag = $"P{PlayerNumber}";
            nameDisplay.SetPlayerTag(playerTag);
        }
    }
}