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

    [Header("Emoticon System")]
    // No longer need individual EmoticonSelectionUI - using shared SimpleEmoticonPanel

    /* ───────── Private ───────── */
    Rigidbody   rb;
    PlayerInput pi;
    InputAction moveAct, aimAct, emoticonAct;
    Vector2     moveInput;
    Vector3     horizVel;
    bool        isEmoting;
    PlayerLifeManager playerLifeManager;

    string lastControlScheme;     // cached to avoid null on first frame
    bool wasHoldingEmoticon;
    public bool isEmoticonPanelOpen = false; // Public so bomb handler can check it

    public Vector3 CurrentAimDirection { get; private set; } = Vector3.forward; // Default to forward

    /* ───────── Lifecycle ───────── */
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pi = GetComponent<PlayerInput>();
        playerLifeManager = GetComponent<PlayerLifeManager>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
            pi.enabled = false;
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        moveAct = pi.actions["Move"];
        aimAct  = pi.actions["Aim"];
        emoticonAct = pi.actions["Emoticon"];
        moveAct.Enable();
        aimAct.Enable();
        emoticonAct.Enable();
        enabled = true;
    }

    public override void OnStopAuthority()
    {
        moveAct?.Disable();
        aimAct?.Disable();
        emoticonAct?.Disable();
        enabled = false;
    }

    /* ───────── Update ───────── */
    void Update()
    {
        // Debug: Check early return conditions
        if (!isLocalPlayer)
        {
            return; // Not local player
        }
        
        if (!GameRunning())
        {
            Debug.LogWarning("GameRunning() returned false - emoticon input will not be processed", this);
            return; // Game not running
        }
        
        if (playerLifeManager != null && playerLifeManager.isInKnockback)
        {
            return; // Player in knockback
        }

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
        
        // Test: Simple keyboard check for debugging
        if (UnityEngine.InputSystem.Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            Debug.Log("Direct keyboard check: 4 key was pressed this frame!", this);
        }
        
        HandleEmoticonInput();
    }

    void HandleRotation()
    {
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
                {
                    CurrentAimDirection = dir.normalized;
                    SmoothLook(CurrentAimDirection);
                }
            }
        }
        else if (scheme == "Gamepad")
        {
            Vector2 aim = aimAct.ReadValue<Vector2>();
            if (aim.sqrMagnitude > 0.01f)
            {
                CurrentAimDirection = new Vector3(aim.x, 0, aim.y).normalized;
                SmoothLook(CurrentAimDirection);
            }
        }
    }

    void SmoothLook(Vector3 dir)
    {
        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    void HandleEmoticonInput()
    {
        // Debug: Check if emoticonAct is null
        if (emoticonAct == null)
        {
            Debug.LogError("emoticonAct is null!");
            return;
        }

        // Simple system: Show/Hide shared panel based on key press
        bool isHoldingEmoticon = emoticonAct.IsPressed();
        
        if (isHoldingEmoticon && !wasHoldingEmoticon)
        {
            Debug.Log("Emoticon key pressed - showing shared panel");
            isEmoticonPanelOpen = true;
            ShowEmoticonPanel();
        }
        else if (!isHoldingEmoticon && wasHoldingEmoticon)
        {
            Debug.Log("Emoticon key released - hiding shared panel");
            isEmoticonPanelOpen = false;
            HideEmoticonPanel();
        }

        wasHoldingEmoticon = isHoldingEmoticon;
    }

    void ShowEmoticonPanel()
    {
        Debug.Log("ShowEmoticonPanel called");
        
        int myPlayerNumber = GetMyPlayerNumber();
        Debug.Log($"My player number is: {myPlayerNumber}");
        
        // Debug: Print all registered panels
        SimpleEmoticonPanel.DebugPrintRegisteredPanels();
        
        // Find the correct panel for this player
        SimpleEmoticonPanel myPanel = GetMyEmoticonPanel();
        if (myPanel != null)
        {
            Debug.Log($"Showing emoticon panel for player {myPlayerNumber}");
            myPanel.ShowPanel();
        }
        else
        {
            Debug.LogError($"Could not find SimpleEmoticonPanel for player {myPlayerNumber}! Make sure each PlayerPanel has a SimpleEmoticonPanel with the correct player number.");
        }
    }

    void HideEmoticonPanel()
    {
        Debug.Log("HideEmoticonPanel called");
        
        // Find the correct panel for this player
        SimpleEmoticonPanel myPanel = GetMyEmoticonPanel();
        if (myPanel != null)
        {
            Debug.Log($"Hiding emoticon panel for player {GetMyPlayerNumber()}");
            myPanel.HidePanel();
        }
        else
        {
            Debug.LogError($"Could not find SimpleEmoticonPanel for player {GetMyPlayerNumber()}!");
        }
    }

    SimpleEmoticonPanel GetMyEmoticonPanel()
    {
        int myPlayerNumber = GetMyPlayerNumber();
        return SimpleEmoticonPanel.GetPanelForPlayer(myPlayerNumber);
    }

    int GetMyPlayerNumber()
    {
        PlayerLifeManager lifeManager = GetComponent<PlayerLifeManager>();
        if (lifeManager != null)
        {
            return lifeManager.PlayerNumber;
        }
        Debug.LogError("PlayerLifeManager not found on this player!");
        return 1; // Default fallback
    }

    // Public method called by EmoticonSelectionUI when a button is clicked
    public void SelectEmoticon(int emoticonIndex)
    {
        Debug.Log($"SelectEmoticon called with index {emoticonIndex}");
        CmdSelectEmoticon(emoticonIndex);
    }

    [Command]
    void CmdSelectEmoticon(int index)
    {
        Debug.Log($"CmdSelectEmoticon called with index {index}", this);
        RpcShowEmoticon(netIdentity, index);
    }

    [ClientRpc]
    void RpcShowEmoticon(NetworkIdentity playerId, int index)
    {
        Debug.Log($"RpcShowEmoticon called for player {playerId.name} with emoticon index {index}", this);
        
        // Find which player sent this emoticon
        GameObject player = playerId.gameObject;
        PlayerLifeManager lifeManager = player.GetComponent<PlayerLifeManager>();
        if (lifeManager != null)
        {
            int senderPlayerNumber = lifeManager.PlayerNumber;
            Debug.Log($"Emoticon sent by player number: {senderPlayerNumber}");
            
            // Show the animation on the panel for the player who sent it
            SimpleEmoticonPanel senderPanel = SimpleEmoticonPanel.GetPanelForPlayer(senderPlayerNumber);
            if (senderPanel != null)
            {
                Debug.Log($"Showing emoticon {index} animation on Player {senderPlayerNumber} panel", this);
                senderPanel.ShowEmoticonAnimation(index);
            }
            else
            {
                Debug.LogError($"Could not find SimpleEmoticonPanel for player {senderPlayerNumber}", this);
            }
        }
        else
        {
            Debug.LogError($"PlayerLifeManager not found on {player.name}", this);
        }
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

    public Vector2 GetMoveInput() => moveInput;
}