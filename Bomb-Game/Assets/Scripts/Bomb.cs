using UnityEngine;

public class Bomb : MonoBehaviour
{
    private Rigidbody rb;
    private Collider bombCollider;
    public GameObject holder;
    private bool isOnRight = true;
    private bool isHeld = true;
    private float lastThrowTime;
    private int currentBounces;
    private float groundHitTime;
    private bool waitingToExplode;

    // Public getter for isOnRight
    public bool IsOnRight => isOnRight;

    [Header("Normal Throw (Right Hand)")]
    [Tooltip("Forward speed of the normal throw (units/second)")]
    public float normalThrowSpeed = 20f;
    [Tooltip("Upward force for the normal throw (units/second)")]
    public float normalThrowUpward = 2f;

    [Header("Lob Throw (Left Hand)")]
    [Tooltip("Forward speed of the lob throw (units/second)")]
    public float lobThrowSpeed = 10f;
    [Tooltip("Upward force for the lob arc (units/second)")]
    public float lobThrowUpward = 5f;

    [Header("General Throw Settings")]
    [Tooltip("Number of bounces before stopping (0 = no bounces)")]
    public int maxBounces = 1;
    [Tooltip("Cooldown between throws (seconds)")]
    public float throwCooldown = 0.5f;
    [Tooltip("Delay before exploding on ground (seconds)")]
    public float groundExplosionDelay = 1f;
    [Tooltip("Rigidbody mass multiplier during flight")]
    public float flightMassMultiplier = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bombCollider = GetComponent<Collider>();
        if (rb == null || bombCollider == null)
        {
            Debug.LogError("Rigidbody or Collider missing on Bomb!");
        }
    }

    void Update()
    {
        if (waitingToExplode && Time.time >= groundHitTime + groundExplosionDelay)
        {
            Explode();
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
            PlayerBombHandler handler = holder.GetComponent<PlayerBombHandler>();
            if (handler != null)
            {
                handler.SetBomb(this);
            }
        }
    }

    public void ClearHolder()
    {
        if (holder != null)
        {
            PlayerBombHandler handler = holder.GetComponent<PlayerBombHandler>();
            if (handler != null)
            {
                handler.ClearBomb();
            }
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
        if (holder == null || Time.time < lastThrowTime + throwCooldown) return;

        transform.SetParent(null);
        rb.isKinematic = false;
        bombCollider.enabled = true;
        isHeld = false;
        rb.mass *= flightMassMultiplier;

        Vector3 forward = holder.transform.forward;
        Vector3 throwForce;

        if (isOnRight) // Normal throw
        {
            throwForce = (forward * normalThrowSpeed) + (Vector3.up * normalThrowUpward);
        }
        else // Lob throw
        {
            throwForce = (forward * lobThrowSpeed) + (Vector3.up * lobThrowUpward);
        }

        rb.AddForce(throwForce, ForceMode.Impulse);
        lastThrowTime = Time.time;
        currentBounces = 0;
        ClearHolder();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isHeld)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                AssignToPlayer(collision.gameObject);
            }
            else if (collision.gameObject.CompareTag("Map"))
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
        Destroy(gameObject);
    }
}