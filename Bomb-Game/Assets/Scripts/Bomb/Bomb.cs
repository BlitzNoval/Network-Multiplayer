using System;
using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bomb : NetworkBehaviour
{
    public static event Action OnBombExplodedGlobal;

    [Header("Timer")]
    [SyncVar] [SerializeField] float initialTimer        = 10f;
    [SyncVar] [SerializeField] float returnPauseDuration = 2f;

    [Header("Throw")]
    [SyncVar] [SerializeField] float normalThrowSpeed      = 20f;
    [SyncVar] [SerializeField] float normalThrowUpward     =  2f;
    [SyncVar] [SerializeField] float lobThrowSpeed         = 10f;
    [SyncVar] [SerializeField] float lobThrowUpward        =  5f;
    [SyncVar] [SerializeField] float throwCooldown         =  0.5f;
    [SyncVar] [SerializeField] float flightMassMultiplier  =  1f;

    [Header("Collision / Explosion")]
    [SyncVar] [SerializeField] int   maxBounces            = 1;
    [SyncVar] [SerializeField] float groundExplosionDelay  = 1f;

    [SerializeField] LayerMask mapLayerMask;
    [SerializeField] string playerTag = "Player";
    [SerializeField] string mapOutTag = "MapOut";

    // Knockback calculator reference
    private KnockbackCalculator knockbackCalculator;

    public void SetInitialTimer(float v)        => initialTimer        = v;
    public void SetReturnPauseDuration(float v) => returnPauseDuration = v;
    public void SetNormalThrowSpeed(float v)    => normalThrowSpeed    = v;
    public void SetNormalThrowUpward(float v)   => normalThrowUpward   = v;
    public void SetLobThrowSpeed(float v)       => lobThrowSpeed       = v;
    public void SetLobThrowUpward(float v)      => lobThrowUpward      = v;
    public void SetThrowCooldown(float v)       => throwCooldown       = v;
    public void SetFlightMassMultiplier(float v)=> flightMassMultiplier= v;
    public void SetMaxBounces(int v)            => maxBounces          = v;
    public void SetGroundExplosionDelay(float v)=> groundExplosionDelay= v;

    Rigidbody       rb;
    Collider        col;
    BombEffects     fx;
    TextMeshProUGUI timerText;
    Transform       canvasTr;

    [SyncVar(hook = nameof(OnHolderChanged))] GameObject holder;
    [SyncVar] bool  isOnRight   = true;
    [SyncVar] bool  isHeld      = true;
    [SyncVar] float currentTimer;

    float       lastThrowTime, groundHitTime;
    bool        waitingToExplode, returnPause;
    float       returnPauseStart;
    int         currentBounces;
    GameObject  lastThrower;

    public GameObject Holder            => holder;
    public bool       IsOnRight         => isOnRight;
    public bool       IsHeld            => isHeld;
    public float      CurrentTimer      => currentTimer;
    public float      NormalThrowSpeed  => normalThrowSpeed;
    public float      NormalThrowUpward => normalThrowUpward;
    public float      LobThrowSpeed     => lobThrowSpeed;
    public float      LobThrowUpward    => lobThrowUpward;

    void Awake()
    {
        rb        = GetComponent<Rigidbody>();
        col       = GetComponent<Collider>();
        fx        = GetComponent<BombEffects>();
        timerText = GetComponentInChildren<TextMeshProUGUI>();
        if (timerText)
            canvasTr = timerText.transform.parent;
        currentTimer = initialTimer;
        
        // Add knockback calculator if not present
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

    void LateUpdate()
    {
        if (canvasTr && Camera.main)
            canvasTr.rotation = Quaternion.LookRotation(canvasTr.position - Camera.main.transform.position);
    }

    [Server]
    void TickTimer()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        if (waitingToExplode)
        {
            if (Time.time >= groundHitTime + groundExplosionDelay)
                Explode();
            return;
        }

        if (returnPause)
        {
            if (Time.time >= returnPauseStart + returnPauseDuration)
                returnPause = false;
            return;
        }

        currentTimer -= Time.deltaTime;
        RpcUpdateTimer(Mathf.CeilToInt(currentTimer));

        if (currentTimer <= 0f && !waitingToExplode)
        {
            currentTimer = 0f;
            Explode();
        }
    }

    [ClientRpc]
    void RpcUpdateTimer(int seconds)
    {
        if (timerText)
            timerText.text = seconds.ToString();
    }

    [Server]
    void Explode()
    {
        if (waitingToExplode) return;
        waitingToExplode = true;

        Vector3 explosionPos = transform.position;
        
        // Debug visualization
        knockbackCalculator.DrawDebugSectors(explosionPos);
        
        // Get all potential targets
        var potentialTargets = Physics.OverlapSphere(explosionPos, knockbackCalculator.GetComponent<KnockbackCalculator>() ? 
            5f : 5f); // Use knockback calculator's explosion radius if available
        
        foreach (var hit in potentialTargets)
        {
            if (!hit.CompareTag(playerTag)) continue;
            
            var lifeManager = hit.GetComponent<PlayerLifeManager>();
            if (lifeManager == null) continue;
            
            var rb = hit.GetComponent<Rigidbody>();
            if (rb == null) continue;
            
            // Calculate knockback using our advanced system
            bool isHolder = hit.gameObject == holder;
            float percentageKnockback = lifeManager.PercentageKnockback;
            
            var knockbackResult = knockbackCalculator.CalculateKnockback(
                explosionPos, 
                hit.gameObject, 
                percentageKnockback, 
                isHolder
            );
            
            if (!knockbackResult.affected) continue;
            
            // Apply physics force locally for immediate feedback
            rb.AddForce(knockbackResult.force * 0.2f, ForceMode.Impulse);
            
            // Send targeted RPC for authoritative knockback
            if (hit.TryGetComponent(out NetworkIdentity ni) && ni.connectionToClient != null)
            {
                lifeManager.TargetApplyKnockback(ni.connectionToClient, knockbackResult.force);
            }
            
            // Register the hit and add explosion percentage
            lifeManager.RegisterKnockbackHit();
            lifeManager.AddExplosionKnockbackPercentage(knockbackResult.sector);
        }

        // Clear bomb from holder
        holder?.GetComponent<PlayerBombHandler>()?.ClearBomb();
        holder = null;

        RpcPlayExplosion();
        OnBombExplodedGlobal?.Invoke();
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

    [Server]
    public void AssignToPlayer(GameObject p)
    {
        holder = p;
        isHeld = true;
        col.isTrigger = true;
        currentBounces = 0;
        rb.isKinematic = true;
        rb.mass = 1f;
        returnPause = false;
        currentTimer = Mathf.Max(currentTimer, 3f);
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
            : forward * lobThrowSpeed   + Vector3.up * lobThrowUpward;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        currentBounces = 0;
        lastThrowTime  = Time.time;
        holder = null;

        StartCoroutine(ReturnToThrowerAfterDelay());
    }

    [Server]
    IEnumerator ReturnToThrowerAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (!isHeld && lastThrower != null && GameManager.Instance.IsPlayerActive(lastThrower))
            AssignToPlayer(lastThrower);
    }

    [ServerCallback]
    void OnCollisionEnter(Collision c)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        if (c.gameObject.CompareTag(playerTag))
        {
            if (c.gameObject != holder)
                AssignToPlayer(c.gameObject);
            return;
        }

        if (isHeld) return;

        if (c.gameObject.CompareTag(mapOutTag) ||
            ((1 << c.gameObject.layer & mapLayerMask) != 0))
        {
            if (lastThrower != null)
            {
                returnPause = true;
                returnPauseStart = Time.time;
                AssignToPlayer(lastThrower);
            }
            return;
        }

        currentBounces++;
        if (currentBounces >= maxBounces)
        {
            waitingToExplode = true;
            groundHitTime    = Time.time;
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
        RpcUpdateTimer(Mathf.CeilToInt(currentTimer));
    }

    [Server]
    public void TriggerImmediateExplosion() => Explode();
    
    // Debug toggle for knockback visualization
    public void SetKnockbackDebugMode(bool enabled)
    {
        if (knockbackCalculator != null)
            knockbackCalculator.SetDebugMode(enabled);
    }
}