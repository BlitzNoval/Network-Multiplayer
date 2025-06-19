using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bomb : NetworkBehaviour
{
    public static event Action OnBombExplodedGlobal;

    [Header("Timer")]
    [SyncVar] [SerializeField] float initialTimer = 10f;

    [Header("Throw")]
    [SyncVar] [SerializeField] float underarmThrowSpeed = 20f;
    [SyncVar] [SerializeField] float underarmThrowUpward = 2f;
    [SyncVar] [SerializeField] float lobThrowSpeed = 10f;
    [SyncVar] [SerializeField] float lobThrowUpward = 5f;
    [SyncVar] [SerializeField] float throwCooldown = 0.5f;
    [SyncVar] [SerializeField] float flightMassMultiplier = 1f;

    [SerializeField] string playerTag = "Player";

    private KnockbackCalculator knockbackCalculator;

    public void SetInitialTimer(float v) => initialTimer = v;
    public void SetUnderarmThrowSpeed(float v) => underarmThrowSpeed = v;
    public void SetUnderarmThrowUpward(float v) => underarmThrowUpward = v;
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
    public float NormalThrowSpeed => underarmThrowSpeed;
    public float NormalThrowUpward => underarmThrowUpward;
    public float LobThrowSpeed => lobThrowSpeed;
    public float LobThrowUpward => lobThrowUpward;
    public float FlightMassMultiplier => flightMassMultiplier;

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
        
        Debug.Log($"Bomb Awake: throwCooldown={throwCooldown}, lastThrowTime={lastThrowTime}");
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
        
        List<GameObject> affectedPlayers = new List<GameObject>();
        if (holder != null)
        {
            affectedPlayers.Add(holder);
        }
        
        foreach (var hit in potentialTargets)
        {
            if (!hit.CompareTag(playerTag)) continue;
            if (hit.gameObject == holder) continue;
            
            affectedPlayers.Add(hit.gameObject);
        }
        
        List<LandingDotData> landingData = new List<LandingDotData>();
        
        foreach (var player in affectedPlayers)
        {
            if (player == null) continue;
            
            var lifeManager = player.GetComponent<PlayerLifeManager>();
            if (lifeManager == null) continue;
            
            bool isHolder = (player == holder);
            var arcData = knockbackCalculator.CalculateKnockbackArc(explosionPos, player, lifeManager.PercentageKnockback, isHolder);
            
            if (arcData.affected)
            {
                landingData.Add(new LandingDotData(arcData.endPoint, lifeManager.PlayerNumber));
            }
        }
        
        if (landingData.Count > 0)
        {
            RpcShowLandingDots(explosionPos, landingData.ToArray());
        }
        
        if (holder != null)
        {
            ProcessPlayerKnockback(holder, explosionPos, true);
        }
        
        foreach (var hit in potentialTargets)
        {
            if (!hit.CompareTag(playerTag)) continue;
            if (hit.gameObject == holder) continue;
            
            ProcessPlayerKnockback(hit.gameObject, explosionPos, false);
        }

        holder?.GetComponent<PlayerBombHandler>()?.ClearBomb();
        holder = null;

        RpcPlayExplosion();
        OnBombExplodedGlobal?.Invoke();
        RpcHideBombTimer();
        StartCoroutine(DestroyAfterDelay(1f));
    }
    
    [Server]
    void ProcessPlayerKnockback(GameObject player, Vector3 explosionPos, bool isHolder)
    {
        var lifeManager = player.GetComponent<PlayerLifeManager>();
        if (lifeManager == null) return;

        var rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        float percentageKnockback = lifeManager.PercentageKnockback;

        var arcData = knockbackCalculator.CalculateKnockbackArc(
            explosionPos,
            player,
            percentageKnockback,
            isHolder
        );

        if (!arcData.affected) return;

        if (player.TryGetComponent(out NetworkIdentity ni) && ni.connectionToClient != null)
        {
            lifeManager.TargetFollowKnockbackArc(ni.connectionToClient, arcData);
        }
        else
        {
            lifeManager.StartKnockbackArc(arcData);
        }

        lifeManager.RegisterKnockbackHit();
        lifeManager.AddExplosionKnockbackPercentage(arcData.sector);
        
        Debug.Log($"Applied knockback arc to {player.name}: Distance={Vector3.Distance(arcData.startPoint, arcData.endPoint):F1}m, Percentage={percentageKnockback:F1}%, Sector={arcData.sector}, IsHolder={isHolder}");
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
    void RpcShowLandingDots(Vector3 explosionPos, LandingDotData[] landingData)
    {
        var knockbackCalc = GetComponent<KnockbackCalculator>();
        if (knockbackCalc != null)
        {
            knockbackCalc.ShowLandingDots(landingData);
        }
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
            lastThrower = newH;
            rb.isKinematic = true;
            col.isTrigger = true;
            
            if (isServer)
            {
                RpcUpdateBombPosition(newH, isOnRight);
            }
            else
            {
                PlayerBombHandler handler = newH.GetComponent<PlayerBombHandler>();
                if (handler != null)
                {
                    Transform grip = isOnRight ? handler.rightHandPoint : handler.leftHandPoint;
                    if (grip != null)
                    {
                        transform.SetParent(grip);
                        transform.localPosition = Vector3.zero;
                        transform.localRotation = Quaternion.identity;
                        Debug.Log("Bomb parented to " + grip.name + " on " + newH.name + " (client)");
                    }
                    else
                    {
                        Debug.LogError("Hand point not set in PlayerBombHandler for " + (isOnRight ? "right" : "left") + " hand on " + newH.name);
                    }
                }
                else
                {
                    Debug.LogError("PlayerBombHandler not found on " + newH.name);
                }
            }
        }
        else
        {
            if (isServer)
            {
                RpcReleaseBomb();
            }
            else
            {
                transform.SetParent(null);
            }
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
        PlayerBombHandler handler = holder.GetComponent<PlayerBombHandler>();
        if (handler != null)
        {
            Transform grip = right ? handler.rightHandPoint : handler.leftHandPoint;
            if (grip != null)
            {
                transform.SetParent(grip);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogError("Hand point not set in PlayerBombHandler for " + (right ? "right" : "left") + " hand on " + holder.name);
            }
        }
        else
        {
            Debug.LogError("PlayerBombHandler not found on " + holder.name);
        }
    }

    [ClientRpc]
    void RpcUpdateBombPosition(GameObject newHolder, bool useRightHand)
    {
        if (newHolder == null) return;
        
        PlayerBombHandler handler = newHolder.GetComponent<PlayerBombHandler>();
        if (handler == null)
        {
            Debug.LogError("PlayerBombHandler not found on " + newHolder.name);
            return;
        }
        
        Transform grip = useRightHand ? handler.rightHandPoint : handler.leftHandPoint;
        if (grip == null)
        {
            Debug.LogError("Hand point not set in PlayerBombHandler for " + (useRightHand ? "right" : "left") + " hand on " + newHolder.name);
            return;
        }
        
        transform.SetParent(grip);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        Debug.Log("Bomb parented to " + grip.name + " on " + newHolder.name);
    }

    [ClientRpc]
    void RpcReleaseBomb()
    {
        transform.SetParent(null);
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
            ? forward * underarmThrowSpeed + Vector3.up * underarmThrowUpward
            : forward * lobThrowSpeed + Vector3.up * lobThrowUpward;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        lastThrowTime = Time.time;
        holder = null;

        StartCoroutine(ReturnToThrowerAfterDelay());
    }
    
    [Server]
    public void ThrowBomb(Vector3 direction, bool useNormalThrow)
    {
        Debug.Log($"ThrowBomb: Called with direction={direction}, useNormalThrow={useNormalThrow}, isHeld={isHeld}, holder={(holder != null ? holder.name : "null")}, Time.time={Time.time}, lastThrowTime={lastThrowTime}, throwCooldown={throwCooldown}, cooldownCheck={Time.time < lastThrowTime + throwCooldown}");
        
        if (!isHeld || holder == null || Time.time < lastThrowTime + throwCooldown)
        {
            Debug.LogWarning($"ThrowBomb: REJECTED - isHeld={isHeld}, holder={(holder != null ? holder.name : "null")}, cooldownRemaining={Mathf.Max(0, (lastThrowTime + throwCooldown) - Time.time)}");
            return;
        }

        Transform holdPoint = holder.transform.Find("RightHoldPoint");
        Vector3 throwOrigin = holdPoint ? holdPoint.position : transform.position;

        lastThrower = holder;
        transform.SetParent(null);
        
        transform.position = throwOrigin;
        
        isHeld = false;
        rb.isKinematic = false;
        col.isTrigger = false;
        rb.mass *= flightMassMultiplier;

        float speed = useNormalThrow ? underarmThrowSpeed : lobThrowSpeed;
        float upward = useNormalThrow ? underarmThrowUpward : lobThrowUpward;
        Vector3 force = direction.normalized * speed + Vector3.up * upward;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        lastThrowTime = Time.time;
        holder = null;
        
        Debug.Log($"ThrowBomb: SUCCESS - Bomb thrown from {throwOrigin} with force={force}, speed={speed}, upward={upward}, finalMass={rb.mass}");

        StartCoroutine(ReturnToThrowerAfterDelay());
    }

    [Server]
    public bool TryThrowBomb(Vector3 direction, bool useNormalThrow)
    {
        Debug.Log($"TryThrowBomb: Called with direction={direction}, useNormalThrow={useNormalThrow}, isHeld={isHeld}, holder={(holder != null ? holder.name : "null")}, Time.time={Time.time}, lastThrowTime={lastThrowTime}, throwCooldown={throwCooldown}, cooldownCheck={Time.time < lastThrowTime + throwCooldown}");
        
        if (!isHeld || holder == null || Time.time < lastThrowTime + throwCooldown)
        {
            Debug.LogWarning($"TryThrowBomb: REJECTED - isHeld={isHeld}, holder={(holder != null ? holder.name : "null")}, cooldownRemaining={Mathf.Max(0, (lastThrowTime + throwCooldown) - Time.time)}");
            return false;
        }

        Transform holdPoint = holder.transform.Find("RightHoldPoint");
        Vector3 throwOrigin = holdPoint ? holdPoint.position : transform.position;

        lastThrower = holder;
        transform.SetParent(null);
        
        transform.position = throwOrigin;
        
        isHeld = false;
        rb.isKinematic = false;
        col.isTrigger = false;
        rb.mass *= flightMassMultiplier;

        float speed = useNormalThrow ? underarmThrowSpeed : lobThrowSpeed;
        float upward = useNormalThrow ? underarmThrowUpward : lobThrowUpward;
        Vector3 force = direction.normalized * speed + Vector3.up * upward;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        lastThrowTime = Time.time;
        holder = null;
        
        Debug.Log($"TryThrowBomb: SUCCESS - Bomb thrown from {throwOrigin} with force={force}, speed={speed}, upward={upward}, finalMass={rb.mass}");

        StartCoroutine(ReturnToThrowerAfterDelay());
        return true;
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