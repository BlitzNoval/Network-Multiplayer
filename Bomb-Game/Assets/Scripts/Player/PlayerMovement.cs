using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class PlayerMovement : NetworkBehaviour
{
    /* ───────── Inspector ───────── */
    [Header("Movement")]
    [SyncVar] public float speed = 5f;
    [SyncVar] public float acceleration = 10f;
    [SyncVar] public float deceleration = 10f;
    [SyncVar] public float rotationSpeed = 10f;

    [Header("Knock-back")]
    bool  isInKnockback;
    float knockbackMovementMultiplier = 1f;

    /* ───────── Private ───────── */
    Rigidbody   rb;
    PlayerInput pi;
    InputAction moveAct, aimAct;
    Vector2     moveInput;
    Vector3     horizVel;
    bool        isEmoting;

    string lastControlScheme;     // cached to avoid null on first frame

    /* ───────── Lifecycle ───────── */
    void Awake()                  // run on ALL instances – owner or not
    {
        rb = GetComponent<Rigidbody>();
        pi = GetComponent<PlayerInput>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)            // ghosts don’t need inputs
            pi.enabled = false;
    }

    public override void OnStartAuthority()      // owner only
    {
        moveAct = pi.actions["Move"];
        aimAct  = pi.actions["Aim"];
        moveAct.Enable();
        aimAct.Enable();
        enabled = true;
    }

    public override void OnStopAuthority()
    {
        moveAct?.Disable();
        aimAct?.Disable();
        enabled = false;
    }

    /* ───────── Update ───────── */
    void Update()
    {
        if (!isLocalPlayer || !GameRunning()) return;

        // in-game pause hotkey
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

        moveInput = isEmoting ? Vector2.zero : moveAct.ReadValue<Vector2>();

        HandleRotation();
    }

    void HandleRotation()
    {
        // cache scheme to survive first-frame null
        string scheme = pi.currentControlScheme ?? lastControlScheme;
        lastControlScheme = scheme;

        if (scheme == "KeyboardMouse")
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (mousePos == Vector2.zero) return;

            if (new Plane(Vector3.up, transform.position)
                .Raycast(cam.ScreenPointToRay(mousePos), out float d))
            {
                Vector3 dir = cam.ScreenPointToRay(mousePos).GetPoint(d) - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                    SmoothLook(dir);
            }
        }
        else if (scheme == "Gamepad")
        {
            Vector2 aim = aimAct.ReadValue<Vector2>();
            if (aim.sqrMagnitude > 0.01f)
                SmoothLook(new Vector3(aim.x, 0, aim.y));
        }
    }

    void SmoothLook(Vector3 dir)
    {
        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    /* ───────── FixedUpdate ───────── */
    void FixedUpdate()
    {
        if (!isLocalPlayer || !GameRunning()) return;

        if (GameManager.Instance.IsPaused)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (isEmoting)
        {
            // only the owner freezes itself – prevents NRE on ghosts
            if (isLocalPlayer)
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            horizVel = Vector3.zero;
            return;
        }

        ApplyMovement();
    }

    void ApplyMovement()
    {
        if (moveInput.magnitude > 1f) moveInput.Normalize();

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camF = cam.transform.forward; camF.y = 0; camF.Normalize();
        Vector3 camR = cam.transform.right;   camR.y = 0; camR.Normalize();

        Vector3 target = (camR * moveInput.x + camF * moveInput.y) *
                         speed * knockbackMovementMultiplier;

        float accel = (target.sqrMagnitude > 0.0001f ? acceleration : deceleration) *
                      Time.fixedDeltaTime * (isInKnockback ? 0.5f : 1f);

        horizVel = Vector3.MoveTowards(horizVel, target, accel);
        rb.linearVelocity = new Vector3(horizVel.x, rb.linearVelocity.y, horizVel.z);
    }

    /* ───────── RPCs & helpers ───────── */
    [Command] public void CmdPauseGame()  => GameManager.Instance.PauseGame(netIdentity);
    [Command] public void CmdResumeGame() => GameManager.Instance.ResumeGame(netIdentity);

    bool GameRunning() => GameManager.Instance && GameManager.Instance.GameActive;

    public void SetKnockbackState(bool active, float multiplier)
    {
        isInKnockback = active;
        knockbackMovementMultiplier = multiplier;
    }

    public void SetEmoteState(bool active)
    {
        isEmoting = active;
        if (isLocalPlayer && active)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            horizVel = Vector3.zero;
        }
    }

    public Vector2 GetMoveInput()
    {
        return moveInput;
    }
        
    
}
