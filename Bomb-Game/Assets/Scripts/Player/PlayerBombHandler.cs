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
    
    [Header("Throw Types")]
    [SerializeField] float shortThrowSpeed = 15f;
    [SerializeField] float shortThrowUpward = 3f;
    [SerializeField] float lobThrowSpeed = 8f;
    [SerializeField] float lobThrowUpward = 8f;
    
    [Header("Aiming")]
    [SerializeField] float mouseSensitivity = 1.5f; // Simple and consistent
    [SerializeField] float controllerSensitivity = 3f; // Simple and consistent
    [SerializeField] float aimingRange = 10f;
    
    
    // Core components
    Bomb currentBomb;
    public Bomb CurrentBomb => currentBomb;
    LineRenderer lr;
    Camera playerCamera;
    
    // Aiming state
    bool isAiming;
    Vector3 aimDirection;
    Vector3 targetAimDirection; // For smooth aiming
    List<Vector3> trajectoryPoints = new();
    float timeStep;
    [SerializeField] float aimSmoothSpeed = 8f; // Simple, not too fast or slow
    
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

        playerInput = GetComponent<PlayerInput>();
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        // Get new input actions
        toggleThrowTypeAct = playerInput.actions["ToggleThrowTypes"];
        aimAct = playerInput.actions["Aim"];
        holdAimAct = playerInput.actions["HoldAim"];
        cancelAimAct = playerInput.actions["CancelAim"];
        
        Debug.Log($"Awake: Input actions bound - ToggleThrowType: {toggleThrowTypeAct?.name}, Aim: {aimAct?.name}, HoldAim: {holdAimAct?.name}, CancelAim: {cancelAimAct?.name}", this);

        playerAnimator = GetComponent<PlayerAnimator>();
        animator = GetComponent<Animator>();
        
        // Find camera - try main camera first, then camera tagged as MainCamera
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = GameObject.FindGameObjectWithTag("MainCamera")?.GetComponent<Camera>();

        timeStep = Time.fixedDeltaTime;
        
        // Initialize aim direction to forward
        aimDirection = transform.forward;
    }

    void Start()
    {
        StartCoroutine(SubscribeToInput());
    }

    IEnumerator SubscribeToInput()
    {
        while (!isLocalPlayer)
        {
            Debug.Log($"SubscribeToInput: Waiting for isLocalPlayer to be true for {gameObject.name}", this);
            yield return new WaitForSeconds(0.1f);
        }

        if (inputSubscribed)
        {
            Debug.Log($"SubscribeToInput: Input already subscribed for {gameObject.name}", this);
            yield break;
        }

        // Subscribe to input events
        toggleThrowTypeAct.performed += ToggleThrowType;
        holdAimAct.started += StartAiming;
        holdAimAct.canceled += ExecuteThrow;
        cancelAimAct.performed += CancelAiming;
        
        inputSubscribed = true;
        Debug.Log($"SubscribeToInput: Input subscribed for {gameObject.name}, isLocalPlayer={isLocalPlayer}", this);
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
        if (!isLocalPlayer || (GameManager.Instance != null && GameManager.Instance.IsPaused))
        {
            if (isAiming)
            {
                HideTrajectory();
                isAiming = false;
            }
            return;
        }

        // Update aim input if we're aiming
        if (isAiming)
        {
            UpdateAimDirection();
            
            // Ultra-responsive trajectory updates for perfect feel
            DrawTrajectory(); // Update every frame for maximum responsiveness
        }

        // Clear bomb reference if bomb is no longer held
        if (currentBomb == null && isAiming)
        {
            HideTrajectory();
            isAiming = false;
        }
    }

    void UpdateAimDirection()
    {
        if (!isAiming) return;

        // Get aim input (mouse delta or right stick)
        Vector2 aimInput = aimAct.ReadValue<Vector2>();
        Vector3 newTargetDirection = targetAimDirection;
        
        // SIMPLE input handling - same for everyone
        if (aimInput.magnitude > 0.1f)
        {
            Vector3 inputDirection = new Vector3(aimInput.x, 0, aimInput.y);
            
            // Simple sensitivity scaling
            float sensitivity = playerInput.currentControlScheme == "KeyboardMouse" ? mouseSensitivity : controllerSensitivity;
            inputDirection *= sensitivity * Time.deltaTime;
            
            // Simple direction update - no complex camera math
            newTargetDirection = (targetAimDirection + inputDirection).normalized;
        }
        
        // Simple interpolation
        targetAimDirection = newTargetDirection;
        aimDirection = Vector3.Slerp(aimDirection, targetAimDirection, aimSmoothSpeed * Time.deltaTime);
    }

    

    public void SetBomb(Bomb b)
    {
        currentBomb = b;
        Debug.Log($"SetBomb called for {gameObject.name}, bomb: {b}, IsHeld={b?.IsHeld}", this);
        UpdateHandAnimationState();
    }

    public void ClearBomb()
    {
        if (isAiming)
        {
            HideTrajectory();
            isAiming = false;
        }
        
        currentBomb = null;
        Debug.Log($"ClearBomb called for {gameObject.name}", this);
        UpdateHandAnimationState();
    }

    void UpdateHandAnimationState()
    {
        if (animator != null && currentBomb != null)
        {
            // Always use right hand for consistency with new system
            animator.SetInteger("activeHand", 2);
        }
        else if (animator != null)
        {
            animator.SetInteger("activeHand", 0);
        }
    }

    void ToggleThrowType(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        
        CmdToggleThrowType();
        Debug.Log($"ToggleThrowType: Switched to {(currentThrowType == ThrowType.Short ? "Lob" : "Short")} throw type", this);
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
        aimDirection = transform.forward; // Initialize aim direction
        targetAimDirection = transform.forward; // Initialize target direction
        
        Debug.Log($"StartAiming: Started aiming with {currentThrowType} throw type", this);
    }

    void CancelAiming(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !isAiming) return;
        
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
        
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
        
        // Store throw parameters for prediction
        Vector3 throwDirection = aimDirection;
        ThrowType throwType = currentThrowType;
        
        // Stop aiming
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
        
        // Trigger throw animation
        if (animator != null)
            animator.SetTrigger("Throw");
        
        // No prediction - keep it simple and identical for everyone
        
        // Send throw command to server
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
        
        // More lenient timing check for better client responsiveness
        if (currentBomb && currentBomb.Holder == gameObject && currentBomb.CurrentTimer > 1.0f)
        {
            if (playerAnimator != null)
                playerAnimator.OnBombThrow();
            
            // Throw the bomb using new method with direction
            bool useShortThrow = throwType == ThrowType.Short;
            currentBomb.ThrowBomb(direction, useShortThrow);
        }
        else
        {
            Debug.LogWarning($"CmdThrowBomb: Rejected throw - bomb: {currentBomb != null}, holder: {currentBomb?.Holder == gameObject}, timer: {currentBomb?.CurrentTimer}", this);
        }
    }

    void DrawTrajectory()
    {
        trajectoryPoints.Clear();
        if (currentBomb == null || !isAiming)
        {
            HideTrajectory();
            return;
        }

        // Always use right hand hold point for consistency
        Transform origin = transform.Find("RightHoldPoint");
        if (!origin)
        {
            HideTrajectory();
            return;
        }

        // Get throw parameters based on current throw type
        float speed = currentThrowType == ThrowType.Short ? shortThrowSpeed : lobThrowSpeed;
        float upward = currentThrowType == ThrowType.Short ? shortThrowUpward : lobThrowUpward;

        Vector3 startPos = origin.position;
        Vector3 velocity = aimDirection * speed + Vector3.up * upward;
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
        Color trajectoryColor = currentThrowType == ThrowType.Short ? Color.blue : Color.yellow;
        lr.startColor = lr.endColor = trajectoryColor;
    }

    void HideTrajectory()
    {
        if (lr != null)
            lr.positionCount = 0;
    }

    // Public method to get current throw type for UI display
    public ThrowType GetCurrentThrowType()
    {
        return currentThrowType;
    }

    // Public method to get aim direction for debugging
    public Vector3 GetAimDirection()
    {
        return aimDirection;
    }

}