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
    [SerializeField] private float maxKnockbackPercentage = 500f;

    [Header("Camera View Elimination")]
    [SerializeField] private float outOfViewTimeLimit = 5f;
    [SerializeField] private float cameraCheckInterval = 0.5f;
    private float timeOutOfView = 0f;
    private float lastCameraCheckTime = 0f;

    public float KnockbackMultiplier => 1f + (percentageKnockback / 100f);
    public float PercentageKnockback => percentageKnockback;

    public event Action<int,int>     OnLivesChanged;
    public event Action<float,float> OnKnockbackPercentageChanged;

    PlayerBombHandler bombHandler;
    PlayerMovement    movement;
    Collider          col;
    Rigidbody         rb;

    private bool isRespawning;
    private float lastRespawnTime;
    private const float gracePeriod = 0.5f;
    private bool isHoldingBomb;
    private float lastKnockbackTime;

    public bool isInKnockback = false;
    private bool hasLanded = false;

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
        lastRespawnTime     = -gracePeriod;
        timeOutOfView       = 0f;
        lastCameraCheckTime = 0f;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateNameTag();
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

        if (bombHandler != null && bombHandler.CurrentBomb != null)
        {
            if (bombHandler.CurrentBomb.Holder == gameObject)
            {
                if (!isHoldingBomb)
                {
                    isHoldingBomb = true;
                }

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

        if (Time.time - lastRespawnTime < gracePeriod)
            return;

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
                timeOutOfView = 0f;
            }
        }

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

        percentageKnockback = 0f;
        TotalHoldTime       = 0f;
        KnockbackHitCount   = 0;
        timeOutOfView       = 0f;
        SetAliveState(true, false);
        isRespawning = false;
        lastRespawnTime = Time.time;
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
    }

    [Server]
    public void AddExplosionKnockbackPercentage(int sector)
    {
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
    public void TargetFollowKnockbackArc(NetworkConnectionToClient _, KnockbackArcData arcData)
    {
        StartKnockbackArc(arcData);
    }

    public void StartKnockbackArc(KnockbackArcData arcData)
    {
        PlayerAnimator animator = GetComponent<PlayerAnimator>();
        if (animator != null)
        {
            animator.OnPlayerStunned();
            animator.SetStunned(true);
        }
        StartCoroutine(FollowKnockbackArc(arcData));
    }

    IEnumerator FollowKnockbackArc(KnockbackArcData arcData)
    {
        if (rb == null || arcData.arcPoints == null || arcData.arcPoints.Length == 0)
            yield break;

        movement?.SetKnockbackState(true, 0f);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float elapsedTime = 0f;
        Vector3 airControlOffset = Vector3.zero;

        const float stunPhase = 0.30f;
        const float recoveryPhase = 0.40f;
        const float controlPhase = 0.30f;

        Camera cam = Camera.main;

        isInKnockback = true;
        hasLanded = false;
        CollisionDetectionMode originalCollisionDetectionMode = rb.collisionDetectionMode;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        while (elapsedTime < arcData.duration && !hasLanded)
        {
            elapsedTime += Time.fixedDeltaTime;
            float t = elapsedTime / arcData.duration;

            float airControlMultiplier;
            if (t <= stunPhase)
                airControlMultiplier = 0f;
            else if (t <= stunPhase + recoveryPhase)
                airControlMultiplier = Mathf.Lerp(0f, 0.4f, (t - stunPhase) / recoveryPhase);
            else
                airControlMultiplier = Mathf.Lerp(0.4f, 0.8f, (t - stunPhase - recoveryPhase) / controlPhase);

            movement?.SetKnockbackState(true, airControlMultiplier);

            int idx = Mathf.Clamp(Mathf.FloorToInt(t * (arcData.arcPoints.Length - 1)), 0, arcData.arcPoints.Length - 1);
            Vector3 target = arcData.arcPoints[idx];

            if (idx < arcData.arcPoints.Length - 1)
            {
                float lerpT = (t * (arcData.arcPoints.Length - 1)) - idx;
                target = Vector3.Lerp(target, arcData.arcPoints[idx + 1], lerpT);
            }

            if (airControlMultiplier > 0f && movement != null && movement.isLocalPlayer)
            {
                Vector2 input = movement.GetMoveInput();
                if (input.sqrMagnitude > 0.01f)
                {
                    Vector3 camF = cam.transform.forward; camF.y = 0; camF.Normalize();
                    Vector3 camR = cam.transform.right;   camR.y = 0; camR.Normalize();

                    Vector3 airForce = (camR * input.x + camF * input.y) * airControlMultiplier * 2f * Time.fixedDeltaTime;

                    airControlOffset += airForce;
                    airControlOffset = Vector3.ClampMagnitude(airControlOffset, 3f);
                }
            }

            rb.MovePosition(target + airControlOffset);
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(arcData.endPoint + airControlOffset);
        LandingDotManager.Instance?.HideLandingDotForPlayer(PlayerNumber);
        movement?.SetKnockbackState(false, 1f);
        isInKnockback = false;

        PlayerAnimator animator = GetComponent<PlayerAnimator>();
        if (animator != null)
        {
            animator.SetStunned(false);
        }

        rb.collisionDetectionMode = originalCollisionDetectionMode;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isInKnockback && collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            hasLanded = true;
            PlayerAnimator animator = GetComponent<PlayerAnimator>();
            if (animator != null)
            {
                animator.OnPlayerLanded();
            }
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

        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

        float buffer = 0.25f;

        bool inView = viewportPos.x >= -buffer && viewportPos.x <= 1f + buffer &&
                     viewportPos.y >= -buffer && viewportPos.y <= 1f + buffer &&
                     viewportPos.z > 0;

        bool onGraceBoundary = (viewportPos.x >= -0.1f && viewportPos.x <= 1.1f &&
                               viewportPos.y >= -0.1f && viewportPos.y <= 1.1f &&
                               viewportPos.z > 0);

        if (onGraceBoundary && !inView)
        {
            timeOutOfView = Mathf.Max(0f, timeOutOfView - (cameraCheckInterval * 0.5f));
            inView = true;
        }

        float distanceToCamera = Vector3.Distance(transform.position, Camera.main.transform.position);
        if (distanceToCamera < 5f)
        {
            inView = true;
        }

        if (!inView)
        {
            RpcLogToClient($"Player out of view: viewport={viewportPos}, distance={distanceToCamera:F1}, graceBoundary={onGraceBoundary}");
        }

        return !inView;
    }
}