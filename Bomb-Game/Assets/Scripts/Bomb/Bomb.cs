using System;
using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bomb : NetworkBehaviour
{
    public static event Action OnBombExplodedGlobal;

    [Header("Timer")]
    [SyncVar] [SerializeField] float initialTimer = 10f;

    [Header("Throw")]
    [SyncVar] [SerializeField] float normalThrowSpeed = 20f;
    [SyncVar] [SerializeField] float normalThrowUpward = 2f;
    [SyncVar] [SerializeField] float lobThrowSpeed = 10f;
    [SyncVar] [SerializeField] float lobThrowUpward = 5f;
    [SyncVar] [SerializeField] float throwCooldown = 0.5f;
    [SyncVar] [SerializeField] float flightMassMultiplier = 1f;

    [SerializeField] string playerTag = "Player";

    private KnockbackCalculator knockbackCalculator;

    public void SetInitialTimer(float v) => initialTimer = v;
    public void SetNormalThrowSpeed(float v) => normalThrowSpeed = v;
    public void SetNormalThrowUpward(float v) => normalThrowUpward = v;
    public void SetLobThrowSpeed(float v) => lobThrowSpeed = v;
    public void SetLobThrowUpward(float v) => lobThrowUpward = v;
    public void SetThrowCooldown(float v) => throwCooldown = v;
    public void SetFlightMassMultiplier(float v) => flightMassMultiplier = v;

    Rigidbody rb;
    Collider col;
    BombEffects fx;

    [SyncVar(hook = nameof(OnHolderChanged))] GameObject holder;
    [SyncVar] bool isOnRight = true;
    [SyncVar] bool isHeld = true;
    [SyncVar] float currentTimer;

    float lastThrowTime;
    bool exploding = false;
    GameObject lastThrower;

    public GameObject Holder => holder;
    public bool IsOnRight => isOnRight;
    public bool IsHeld => isHeld;
    public float CurrentTimer => currentTimer;
    public float NormalThrowSpeed => normalThrowSpeed;
    public float NormalThrowUpward => normalThrowUpward;
    public float LobThrowSpeed => lobThrowSpeed;
    public float LobThrowUpward => lobThrowUpward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        fx = GetComponent<BombEffects>();
        currentTimer = initialTimer;

        knockbackCalculator = GetComponent<KnockbackCalculator>();
        if (knockbackCalculator == null)
        {
            knockbackCalculator = gameObject.AddComponent<KnockbackCalculator>();
        }
    }

    void Update()
    {
        if (isServer) TickTimer();
    }

    [Server]
    void TickTimer()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        if (!exploding)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer < 0f) currentTimer = 0f;
            int displayTimer = Mathf.CeilToInt(currentTimer);
            RpcUpdateBombTimer(displayTimer);

            if (currentTimer <= 0f)
            {
                Explode();
            }
        }
    }

    [ClientRpc]
    void RpcUpdateBombTimer(int seconds)
    {
        if (GameManager.Instance != null && GameManager.Instance.ui != null)
        {
            GameManager.Instance.ui.UpdateBombTimer(seconds);
        }
    }

    [Server]
    void Explode()
    {
        if (exploding) return;
        exploding = true;

        Vector3 explosionPos = transform.position;

        knockbackCalculator.DrawDebugSectors(explosionPos);

        var potentialTargets = Physics.OverlapSphere(explosionPos, knockbackCalculator.GetComponent<KnockbackCalculator>() ? 5f : 5f);

        foreach (var hit in potentialTargets)
        {
            if (!hit.CompareTag(playerTag)) continue;

            var lifeManager = hit.GetComponent<PlayerLifeManager>();
            if (lifeManager == null) continue;

            var rb = hit.GetComponent<Rigidbody>();
            if (rb == null) continue;

            bool isHolder = hit.gameObject == holder;
            float percentageKnockback = lifeManager.PercentageKnockback;

            var knockbackResult = knockbackCalculator.CalculateKnockback(
                explosionPos,
                hit.gameObject,
                percentageKnockback,
                isHolder
            );

            if (!knockbackResult.affected) continue;

            rb.AddForce(knockbackResult.force * 0.2f, ForceMode.Impulse);

            if (hit.TryGetComponent(out NetworkIdentity ni) && ni.connectionToClient != null)
            {
                lifeManager.TargetApplyKnockback(ni.connectionToClient, knockbackResult.force);
            }

            lifeManager.RegisterKnockbackHit();
            lifeManager.AddExplosionKnockbackPercentage(knockbackResult.sector);
        }

        holder?.GetComponent<PlayerBombHandler>()?.ClearBomb();
        holder = null;

        RpcPlayExplosion();
        OnBombExplodedGlobal?.Invoke();
        RpcHideBombTimer();
        StartCoroutine(DestroyAfterDelay(1f));
    }

    [Server]
    IEnumerator DestroyAfterDelay(float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                yield return null;
            else
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcPlayExplosion()
    {
        if (fx != null && !fx.IsPlayingEffects)
            fx.PlayExplosionEffects();
    }

    [ClientRpc]
    void RpcHideBombTimer()
    {
        if (GameManager.Instance != null && GameManager.Instance.ui != null)
        {
            GameManager.Instance.ui.HideBombTimer();
        }
    }

    [Server]
    public void AssignToPlayer(GameObject p)
    {
        holder = p;
        isHeld = true;
        col.isTrigger = true;
        rb.isKinematic = true;
        rb.mass = 1f;
    }

    void OnHolderChanged(GameObject oldH, GameObject newH)
    {
        oldH?.GetComponent<PlayerBombHandler>()?.ClearBomb();

        if (newH)
        {
            newH.GetComponent<PlayerBombHandler>()?.SetBomb(this);
            Transform grip = newH.transform.Find(isOnRight ? "RightHoldPoint" : "LeftHoldPoint");
            if (grip)
            {
                transform.SetParent(grip);
                transform.localPosition = Vector3.zero;
            }
            rb.isKinematic = true;
            col.isTrigger = true;
            lastThrower = newH;
        }
        else
        {
            transform.SetParent(null);
            rb.isKinematic = false;
            col.isTrigger = false;
        }
    }

    [Server]
    public void SwapHoldPoint()
    {
        isOnRight = !isOnRight;
        RpcRefreshGrip(isOnRight);
    }

    [ClientRpc]
    void RpcRefreshGrip(bool right)
    {
        if (!holder) return;
        Transform grip = holder.transform.Find(right ? "RightHoldPoint" : "LeftHoldPoint");
        if (grip)
        {
            transform.SetParent(grip);
            transform.localPosition = Vector3.zero;
        }
    }

    [Server]
    public void ThrowBomb()
    {
        if (!isHeld || holder == null || Time.time < lastThrowTime + throwCooldown)
            return;

        lastThrower = holder;
        transform.SetParent(null);
        isHeld = false;
        rb.isKinematic = false;
        col.isTrigger = false;
        rb.mass *= flightMassMultiplier;

        Transform origin = holder.transform.Find(isOnRight ? "RightHoldPoint" : "LeftHoldPoint");
        Vector3 forward = origin ? origin.forward : holder.transform.forward;
        Vector3 force = isOnRight
            ? forward * normalThrowSpeed + Vector3.up * normalThrowUpward
            : forward * lobThrowSpeed + Vector3.up * lobThrowUpward;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        lastThrowTime = Time.time;
        holder = null;

        StartCoroutine(ReturnToThrowerAfterDelay());
    }
    
    [Server]
    public void ThrowBomb(Vector3 direction, bool useShortThrow, float elevationMultiplier)
    {
        if (!isHeld || holder == null || Time.time < lastThrowTime + throwCooldown)
            return;

        lastThrower = holder;
        transform.SetParent(null);
        isHeld = false;
        rb.isKinematic = false;
        col.isTrigger = false;
        rb.mass *= flightMassMultiplier;

        // Use provided direction and parameters
        float speed = useShortThrow ? normalThrowSpeed : lobThrowSpeed;
        float baseUpward = useShortThrow ? normalThrowUpward : lobThrowUpward;
        float upward = baseUpward * elevationMultiplier;
        
        Vector3 force = direction.normalized * speed + Vector3.up * upward;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        lastThrowTime = Time.time;
        holder = null;

        StartCoroutine(ReturnToThrowerAfterDelay());
    }

    [Server]
    IEnumerator ReturnToThrowerAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (!isHeld && lastThrower != null && GameManager.Instance.IsPlayerActive(lastThrower) && !exploding)
            AssignToPlayer(lastThrower);
    }

    [ServerCallback]
    void OnCollisionEnter(Collision c)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        if (c.gameObject.CompareTag(playerTag) && c.gameObject != holder)
        {
            AssignToPlayer(c.gameObject);
        }
    }

    [ServerCallback]
    void OnTriggerEnter(Collider other)
    {
        if (isHeld && other.CompareTag(playerTag) && other.gameObject != holder)
            AssignToPlayer(other.gameObject);
    }

    [Server]
    public void ResetTimer()
    {
        currentTimer = initialTimer;
        RpcUpdateBombTimer(Mathf.CeilToInt(currentTimer));
    }

    [Server]
    public void TriggerImmediateExplosion() => Explode();

    public void SetKnockbackDebugMode(bool enabled)
    {
        if (knockbackCalculator != null)
            knockbackCalculator.SetDebugMode(enabled);
    }
}