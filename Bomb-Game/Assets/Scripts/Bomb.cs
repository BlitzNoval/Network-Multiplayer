using UnityEngine;

public class Bomb : MonoBehaviour
{
    private Rigidbody rb;
    private Collider bombCollider;
    public GameObject holder; // The player holding the bomb
    private bool isOnRight = true; // True = right hand, false = left hand
    private bool isHeld = true; // Is the bomb being held?
    private float lastThrowTime; // Tracks last throw for cooldown

    // Curve Throw (Right Hand) Parameters
    [Header("Curve Throw (Right Hand)")]
    [Tooltip("Forward speed of the curve throw (units/second)")]
    public float curveSpeed = 15f;
    [Tooltip("Sideways force for the curve arc (units/second)")]
    public float curveSideForce = 5f;
    [Tooltip("Angle offset for the throw direction (degrees, 0 = straight)")]
    public float curveAngleOffset = 0f;

    // Lob Throw (Left Hand) Parameters
    [Header("Lob Throw (Left Hand)")]
    [Tooltip("Forward speed of the lob throw (units/second)")]
    public float lobSpeed = 10f;
    [Tooltip("Upward force for the lob arc (units/second)")]
    public float lobUpwardForce = 5f;
    [Tooltip("Angle offset for the throw direction (degrees, 0 = straight)")]
    public float lobAngleOffset = 10f;

    // General Throw Parameters
    [Header("General Throw Settings")]
    [Tooltip("Number of bounces before exploding (0 = explode on first hit)")]
    public int maxBounces = 1;
    [Tooltip("Cooldown between throws (seconds)")]
    public float throwCooldown = 0.5f;
    [Tooltip("Delay before exploding on ground (seconds)")]
    public float groundExplosionDelay = 1f;
    [Tooltip("Rigidbody mass multiplier during flight (1 = default)")]
    public float flightMassMultiplier = 1f;

    private int currentBounces; // Tracks bounces
    private float groundHitTime; // Time when bomb hits ground
    private bool waitingToExplode; // Is bomb waiting to explode?

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
            rb.mass = 1f; // Reset mass
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

        if (isOnRight) // Curve throw
        {
            Vector3 right = holder.transform.right;
            forward = Quaternion.Euler(0, curveAngleOffset, 0) * forward;
            throwForce = (forward * curveSpeed) + (right * curveSideForce);
        }
        else // Lob throw
        {
            forward = Quaternion.Euler(0, lobAngleOffset, 0) * forward;
            throwForce = (forward * lobSpeed) + (Vector3.up * lobUpwardForce);
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
        Destroy(gameObject); // Temporary
    }
}