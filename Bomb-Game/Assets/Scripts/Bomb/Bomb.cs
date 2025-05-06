using System;
using UnityEngine;
using TMPro;

public class Bomb : MonoBehaviour
{
    // --- GLOBAL EXPLOSION EVENT ---
    public static event Action OnBombExplodedGlobal;

    // --- REFERENCES & COMPONENT CACHING ---
    private Rigidbody rb;
    private Collider bombCollider;
    private BombEffects bombEffects;
    private TextMeshProUGUI timerText;
    private Transform canvasTransform;

    // --- HOLDER STATE ---
    private GameObject holder;
    private bool isOnRight = true;
    private bool isHeld = true;
    private float lastThrowTime;
    private GameObject lastThrower;

    // --- TIMER STATE ---
    [Header("Timer Settings")]
    [SerializeField] private float initialTimer        = 10f;
    [SerializeField] private float returnPauseDuration = 0.5f;
    private float currentTimer;
    private bool returnPause;
    private float returnPauseStart;

    // --- THROW SETTINGS ---
    [Header("Throw Settings")]
    [SerializeField] private float normalThrowSpeed    = 20f;
    [SerializeField] private float normalThrowUpward   =  2f;
    [SerializeField] private float lobThrowSpeed       = 10f;
    [SerializeField] private float lobThrowUpward      =  5f;
    [SerializeField] private float throwCooldown       = 0.5f;
    [SerializeField] private float flightMassMultiplier = 1f;

    // --- COLLISION & BOUNCE SETTINGS ---
    [Header("Collision Settings")]
    [SerializeField] private int maxBounces             = 1;
    [SerializeField] private float groundExplosionDelay = 1f;
    [SerializeField] private LayerMask mapLayerMask;
    [SerializeField] private string playerTag           = "Player";
    [SerializeField] private string mapOutTag           = "MapOut";

    private int currentBounces;
    private float groundHitTime;
    private bool waitingToExplode;

    // --- PUBLIC READONLY ACCESSORS ---
    public bool IsOnRight           => isOnRight;
    public bool IsHeld              => isHeld;
    public float CurrentTimer       => currentTimer;
     public GameObject Holder { get; private set; }
    public float NormalThrowSpeed   => normalThrowSpeed;
    public float NormalThrowUpward  => normalThrowUpward;
    public float LobThrowSpeed      => lobThrowSpeed;
    public float LobThrowUpward     => lobThrowUpward;

    void Awake()
    {
        rb           = GetComponent<Rigidbody>();
        bombCollider = GetComponent<Collider>();
        bombEffects  = TryGetComponent(out BombEffects be) ? be : null;
        timerText    = GetComponentInChildren<TextMeshProUGUI>();

        if (rb == null || bombCollider == null)
            Debug.LogError("Bomb requires Rigidbody & Collider", this);
        if (timerText == null)
            Debug.LogWarning("Missing TextMeshProUGUI under Bomb", this);

        currentTimer = initialTimer;
        if (timerText != null)
            canvasTransform = timerText.transform.parent;
    }

    void Update()     => UpdateTimer();
    void LateUpdate() => FaceCamera();

    private void UpdateTimer()
    {
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
            else
                return;
        }

        currentTimer -= Time.deltaTime;
        if (timerText != null)
            timerText.text = Mathf.Ceil(currentTimer).ToString();

        if (currentTimer <= 0f)
            Explode();
    }

    private void FaceCamera()
    {
        if (canvasTransform != null && Camera.main != null)
        {
            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - Camera.main.transform.position
            );
        }
    }

    public void AssignToPlayer(GameObject player)
{
    holder = player;
    isHeld = true;
    bombCollider.enabled = false;
    waitingToExplode = false;
    currentBounces = 0;
    rb.mass = 1f;
    // Removed: currentTimer = initialTimer;
    returnPause = false;

    UpdateHoldTransform();
    if (holder.TryGetComponent<PlayerBombHandler>(out var handler))
        handler.SetBomb(this);
}

    public void ClearHolder()
    {
        if (holder != null && holder.TryGetComponent<PlayerBombHandler>(out var handler))
            handler.ClearBomb();
        holder = null;
    }

    private void UpdateHoldTransform()
    {
        if (!isHeld || holder == null) return;

        var pointName = isOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        var holdPoint = holder.transform.Find(pointName);
        if (holdPoint == null) return;

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        rb.isKinematic          = true;
    }

    public void SwapHoldPoint()
    {
        if (!isHeld) return;
        isOnRight = !isOnRight;
        UpdateHoldTransform();
    }

    public void ThrowBomb()
    {
        if (holder == null || Time.time < lastThrowTime + throwCooldown)
            return;

        lastThrower          = holder;
        transform.SetParent(null);
        rb.isKinematic       = false;
        bombCollider.enabled = true;
        isHeld               = false;
        rb.mass             *= flightMassMultiplier;

        Vector3 forward = holder.transform.forward;
        Vector3 force   = isOnRight
            ? forward * normalThrowSpeed + Vector3.up * normalThrowUpward
            : forward * lobThrowSpeed    + Vector3.up * lobThrowUpward;

        rb.AddForce(force, ForceMode.Impulse);
        lastThrowTime   = Time.time;
        currentBounces  = 0;
        ClearHolder();
    }

    void OnCollisionEnter(Collision col)
    {
        if (isHeld) return;

        // Return to last thrower if touching MapOut tag or map layers
        if (col.gameObject.CompareTag(mapOutTag) ||
            (((1 << col.gameObject.layer) & mapLayerMask) != 0))
        {
            if (lastThrower != null)
            {
                returnPause      = true;
                returnPauseStart = Time.time;
                AssignToPlayer(lastThrower);
                return;
            }
        }

        // Assign to player on contact
        if (col.gameObject.CompareTag(playerTag))
        {
            AssignToPlayer(col.gameObject);
            return;
        }

        // Bounce logic
        currentBounces++;
        if (currentBounces >= maxBounces)
        {
            waitingToExplode = true;
            groundHitTime    = Time.time;
        }
    }

     public void DetachFromPlayer()
    {
        if (Holder != null)
        {
            Holder.GetComponent<PlayerBombHandler>()?.ClearBomb();
            Holder = null;
        }
        transform.SetParent(null);
    }

    public void TriggerImmediateExplosion()
    {
        StopAllCoroutines();
        Explode();
    }

    // Modified existing Explode method
    private void Explode()
    {
        // Existing explosion logic
        bombEffects?.PlayExplosionEffects();
        OnBombExplodedGlobal?.Invoke();
        
        // Security cleanup
        if (Holder != null)
        {
            Holder.GetComponent<PlayerBombHandler>()?.ClearBomb();
        }
        Destroy(gameObject);
    }
}
