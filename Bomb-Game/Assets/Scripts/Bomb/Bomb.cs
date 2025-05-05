using System;
using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bomb : NetworkBehaviour
{
    public static event Action OnBombExplodedGlobal; 
    //  global event so GameManager (and anyone else) can know when the bomb finally pops

    // ───────────── Inspector fields ─────────────
    [Header("Timer")]
    [SerializeField] float initialTimer        = 10f; // starting countdown in seconds
    [SerializeField] float returnPauseDuration = 2f;  // pause before giving bomb back after falling off

    [Header("Throw")]
    [SerializeField] float normalThrowSpeed      = 20f; // speed when you toss it from right hand
    [SerializeField] float normalThrowUpward     =  2f; // slight lift on a normal throw
    [SerializeField] float lobThrowSpeed         = 10f; // slower, higher arc when throwing from left hand
    [SerializeField] float lobThrowUpward        =  5f; // more upward on lob
    [SerializeField] float throwCooldown         =  0.5f; // seconds between throws
    [SerializeField] float flightMassMultiplier  =  1f;   // optional mass tweak mid-flight

    [Header("Collision / Explosion")]
    [SerializeField] int   maxBounces            = 1;   // how many times we bounce before exploding
    [SerializeField] float groundExplosionDelay  = 1f;  // delay once we hit ground
    [SerializeField] float explosionRadius       = 5f;  // how far our knockback reaches
    [SerializeField] float baseKnockForce        = 5f;  // baseline knockback strength
    [SerializeField] LayerMask mapLayerMask;           // which layers count as map-out
    [SerializeField] string playerTag = "Player";      // tag to detect players
    [SerializeField] string mapOutTag = "MapOut";      // tag for area that kicks bomb back

    // ───────────── Cached references ───────────
    Rigidbody       rb;         // physics body
    Collider        col;        // collider for bouncing and passing
    BombEffects     fx;         // VFX & SFX handler
    TextMeshProUGUI timerText;  // UI showing countdown
    Transform       canvasTr;   // parent transform for timer so it faces camera

    // ───────────── Network-synced state ───────
    [SyncVar(hook = nameof(OnHolderChanged))] GameObject holder;  
    //  who currently has the bomb (null if flying/free)
    [SyncVar] bool  isOnRight   = true;  
    [SyncVar] bool  isHeld      = true;  
    [SyncVar] float currentTimer;  // sync countdown so everyone sees same number

    // ───────────── Server-only fields ───────────
    float       lastThrowTime, groundHitTime;
    bool        waitingToExplode, returnPause;
    float       returnPauseStart;
    int         currentBounces;
    GameObject  lastThrower;  // track who threw it for return logic

    // ───────────── Public getters ─────────────
    public GameObject Holder            => holder;
    public bool       IsOnRight         => isOnRight;
    public bool       IsHeld            => isHeld;
    public float      NormalThrowSpeed  => normalThrowSpeed;
    public float      NormalThrowUpward => normalThrowUpward;
    public float      LobThrowSpeed     => lobThrowSpeed;
    public float      LobThrowUpward    => lobThrowUpward;

    // ───────────── Unity callbacks ─────────────
    void Awake()
    {
        // grab our components once to avoid repeated GetComponent calls
        rb        = GetComponent<Rigidbody>();
        col       = GetComponent<Collider>();
        fx        = GetComponent<BombEffects>();
        timerText = GetComponentInChildren<TextMeshProUGUI>();
        if (timerText) 
            canvasTr = timerText.transform.parent;  // we rotate this towards camera each frame
        currentTimer = initialTimer;  // initialize our countdown
    }

    void Update()
    {
        if (isServer) TickTimer(); // only the server decrements the timer
    }

    void LateUpdate()
    {
        // rotate the timer UI toward the main camera so it's always readable
        if (canvasTr && Camera.main)
            canvasTr.rotation = Quaternion.LookRotation(canvasTr.position - Camera.main.transform.position);
    }

    // ───────────── Countdown and explosion ─────────────
    [Server]
    void TickTimer()
    {
        if (waitingToExplode)
        {
            // if we've landed, wait a bit and then blow
            if (Time.time >= groundHitTime + groundExplosionDelay)
                Explode();
            return;
        }

        if (returnPause)
        {
            // pause countdown briefly if it hit map-out
            if (Time.time >= returnPauseStart + returnPauseDuration)
                returnPause = false;
            return;
        }

        // subtract time and tell clients
        currentTimer -= Time.deltaTime;
        RpcUpdateTimer(Mathf.CeilToInt(currentTimer));

        if (currentTimer <= 0f && !waitingToExplode)
        {
            currentTimer = 0f;
            Explode();  // boom!
        }
    }

    [ClientRpc]
    void RpcUpdateTimer(int seconds)
    {
        if (timerText)
            timerText.text = seconds.ToString();  // update the displayed number
    }

    [Server]
    void Explode()
    {
        // guard against double-calls
        if (waitingToExplode) return;
        waitingToExplode = true;

        // knockback logic: find nearby players
        foreach (var hit in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            if (!hit.CompareTag(playerTag)) continue;
            if (!hit.TryGetComponent(out Rigidbody prb)) continue;
            if (!hit.TryGetComponent(out PlayerLifeManager life)) continue;

            // make most of force horizontal, with a bit of lift
            Vector3 radial           = hit.transform.position - transform.position;
            Vector3 horizDir         = new Vector3(radial.x, 0, radial.z).normalized;
            const float sidePct      = 0.85f;  // mostly sideways
            const float upwardPct    = 0.15f;  // small bump upward
            Vector3 dir              = (horizDir * sidePct + Vector3.up * upwardPct).normalized;

            // scale by player-specific multiplier, boost if it was in their hands
            float forceMag = baseKnockForce * Mathf.Pow(life.knockbackMultiplier, 1.25f);
            if (hit.gameObject == holder) forceMag *= 1.5f;

            // small server-side push so spectators see movement
            prb.AddForce(dir * forceMag * 0.2f, ForceMode.Impulse);

            // full-force on the client owning that player for responsiveness
            if (hit.TryGetComponent(out NetworkIdentity ni) && ni.connectionToClient != null)
                life.TargetApplyKnockback(ni.connectionToClient, dir * forceMag);

            life.RegisterKnockbackHit();
        }

        // let go of bomb if someone had it
        holder?.GetComponent<PlayerBombHandler>()?.ClearBomb();
        holder = null;

        // play VFX / SFX everywhere
        RpcPlayExplosion();
        OnBombExplodedGlobal?.Invoke();

        // clean up after a second
        StartCoroutine(DestroyAfterDelay(1f));
    }

    [Server]
    IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcPlayExplosion()
    {
        if (fx != null && !fx.IsPlayingEffects)
            fx.PlayExplosionEffects(); // cue explosion visuals & sound
    }

    // ───────────── Holder assignment ─────────────
    [Server]
    public void AssignToPlayer(GameObject p)
    {
        // server tells us who holds the bomb now
        holder         = p;
        isHeld         = true;
        col.enabled    = false;
        currentBounces = 0;
        rb.isKinematic = true;
        rb.mass        = 1f;
        returnPause    = false;
        currentTimer   = Mathf.Max(currentTimer, 3f); // give them some breathing room
    }

    void OnHolderChanged(GameObject oldH, GameObject newH)
    {
        // clear old player's reference
        oldH?.GetComponent<PlayerBombHandler>()?.ClearBomb();

        if (newH)
        {
            // hook it into the new player's hand mesh
            newH.GetComponent<PlayerBombHandler>()?.SetBomb(this);
            Transform grip = newH.transform.Find(isOnRight ? "RightHoldPoint" : "LeftHoldPoint");
            if (grip)
            {
                transform.SetParent(grip);
                transform.localPosition = Vector3.zero;
            }
            rb.isKinematic = true;
            col.enabled    = false;
            lastThrower    = newH;
        }
        else
        {
            // free-flying bomb
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled    = true;
        }
    }

    [Server]
    public void SwapHoldPoint()
    {
        // flip from right-hand to left-hand hold
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
        // server-run throw check
        if (!isHeld || holder == null || Time.time < lastThrowTime + throwCooldown)
            return;

        lastThrower = holder;
        transform.SetParent(null);
        isHeld         = false;
        rb.isKinematic = false;
        col.enabled    = true;
        rb.mass       *= flightMassMultiplier;

        Transform origin = holder.transform.Find(isOnRight ? "RightHoldPoint" : "LeftHoldPoint");
        Vector3 forward  = origin ? origin.forward : holder.transform.forward;
        Vector3 force    = isOnRight
            ? forward * normalThrowSpeed + Vector3.up * normalThrowUpward
            : forward * lobThrowSpeed   + Vector3.up * lobThrowUpward;

        rb.velocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);

        currentBounces  = 0;
        lastThrowTime   = Time.time;
        holder          = null;
    }

    [ServerCallback]
    void OnCollisionEnter(Collision c)
    {
        // if we hit a player, hand it off
        if (c.gameObject.CompareTag(playerTag))
        {
            if (c.gameObject != holder)
                AssignToPlayer(c.gameObject);
            return;
        }

        if (isHeld) return; // ignore while in hand

        // if we leave map bounds, return to thrower
        if (c.gameObject.CompareTag(mapOutTag) ||
            ((1 << c.gameObject.layer & mapLayerMask) != 0))
        {
            if (lastThrower != null)
            {
                returnPause      = true;
                returnPauseStart = Time.time;
                AssignToPlayer(lastThrower);
            }
            return;
        }

        // count our bounces
        currentBounces++;
        if (currentBounces >= maxBounces)
        {
            waitingToExplode = true;
            groundHitTime    = Time.time;
        }
    }

    [Server]
    public void TriggerImmediateExplosion() => Explode();
}
