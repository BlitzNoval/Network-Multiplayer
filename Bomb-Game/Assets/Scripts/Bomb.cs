using UnityEngine;
using TMPro;

public class Bomb : MonoBehaviour
{
    private Rigidbody rb;
    private Collider bombCollider;

    [Header("Holder")]
    public GameObject holder;
    private bool isOnRight = true;
    private bool isHeld = true;
    private float lastThrowTime;
    private GameObject lastThrower;

    [Header("Timer")]
    [Tooltip("How long the bomb lives (seconds)")]
    public float initialTimer = 10f;
    private float currentTimer;
    public TextMeshProUGUI timerText;

    [Tooltip("Pause duration (seconds) when returning to a player")]
    public float returnPauseDuration = 0.5f;
    private bool returnPause;
    private float returnPauseStart;

    [Header("Throw Settings")]
    public float normalThrowSpeed = 20f;
    public float normalThrowUpward = 2f;
    public float lobThrowSpeed = 10f;
    public float lobThrowUpward = 5f;
    public int maxBounces = 1;
    public float throwCooldown = 0.5f;
    public float groundExplosionDelay = 1f;
    public float flightMassMultiplier = 1f;

    // bounce/explode state
    private int currentBounces;
    private float groundHitTime;
    private bool waitingToExplode;

    // for world‑space canvas facing the camera
    private Transform canvasTransform;

    // Public getter
    public bool IsOnRight => isOnRight;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bombCollider = GetComponent<Collider>();
        timerText = GetComponentInChildren<TextMeshProUGUI>();

        if (rb == null || bombCollider == null)
            Debug.LogError("Rigidbody or Collider missing on Bomb!");
        if (timerText == null)
            Debug.LogWarning("TextMeshProUGUI component not found on Bomb child!");

        // start timer immediately on spawn
        currentTimer = initialTimer;

        // cache the Canvas (parent of the text)
        if (timerText != null)
            canvasTransform = timerText.transform.parent;
    }

    void Update()
    {
        // --- 1) COUNTDOWN (always) with optional return‑pause ---
        if (currentTimer > 0f)
        {
            if (returnPause)
            {
                // check if the pause window has expired
                if (Time.time >= returnPauseStart + returnPauseDuration)
                    returnPause = false;
            }
            else
            {
                currentTimer -= Time.deltaTime;
                if (timerText != null)
                    timerText.text = Mathf.Ceil(currentTimer).ToString();
                if (currentTimer <= 0f)
                {
                    Explode();
                    return;
                }
            }
        }

        // --- 2) GROUND‐EXPLOSION DELAY (unchanged) ---
        if (waitingToExplode && Time.time >= groundHitTime + groundExplosionDelay)
            Explode();
    }

    void LateUpdate()
    {
        // --- 3) FACE MAIN CAMERA ---
        if (canvasTransform != null && Camera.main != null)
        {
            // rotate so the canvas normal looks back at the camera
            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - Camera.main.transform.position
            );
        }
    }

    public void AssignToPlayer(GameObject player)
    {
        holder = player;
        if (holder != null)
        {
            isHeld = true;
            bombCollider.enabled = false;
            waitingToExplode = false;
            currentBounces = 0;
            rb.mass = 1f;

            UpdateHoldPoint();

            var handler = holder.GetComponent<PlayerBombHandler>();
            if (handler != null)
                handler.SetBomb(this);
        }
    }

    public void ClearHolder()
    {
        if (holder != null)
        {
            var handler = holder.GetComponent<PlayerBombHandler>();
            if (handler != null)
                handler.ClearBomb();
            holder = null;
        }
    }

    void UpdateHoldPoint()
    {
        if (holder == null || !isHeld) return;

        string holdPointName = isOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        Transform holdPoint = holder.transform.Find(holdPointName);
        if (holdPoint != null)
        {
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    public void SwapHoldPoint()
    {
        if (holder != null && isHeld)
        {
            isOnRight = !isOnRight;
            UpdateHoldPoint();
        }
    }

    public void ThrowBomb()
    {
        if (holder == null || Time.time < lastThrowTime + throwCooldown)
            return;

        lastThrower = holder;
        transform.SetParent(null);
        rb.isKinematic = false;
        bombCollider.enabled = true;
        isHeld = false;
        rb.mass *= flightMassMultiplier;

        Vector3 forward = holder.transform.forward;
        Vector3 throwForce = isOnRight
            ? (forward * normalThrowSpeed) + Vector3.up * normalThrowUpward
            : (forward * lobThrowSpeed)   + Vector3.up * lobThrowUpward;

        rb.AddForce(throwForce, ForceMode.Impulse);
        lastThrowTime = Time.time;
        currentBounces = 0;
        ClearHolder();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isHeld) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            AssignToPlayer(collision.gameObject);
        }
        else if (collision.gameObject.CompareTag("Map"))
        {
            if (lastThrower != null)
            {
                // pause countdown briefly, then give it back
                returnPause = true;
                returnPauseStart = Time.time;
                AssignToPlayer(lastThrower);
            }
            else
            {
                currentBounces++;
                if (currentBounces >= maxBounces)
                {
                    waitingToExplode = true;
                    groundHitTime = Time.time;
                }
            }
        }
    }

    void Explode()
{
    Debug.Log("Bomb exploded!");

    // Find all players
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    if (players.Length > 0)
    {
        // Select a random player
        GameObject randomPlayer = players[Random.Range(0, players.Length)];

        // Spawn a new bomb instance using the GameManager's bombPrefab
        if (GameManager.Instance != null && GameManager.Instance.bombPrefab != null)
        {
            GameObject newBomb = Instantiate(GameManager.Instance.bombPrefab, Vector3.zero, Quaternion.identity);
            Bomb bombScript = newBomb.GetComponent<Bomb>();
            if (bombScript != null)
            {
                bombScript.AssignToPlayer(randomPlayer);
                GameManager.Instance.bombInstance = newBomb; // Update the GameManager's bombInstance
            }
            else
            {
                Debug.LogError("Bomb script missing on instantiated bomb!");
            }
        }
    }
    else
    {
        Debug.LogError("No players found to assign new bomb!");
    }

    Destroy(gameObject);
}
}
