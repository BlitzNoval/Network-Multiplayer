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

    /* NEW: block movement while an emote plays */
    bool isEmoting;

    Rigidbody rb;
    InputAction moveAct;
    InputAction aimAct;
    Vector2 moveInput;
    Vector3 horizVel;
    PlayerInput playerInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        moveAct = playerInput.actions.FindAction("Move");
        aimAct  = playerInput.actions.FindAction("Aim");
    }

    void OnEnable()
    {
        if (isLocalPlayer)
        {
            moveAct.Enable();
            aimAct.Enable();
        }
    }
    void OnDisable()
    {
        if (isLocalPlayer)
        {
            moveAct.Disable();
            aimAct.Disable();
        }
    }

    void Update()
    {
        if (!isLocalPlayer || !GameManager.Instance || !GameManager.Instance.GameActive)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameManager.Instance.IsPaused)
            {
                if (GameManager.Instance.Pauser == netIdentity)
                    CmdResumeGame();
            }
            else
            {
                CmdPauseGame();
            }
        }

        // suppress move input while emoting
        moveInput = isEmoting ? Vector2.zero : moveAct.ReadValue<Vector2>();

        HandleRotation();
    }

    void HandleRotation()
    {
        if (playerInput.currentControlScheme == "KeyboardMouse")
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Plane ground = new Plane(Vector3.up, transform.position);
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            if (ground.Raycast(ray, out float d))
            {
                Vector3 hit = ray.GetPoint(d);
                Vector3 dir = hit - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion look = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
                }
            }
        }
        else if (playerInput.currentControlScheme == "Gamepad")
        {
            Vector2 aimInput = aimAct.ReadValue<Vector2>();
            if (aimInput.sqrMagnitude > 0.01f)
            {
                Vector3 dir = new Vector3(aimInput.x, 0, aimInput.y).normalized;
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
            }
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer || !GameManager.Instance || !GameManager.Instance.GameActive)
            return;

        if (GameManager.Instance.IsPaused)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (isEmoting)
        {
            // lock horizontal motion but preserve vertical (e.g., gravity)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            horizVel = Vector3.zero;
            return;
        }

        ApplyMovement();
    }

    void ApplyMovement()
    {
        if (moveInput.magnitude > 1f) moveInput.Normalize();

        Vector3 camF = Camera.main.transform.forward; camF.y = 0; camF.Normalize();
        Vector3 camR = Camera.main.transform.right;   camR.y = 0; camR.Normalize();

        Vector3 target = (camR * moveInput.x + camF * moveInput.y) * speed * knockbackMovementMultiplier;

        float accel = (target.magnitude > 0.01f ? acceleration : deceleration)
                      * Time.fixedDeltaTime * (isInKnockback ? 0.5f : 1f);

        horizVel = Vector3.MoveTowards(horizVel, target, accel);
        rb.linearVelocity = new Vector3(horizVel.x, rb.linearVelocity.y, horizVel.z);
    }

    [Command] public void CmdPauseGame()  => GameManager.Instance.PauseGame(netIdentity);
    [Command] public void CmdResumeGame() => GameManager.Instance.ResumeGame(netIdentity);

    public void SetKnockbackState(bool inKnockback, float movementMultiplier)
    {
        isInKnockback = inKnockback;
        knockbackMovementMultiplier = movementMultiplier;
    }

    /*  Called by PlayerAnimator when Emote parameter changes */
    public void SetEmoteState(bool emoteActive)
    {
        isEmoting = emoteActive;
        if (isEmoting)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            horizVel = Vector3.zero;
        }
    }
}
