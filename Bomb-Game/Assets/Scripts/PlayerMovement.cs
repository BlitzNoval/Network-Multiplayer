using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    public float speed = 5f;           // Max movement speed
    public float acceleration = 10f;   // How fast it speeds up (for standard mode)
    public float deceleration = 10f;   // How fast it slows down (for standard mode)
    public float rotationSpeed = 10f;  // How fast it rotates to face direction
    public bool continuousMovement = false; // Toggle for continuous movement mode

    private Vector2 moveInput;         // Stores input from keyboard/controller
    private Vector3 horizontalVelocity; // Current movement velocity
    private Vector3 movementDirection = Vector3.zero; // Current movement direction for continuous mode

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing on " + gameObject.name);
        }
    }

    // Called by PlayerInput when "Move" action is triggered
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void FixedUpdate()
    {
        if (continuousMovement)
        {
            // Continuous movement: always move at full speed in the current direction
            if (moveInput.magnitude > 0.1f)
            {
                // Update direction only when significant input is given
                movementDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            }
            // Set velocity directly to full speed in the current direction
            horizontalVelocity = movementDirection * speed;
        }
        else
        {
            // Standard movement: accelerate towards input direction, decelerate when no input
            Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y).normalized * speed;
            if (moveInput.magnitude > 0.1f)
            {
                // Accelerate towards target velocity
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                // Decelerate to stop
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            }
        }

        // Apply velocity, preserving vertical velocity (e.g., for gravity)
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);

        // Rotate to face movement direction
        if (horizontalVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}