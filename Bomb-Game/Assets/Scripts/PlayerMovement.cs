using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    public float speed = 5f;           // Max movement speed
    public float acceleration = 10f;   // How fast it speeds up
    public float deceleration = 10f;   // How fast it slows down
    public float rotationSpeed = 10f;  // How fast it rotates to face direction
    private Vector2 moveInput;         // Stores input from keyboard/controller
    private Vector3 horizontalVelocity;// Current movement velocity

    // Public getters for state syncing
    public Vector2 MoveInput => moveInput;
    public Vector3 HorizontalVelocity => horizontalVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing on " + gameObject.name);
        }
    }

    // Called by PlayerInput when "Move" action is triggered (intended for local player input)
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // Apply movement based on input (intended for server-side processing when networked)
    public void ApplyMovement()
    {
        // Normalize input if magnitude > 1 (e.g., diagonal movement)
        if (moveInput.magnitude > 1f)
        {
            moveInput.Normalize();
        }

        // Calculate desired velocity based on input
        Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y) * speed;

        // Smoothly accelerate or decelerate
        if (targetVelocity.magnitude > 0)
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        // Apply velocity, keeping vertical velocity (e.g., for gravity)
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);

        // Rotate to face movement direction
        if (horizontalVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }
}