using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Collider), typeof(Rigidbody))]
public class PlayerLifeManager : NetworkBehaviour
{
    [Header("Lives")]
    [SerializeField] int   maxLives          = 3;
    [SerializeField] float respawnDelay      = 2f;
    [SerializeField] float fallThreshold     = -10f;
    [SerializeField] float absoluteFallLimit = -500f;

    [SyncVar] public int   currentLives;
    [SyncVar] public bool  IsDead;
    [SyncVar] public float knockbackMultiplier = 1f;
    [SyncVar] public float totalHoldTime;
    [SyncVar] public int   knockbackHitCount;

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
        currentLives        = maxLives;
        IsDead              = false;
        knockbackMultiplier = 1f;
        totalHoldTime       = 0f;
        knockbackHitCount   = 0;
    }

    void FixedUpdate()
    {
        if (!isServer || IsDead) return;

        float yPos = transform.position.y;

        if (yPos < fallThreshold)
        {
            HandleDeath();
        } 
        else if (yPos < absoluteFallLimit)
        {
            HandleDeath();
        }


        if (bombHandler?.CurrentBomb != null &&
            bombHandler.CurrentBomb.Holder == gameObject)
        {
            totalHoldTime += Time.deltaTime;
            UpdateKnockbackMultiplier();
        }
    }

    // ───────────── Knock-back multiplier ─────────────
    [Server] void UpdateKnockbackMultiplier()
    {
        float baseMultiplier = 1f;
        float holdTimeFactor = 0.1f;
        float hitFactor      = 0.2f;
        float multiplier     = baseMultiplier *
                               (1 + holdTimeFactor * totalHoldTime) *
                               Mathf.Pow(1 + hitFactor * knockbackHitCount, 2);
        knockbackMultiplier  = Mathf.Min(multiplier, 4f);
    }

    // ───────────── Death / Respawn ─────────────
    [Server] public void HandleDeath()
    {
        if (IsDead) return;

        currentLives--;
        if (currentLives <= 0)
        {
            FinalDeath();
            return;
        }
        StartCoroutine(RespawnRoutine());
    }

    [Server] IEnumerator RespawnRoutine()
    {
        SetAliveState(false, true);
        yield return new WaitForSeconds(respawnDelay);

        while (true)
        {
            if (SpawnManager.Instance == null) { yield return new WaitForSeconds(0.1f); continue; }

            Transform spawnPoint = SpawnManager.Instance.GetNextSpawnPoint();
            if (spawnPoint == null)            { yield return new WaitForSeconds(0.1f); continue; }

            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            knockbackMultiplier = 1f;
            totalHoldTime       = 0f;
            knockbackHitCount   = 0;
            SetAliveState(true, false);
            break;
        }
    }

    [Server] void FinalDeath()
    {
        if (bombHandler && bombHandler.CurrentBomb)
            bombHandler.CurrentBomb.TriggerImmediateExplosion();

        GameManager.Instance?.UnregisterPlayer(gameObject);
        NetworkServer.Destroy(gameObject);
    }

    [Server] public void RegisterKnockbackHit()
    {
        knockbackHitCount++;
        UpdateKnockbackMultiplier();
    }

    // ───────────── Knock-back RPC ─────────────
    [TargetRpc]
    public void TargetApplyKnockback(NetworkConnectionToClient _, Vector3 force)
    {
        movement.enabled = false;
        StartCoroutine(ReEnableMovement(0.5f));

        if (TryGetComponent<Rigidbody>(out var rb))
            rb.AddForce(force, ForceMode.Impulse);
    }

    IEnumerator ReEnableMovement(float delay)
    {
        yield return new WaitForSeconds(delay);
        movement.enabled = true;
    }

    // ───────────── Helpers ─────────────
    void SetAliveState(bool state, bool triggerMode)
    {
        IsDead           = !state;
        movement.enabled = state;
        col.enabled      = true;
        col.isTrigger    = triggerMode;
        rb.isKinematic   = !state;
        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
    }
}
