using System;
using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Collider), typeof(Rigidbody))]
public class PlayerLifeManager : NetworkBehaviour
{
    [Header("Lives")]
    [SyncVar] [SerializeField] int   maxLives           = 3;
    [SyncVar] [SerializeField] float respawnDelay      = 2f;
    [SyncVar] [SerializeField] float fallThreshold     = -10f;
    [SyncVar] [SerializeField] float absoluteFallLimit = -500f;

    [SyncVar] public bool IsDisconnected;

    public void SetMaxLives(int v)            => maxLives           = v;
    public void SetRespawnDelay(float v)      => respawnDelay       = v;
    public void SetFallThreshold(float v)     => fallThreshold      = v;
    public void SetAbsoluteFallLimit(float v) => absoluteFallLimit  = v;

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
        if (!isServer || IsDead || IsDisconnected || (GameManager.Instance != null && GameManager.Instance.IsPaused))
            return;

        float y = transform.position.y;
        if (y < fallThreshold || y < absoluteFallLimit)
            HandleDeath();

        if (bombHandler?.CurrentBomb != null &&
            bombHandler.CurrentBomb.Holder == gameObject)
        {
            TotalHoldTime += Time.deltaTime;
            UpdateKnockbackMultiplier();
        }
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
        if (IsDead) return;
        CurrentLives--;
        if (CurrentLives <= 0) { FinalDeath(); return; }
        StartCoroutine(RespawnRoutine());
    }

    [Server]
    IEnumerator RespawnRoutine()
    {
        SetAliveState(false, true);
        yield return new WaitForSeconds(respawnDelay);

        while (SpawnManager.Instance == null ||
               SpawnManager.Instance.GetNextSpawnPoint() == null)
            yield return new WaitForSeconds(0.1f);

        var spawn = SpawnManager.Instance.GetNextSpawnPoint();
        transform.SetPositionAndRotation(spawn.position, spawn.rotation);

        KnockbackMultiplier = 1f;
        TotalHoldTime       = 0f;
        KnockbackHitCount   = 0;
        SetAliveState(true, true);
    }

    [Server]
    void FinalDeath()
    {
        if (bombHandler?.CurrentBomb != null)
            bombHandler.CurrentBomb.TriggerImmediateExplosion();

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

    public void SetAliveState(bool alive, bool triggerMode)
    {
        IsDead           = !alive;
        movement.enabled = alive;
        col.enabled      = true;
        col.isTrigger    = triggerMode;
        rb.isKinematic   = !alive;
        if (!rb.isKinematic)
            rb.linearVelocity = Vector3.zero;
    }
}