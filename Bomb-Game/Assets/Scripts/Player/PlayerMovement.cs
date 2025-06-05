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
    
    [Header("Bomb Holder Boost")]
    [SerializeField] private float bombHolderSpeedMultiplier = 1.3f; // 30% speed boost when holding bomb

    Rigidbody  rb;
    InputAction moveAct;
    Vector2    moveInput;
    Vector3    horizVel;
    PlayerBombHandler bombHandler;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        var pi = GetComponent<PlayerInput>();
        moveAct = pi.actions.FindAction("Move");
        bombHandler = GetComponent<PlayerBombHandler>();
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
        
        // Apply speed boost if holding bomb
        float currentSpeed = speed;
        if (bombHandler != null && bombHandler.CurrentBomb != null && bombHandler.CurrentBomb.Holder == gameObject)
        {
            currentSpeed *= bombHolderSpeedMultiplier;
        }
        
        Vector3 target = new Vector3(moveInput.x, 0, moveInput.y) * currentSpeed;

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