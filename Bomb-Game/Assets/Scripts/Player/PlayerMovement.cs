using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SyncVar] public float speed = 5f;
    [SyncVar] public float acceleration = 10f;
    [SyncVar] public float deceleration = 10f;
    [SyncVar] public float rotationSpeed = 10f;
    
    [Header("Knockback")]
    private bool isInKnockback = false;
    private float knockbackMovementMultiplier = 1f;

    Rigidbody rb;
    InputAction moveAct;
    InputAction aimAct;
    Vector2 moveInput;
    Vector3 horizVel;
    private PlayerInput playerInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        moveAct = playerInput.actions.FindAction("Move");
        aimAct = playerInput.actions.FindAction("Aim");
    }

    void OnEnable() { if (isLocalPlayer) { moveAct.Enable(); aimAct.Enable(); } }
    void OnDisable() { if (isLocalPlayer) { moveAct.Disable(); aimAct.Disable(); } }

    void Update()
    {
        if (!isLocalPlayer || !GameManager.Instance || !GameManager.Instance.GameActive) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameManager.Instance.IsPaused)
            {
                if (GameManager.Instance.Pauser == netIdentity)
                {
                    CmdResumeGame();
                }
            }
            else
            {
                CmdPauseGame();
            }
        }

        moveInput = moveAct.ReadValue<Vector2>();

        // Handle rotation based on aiming input
        if (playerInput.currentControlScheme == "KeyboardMouse")
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            // Project the ray onto a plane at player height, not y=0
            Plane ground = new Plane(Vector3.up, transform.position);
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            if (ground.Raycast(ray, out float d))
            {
                Vector3 hit = ray.GetPoint(d);
                // Zero-out the vertical component before normalizing to ensure rotation around Y-axis only
                Vector3 direction = hit - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion look = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
                }
            }
        }
        else if (playerInput.currentControlScheme == "Gamepad")
        {
            Vector2 aimInput = aimAct.ReadValue<Vector2>();
            if (aimInput.sqrMagnitude > 0.01f)
            {
                Vector3 direction = new Vector3(aimInput.x, 0, aimInput.y).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer || !GameManager.Instance || !GameManager.Instance.GameActive) return;

        if (GameManager.Instance.IsPaused)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        ApplyMovement();
    }

    void ApplyMovement()
    {
        if (moveInput.magnitude > 1f) moveInput.Normalize();

        Vector3 cameraForward = Camera.main.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 cameraRight = Camera.main.transform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        Vector3 target = (cameraRight * moveInput.x + cameraForward * moveInput.y) * speed * knockbackMovementMultiplier;

        float currentAcceleration = (target.magnitude > 0.01f ? acceleration : deceleration) * Time.fixedDeltaTime;
        
        // Reduce acceleration/deceleration during knockback for more natural feel
        if (isInKnockback)
        {
            currentAcceleration *= 0.5f; // Slower movement changes during knockback
        }

        horizVel = Vector3.MoveTowards(horizVel, target, currentAcceleration);

        rb.linearVelocity = new Vector3(horizVel.x, rb.linearVelocity.y, horizVel.z);
    }

    [Command]
    public void CmdPauseGame() => GameManager.Instance.PauseGame(netIdentity);

    [Command]
    public void CmdResumeGame() => GameManager.Instance.ResumeGame(netIdentity);

    public void SetKnockbackState(bool inKnockback, float movementMultiplier)
    {
        isInKnockback = inKnockback;
        knockbackMovementMultiplier = movementMultiplier;
    }

    public float GetCurrentMovementMultiplier()
    {
        return knockbackMovementMultiplier;
    }
}