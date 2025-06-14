using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

[RequireComponent(typeof(LineRenderer), typeof(PlayerInput))]
public class PlayerBombHandler : NetworkBehaviour
{
    [Header("Trajectory")]
    [SerializeField, Range(10,300)] int maxPoints = 100;
    [SerializeField] LayerMask collisionMask;
    
    
    [Header("Aiming")]
    [SerializeField] float mouseSensitivity = 1.5f;
    [SerializeField] float controllerSensitivity = 3f;
    [SerializeField] float aimingRange = 10f;
    [SerializeField] float aimSmoothSpeed = 8f;
    [SerializeField] float networkSyncRate = 0.05f; // How often to sync aim direction
    
    // Core components
    Bomb currentBomb;
    public Bomb CurrentBomb => currentBomb;
    LineRenderer lr;
    Camera playerCamera;
    
    // Aiming state - Now properly synchronized
    [SyncVar(hook = nameof(OnNetworkAimDirectionChanged))]
    Vector3 networkAimDirection;
    
    [SyncVar]
    bool networkIsAiming;
    
    Vector3 localAimDirection;
    Vector3 smoothedAimDirection;
    bool isAiming;
    
    List<Vector3> trajectoryPoints = new();
    float timeStep;
    float lastNetworkSync;
    
    // Throw type state
    public enum ThrowType { Short, Lob }
    [SyncVar] ThrowType currentThrowType = ThrowType.Short;
    
    // Input
    InputAction toggleThrowTypeAct, aimAct, holdAimAct, cancelAimAct;
    PlayerInput playerInput;
    bool inputSubscribed;
    Vector2 currentAimInput;
    bool isHoldingAim;
    
    // Animation
    private PlayerAnimator playerAnimator;
    private Animator animator;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        lr.startWidth = lr.endWidth = 0.1f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.material.color = new Color(1f, 1f, 1f, 0.7f);
        timeStep = 0.02f;
        
        playerInput = GetComponent<PlayerInput>();
        playerCamera = Camera.main;
        
        // Initialize aim directions
        localAimDirection = transform.forward;
        smoothedAimDirection = transform.forward;
        networkAimDirection = transform.forward;
        
