using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    private InputAction moveAction;
    private Vector2 moveInput;
    private Vector3 horizontalVelocity;

    [Header("Movement Settings")]
    public float speed = 5f;
    public float acceleration = 10f;
    public float deceleration = 10f;
    public float rotationSpeed = 10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        var input = GetComponent<PlayerInput>();
        moveAction = input.actions.FindAction("Move");
    }

    void OnEnable()
    {
        moveAction.performed += HandleMove;
        moveAction.canceled += HandleStop; // New event handler
        moveAction.Enable();
    }

    void OnDisable()
    {
        moveAction.performed -= HandleMove;
        moveAction.canceled -= HandleStop; // New event unsubscription
        moveAction.Disable();
    }

    private void HandleMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void HandleStop(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero; // Reset input when released
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    private void ApplyMovement()
    {
        if (moveInput.magnitude > 1f)
            moveInput.Normalize();

        Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y) * speed;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            (targetVelocity.magnitude > 0.01f ? acceleration : deceleration) * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            rb.linearVelocity.y,
            horizontalVelocity.z
        );

        if (horizontalVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );
        }
    }
}