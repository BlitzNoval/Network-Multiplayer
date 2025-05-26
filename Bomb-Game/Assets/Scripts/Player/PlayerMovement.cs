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

    Rigidbody  rb;
    InputAction moveAct;
    Vector2    moveInput;
    Vector3    horizVel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        var pi = GetComponent<PlayerInput>();
        moveAct = pi.actions.FindAction("Move");
    }

    void OnEnable()  { if (isLocalPlayer) moveAct.Enable(); }
    void OnDisable() { if (isLocalPlayer) moveAct.Disable(); }

    void Update()
    {
        if (!isLocalPlayer || !GameManager.Instance || !GameManager.Instance.GameActive)
            return;

        // Check for ESC key press to pause or resume
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameManager.Instance.IsPaused)
            {
                // Only the player who paused can resume
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
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer || !GameManager.Instance || !GameManager.Instance.GameActive)
            return;

        if (GameManager.Instance.IsPaused)
        {
            // Stop movement when paused
            rb.linearVelocity = Vector3.zero;
            return;
        }

        ApplyMovement();
    }

    void ApplyMovement()
    {
        if (moveInput.magnitude > 1f) moveInput.Normalize();
        Vector3 target = new Vector3(moveInput.x, 0, moveInput.y) * speed;

        horizVel = Vector3.MoveTowards(
            horizVel,
            target,
            (target.magnitude > 0.01f ? acceleration : deceleration) * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(horizVel.x, rb.linearVelocity.y, horizVel.z);

        if (horizVel.magnitude > 0.1f)
        {
            Quaternion rot = Quaternion.LookRotation(horizVel.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    [Command]
    public void CmdPauseGame()
    {
        GameManager.Instance.PauseGame(netIdentity);
    }

    [Command]
    public void CmdResumeGame()
    {
        GameManager.Instance.ResumeGame(netIdentity);
    }
}