        // Get animation components
        playerAnimator = GetComponent<PlayerAnimator>();
        animator = GetComponent<Animator>();
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        Debug.Log($"OnStartAuthority: PlayerBombHandler started for {gameObject.name}", this);
        SubscribeToInput();
    }

    void OnEnable()
    {
        if (isLocalPlayer)
        {
            SubscribeToInput();
        }
    }

    void SubscribeToInput()
    {
        if (!isLocalPlayer || inputSubscribed) return;
        
        try
        {
            // Get actions directly from playerInput.actions using the indexer
            toggleThrowTypeAct = playerInput.actions["ToggleThrowTypes"]; // Note: "ToggleThrowTypes" with 's' based on your code
            aimAct = playerInput.actions["Aim"];
            holdAimAct = playerInput.actions["HoldAim"];
            cancelAimAct = playerInput.actions["CancelAim"];
            
            if (toggleThrowTypeAct == null || aimAct == null || holdAimAct == null || cancelAimAct == null)
            {
                Debug.LogError($"SubscribeToInput: One or more actions not found - Toggle: {toggleThrowTypeAct?.name}, Aim: {aimAct?.name}, HoldAim: {holdAimAct?.name}, CancelAim: {cancelAimAct?.name}", this);
                return;
            }
            
            toggleThrowTypeAct.performed += ToggleThrowType;
            holdAimAct.started += StartAiming;
            holdAimAct.canceled += ExecuteThrow;
            cancelAimAct.performed += CancelAiming;
            
            inputSubscribed = true;
            Debug.Log($"SubscribeToInput: Input subscribed for {gameObject.name}, isLocalPlayer={isLocalPlayer}", this);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SubscribeToInput: Failed to subscribe to input actions - {e.Message}", this);
        }
    }

    void OnDisable()
    {
        if (!isLocalPlayer || !inputSubscribed) return;
        
        toggleThrowTypeAct.performed -= ToggleThrowType;
        holdAimAct.started -= StartAiming;
        holdAimAct.canceled -= ExecuteThrow;
        cancelAimAct.performed -= CancelAiming;
        
        inputSubscribed = false;
        Debug.Log($"PlayerBombHandler disabled for {gameObject.name}, input unsubscribed", this);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            if (isAiming)
            {
                HideTrajectory();
                isAiming = false;
            }
            return;
        }

        if (isLocalPlayer)
        {
            // Update aim direction locally for smooth control
            if (isAiming)
            {
                UpdateLocalAimDirection();
                
                // Sync to network periodically
                if (Time.time - lastNetworkSync > networkSyncRate)
                {
                    CmdUpdateAimDirection(localAimDirection);
                    lastNetworkSync = Time.time;
                }
                
                // Draw trajectory using smoothed direction
                DrawTrajectory();
            }

            // Clear bomb reference if bomb is no longer held
            if (currentBomb == null && isAiming)
            {
                HideTrajectory();
                isAiming = false;
                CmdSetAiming(false);
            }
        }
        else
        {
            // Non-local players: smooth between network updates
            if (networkIsAiming)
            {
                smoothedAimDirection = Vector3.Slerp(smoothedAimDirection, networkAimDirection, Time.deltaTime * aimSmoothSpeed * 2f);
                DrawTrajectory();
            }
            else
            {
                HideTrajectory();
            }
        }
    }

    void UpdateLocalAimDirection()
    {
        if (!isAiming) return;

        // Get aim input (mouse delta or right stick)
        Vector2 aimInput = aimAct.ReadValue<Vector2>();
        
        if (aimInput.magnitude > 0.1f)
        {
            Vector3 inputDirection = new Vector3(aimInput.x, 0, aimInput.y);
            
            // Apply sensitivity
            float sensitivity = playerInput.currentControlScheme == "KeyboardMouse" ? 
                mouseSensitivity : controllerSensitivity;
            
            inputDirection *= sensitivity * Time.deltaTime;
            
            // Convert to world space relative to camera
            if (playerCamera != null)
            {
                Vector3 forward = playerCamera.transform.forward;
                Vector3 right = playerCamera.transform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();
                
                localAimDirection += right * inputDirection.x + forward * inputDirection.z;
            }
            else
            {
                localAimDirection += inputDirection;
            }
            
            // Normalize and clamp to range
            if (localAimDirection.magnitude > aimingRange)
            {
                localAimDirection = localAimDirection.normalized * aimingRange;
            }
            
            localAimDirection.y = 0;
            if (localAimDirection.magnitude < 0.1f)
            {
                localAimDirection = transform.forward;
            }
            localAimDirection.Normalize();
        }
        
        // Smooth the aim direction locally
        smoothedAimDirection = Vector3.Slerp(smoothedAimDirection, localAimDirection, Time.deltaTime * aimSmoothSpeed);
    }

    [Command]
    void CmdUpdateAimDirection(Vector3 direction)
    {
        networkAimDirection = direction;
    }

    [Command]
    void CmdSetAiming(bool aiming)
    {
        networkIsAiming = aiming;
    }

    void OnNetworkAimDirectionChanged(Vector3 oldValue, Vector3 newValue)
    {
        // Smooth transition on non-local clients
        if (!isLocalPlayer)
        {
            smoothedAimDirection = Vector3.Slerp(smoothedAimDirection, newValue, 0.5f);
        }
    }

    void ToggleThrowType(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        
        CmdToggleThrowType();
        Debug.Log($"ToggleThrowType: Switched to {(currentThrowType == ThrowType.Lob ? "Lob" : "Short")} throw type", this);
    }

    void StartAiming(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || currentBomb == null) return;
        
        if (!currentBomb.IsHeld || currentBomb.Holder != gameObject)
        {
            Debug.LogWarning($"StartAiming: Bomb is not properly held by this player.", this);
            return;
        }
        
        isAiming = true;
        isHoldingAim = true;
        localAimDirection = transform.forward;
        smoothedAimDirection = transform.forward;
        
        CmdSetAiming(true);
        
        Debug.Log($"StartAiming: Started aiming with {currentThrowType} throw type", this);
    }

    void CancelAiming(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !isAiming) return;
        
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
        
        CmdSetAiming(false);
        
        Debug.Log("CancelAiming: Cancelled aiming", this);
    }

    void ExecuteThrow(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !isAiming || currentBomb == null) return;
        
        if (!currentBomb.IsHeld || currentBomb.Holder != gameObject)
        {
            Debug.LogWarning($"ExecuteThrow: Cannot throw - bomb not properly held", this);
            return;
        }
        
        // Use the smoothed aim direction for the throw
        Vector3 throwDirection = smoothedAimDirection;
        ThrowType throwType = currentThrowType;
        
        // Stop aiming
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
        
        CmdSetAiming(false);
        
        // Trigger throw animation
        if (animator != null)
            animator.SetTrigger("Throw");
        
        // Send throw command to server with final direction
        CmdThrowBomb(throwDirection, throwType);
        
        Debug.Log($"ExecuteThrow: Throwing bomb in direction {throwDirection} with {throwType} throw type", this);
    }

    [Command]
    void CmdToggleThrowType()
    {
        currentThrowType = currentThrowType == ThrowType.Short ? ThrowType.Lob : ThrowType.Short;
        Debug.Log($"CmdToggleThrowType: Server switched to {currentThrowType} throw type", this);
    }

    [Command]
    void CmdThrowBomb(Vector3 direction, ThrowType throwType)
    {
        Debug.Log($"CmdThrowBomb: Server received throw command - direction: {direction}, type: {throwType}", this);
        
        if (currentBomb && currentBomb.Holder == gameObject && currentBomb.CurrentTimer > 1.0f)
        {
            if (playerAnimator != null)
                playerAnimator.OnBombThrow();
            
            // Throw the bomb using the direction from client
            bool useShortThrow = throwType == ThrowType.Short;
            currentBomb.ThrowBomb(direction, useShortThrow);
            
            // Clear network aiming state
            networkIsAiming = false;
        }
        else
        {
            Debug.LogWarning($"CmdThrowBomb: Rejected throw - bomb: {currentBomb != null}, holder: {currentBomb?.Holder == gameObject}, timer: {currentBomb?.CurrentTimer}", this);
        }
    }

    void DrawTrajectory()
    {
        trajectoryPoints.Clear();
        if (currentBomb == null || (!isAiming && !networkIsAiming))
        {
            HideTrajectory();
            return;
        }

        // Use right hand hold point for consistency
        Transform origin = transform.Find("RightHoldPoint");
        if (!origin)
        {
            HideTrajectory();
            return;
        }

        // Get throw parameters from the bomb itself
        if (currentBomb == null) return;
        
        float speed = currentThrowType == ThrowType.Short ? currentBomb.NormalThrowSpeed : currentBomb.LobThrowSpeed;
        float upward = currentThrowType == ThrowType.Short ? currentBomb.NormalThrowUpward : currentBomb.LobThrowUpward;

        Vector3 startPos = origin.position;
        Vector3 velocity = smoothedAimDirection * speed + Vector3.up * upward;
        Vector3 lastPos = startPos;
        trajectoryPoints.Add(lastPos);

        float t = 0f;
        for (int i = 0; i < maxPoints; ++i)
        {
            t += timeStep;
            Vector3 next = startPos + velocity * t + 0.5f * Physics.gravity * t * t;
            
            if (Physics.Raycast(lastPos, next - lastPos, out RaycastHit hit,
                                (next - lastPos).magnitude, collisionMask))
            {
                trajectoryPoints.Add(hit.point);
                break;
            }
            
            trajectoryPoints.Add(next);
            lastPos = next;
        }

        lr.positionCount = trajectoryPoints.Count;
        lr.SetPositions(trajectoryPoints.ToArray());
        
        // Color based on throw type: Blue for short, Yellow for lob
        Color trajectoryColor = currentThrowType == ThrowType.Short ? 
            new Color(0.3f, 0.6f, 1f, 0.8f) : new Color(1f, 0.9f, 0.3f, 0.8f);
        lr.startColor = lr.endColor = trajectoryColor;
    }

    void HideTrajectory()
    {
        lr.positionCount = 0;
        trajectoryPoints.Clear();
    }

    public void SetBomb(Bomb b)
    {
        currentBomb = b;
        Debug.Log($"SetBomb: Bomb assigned to {gameObject.name}", this);
    }

    public void ClearBomb()
    {
        currentBomb = null;
        if (isAiming)
        {
            isAiming = false;
            HideTrajectory();
            if (isLocalPlayer)
            {
                CmdSetAiming(false);
            }
        }
        Debug.Log($"ClearBomb: Bomb cleared from {gameObject.name}", this);
    }
}