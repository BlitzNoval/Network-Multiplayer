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
    [SerializeField] private float maxKnockbackPercentage = 500f; // Max knockback percentage
    
    
    [Header("Camera View Elimination")]
    [SerializeField] private float outOfViewTimeLimit = 5f; // Time out of camera view before elimination
    [SerializeField] private float cameraCheckInterval = 0.5f; // How often to check camera view
    private float timeOutOfView = 0f;
    private float lastCameraCheckTime = 0f;
    
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
        timeOutOfView       = 0f;
        lastCameraCheckTime = 0f;
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
                
                // Get dynamic rate from KnockbackCalc and apply increase
                KnockbackCalculator knockbackCalc = FindAnyObjectByType<KnockbackCalculator>();
                float rate = knockbackCalc != null ? knockbackCalc.GetDynamicKnockbackRate(percentageKnockback) : 10f;
                float increase = rate * Time.deltaTime;
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

        // Check if player is out of camera view
        if (Time.time - lastCameraCheckTime >= cameraCheckInterval)
        {
            lastCameraCheckTime = Time.time;
            
            if (IsPlayerOutOfCameraView())
            {
                timeOutOfView += cameraCheckInterval;
                if (timeOutOfView >= outOfViewTimeLimit)
                {
                    RpcLogToClient($"Player {gameObject.name} eliminated: out of camera view for {timeOutOfView:F1}s (limit: {outOfViewTimeLimit}s)");
                    HandleDeath();
                }
            }
            else
            {
                timeOutOfView = 0f; // Reset timer when back in view
            }
        }
        
        // Keep basic fall-off check as backup
        if (SpawnManager.Instance != null && SpawnManager.Instance.respawnReference != null)
        {
            float referenceY = SpawnManager.Instance.respawnReference.position.y;
            float threshold = referenceY - SpawnManager.Instance.respawnOffset;
            if (transform.position.y < threshold)
            {
                RpcLogToClient($"Player {gameObject.name} fell below threshold: position={transform.position}, threshold={threshold}");
                HandleDeath();
            }
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
        timeOutOfView       = 0f;
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
        // Don't disable movement completely - instead reduce effectiveness and apply gradual force
        StartCoroutine(ApplyDynamicKnockback(force));
    }

    [TargetRpc]
    public void TargetSyncExplosionData(NetworkConnectionToClient _, Vector3 explosionPos, float magnitude, int sector)
    {
        // Client-side explosion data for validation and effects
        // This helps ensure knockback feels consistent even with network latency
        if (KnockbackCalculator.GlobalDebugEnabled)
        {
            Debug.Log($"Client received explosion data: pos={explosionPos}, magnitude={magnitude:F1}, sector={sector}", this);
        }
        
        // Optional: Add client-side prediction validation here
        // Could check if client position matches expected knockback trajectory
    }

    IEnumerator ApplyDynamicKnockback(Vector3 totalForce)
    {
        // Smooth force application over short time
        if (rb != null)
        {
            float smoothTime = 0.1f; // Very short smoothing time
            int steps = 5; // Apply over 5 frames
            Vector3 forcePerStep = totalForce / steps;
            
            for (int i = 0; i < steps; i++)
            {
                if (rb != null)
                    rb.AddForce(forcePerStep, ForceMode.VelocityChange);
                yield return new WaitForFixedUpdate();
            }
        }
        
        // Calculate movement reduction based on force strength
        float forceStrength = totalForce.magnitude;
        float movementReduction = Mathf.Clamp(0.2f + (forceStrength * 0.01f), 0.2f, 0.6f);
        float movementReductionTime = Mathf.Clamp(0.3f + (forceStrength * 0.02f), 0.3f, 0.8f);
        
        // Apply movement reduction during knockback
        if (movement != null)
        {
            movement.SetKnockbackState(true, movementReduction);
        }
        
        yield return new WaitForSeconds(movementReductionTime);
        
        // Gradually restore movement over short time
        float restoreTime = 0.2f;
        float elapsedTime = 0f;
        
        while (elapsedTime < restoreTime && movement != null)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / restoreTime;
            float currentMultiplier = Mathf.Lerp(movementReduction, 1f, t);
            movement.SetKnockbackState(true, currentMultiplier);
            yield return null;
        }
        
        // Restore full movement
        if (movement != null)
        {
            movement.SetKnockbackState(false, 1f);
        }
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

    bool IsPlayerOutOfCameraView()
    {
        if (Camera.main == null) return false;
        
        // Get the player's position in viewport coordinates
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
        
        // Generous buffer with grace boundary for players on viewport edge
        float buffer = 0.25f; // 25% buffer outside screen edges with grace boundary
        
        // Check if player is within the camera's viewport with generous buffer
        // Special grace boundary: if player is near viewport edge, give them more time
        bool inView = viewportPos.x >= -buffer && viewportPos.x <= 1f + buffer &&
                     viewportPos.y >= -buffer && viewportPos.y <= 1f + buffer &&
                     viewportPos.z > 0; // z > 0 means in front of camera
                     
        // Grace boundary check - if player is on the edge, they get extra leeway
        bool onGraceBoundary = (viewportPos.x >= -0.1f && viewportPos.x <= 1.1f &&
                               viewportPos.y >= -0.1f && viewportPos.y <= 1.1f &&
                               viewportPos.z > 0);
                               
        if (onGraceBoundary && !inView)
        {
            // Player is in grace boundary - reset timer to give them chance to return
            timeOutOfView = Mathf.Max(0f, timeOutOfView - (cameraCheckInterval * 0.5f));
            inView = true; // Treat as in view for this frame
        }
        
        // Additional check: if player is very close to camera but out of view, don't eliminate
        float distanceToCamera = Vector3.Distance(transform.position, Camera.main.transform.position);
        if (distanceToCamera < 5f) // Within 5 units of camera
        {
            inView = true; // Don't eliminate if very close to camera
        }
        
        // Debug log when player goes out of view
        if (!inView)
        {
            RpcLogToClient($"Player out of view: viewport={viewportPos}, distance={distanceToCamera:F1}, graceBoundary={onGraceBoundary}");
        }
        
        return !inView;
    }
}