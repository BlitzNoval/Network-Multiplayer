using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

[RequireComponent(typeof(PlayerInput))]
public class PlayerBombHandler : NetworkBehaviour
{
    [Header("Trajectory")]
    [SerializeField, Range(10,300)] int maxPoints = 100;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] private GameObject trajectoryPointPrefab; // Prefab for dotted line points
    [SerializeField] private float trajectoryPointSpacing = 0.5f; // Space between dots
    [SerializeField] private Sprite landingMarkerSprite; // Sprite for landing indicator
    [SerializeField] private GameObject landingMarkerPrefab; // Prefab with SpriteRenderer
    [SerializeField] private string landingMarkerLayer = "UI"; // Layer for landing marker
    
    
    [Header("Visual Settings")]
    [SerializeField] private Color shortThrowColor = Color.white;
    [SerializeField] private Color lobThrowColor = new Color(1f, 1f, 1f, 0.6f); // Slightly transparent white
    [SerializeField] private float trajectoryFadeDistance = 0.8f; // Fade dots over distance
    
    [Header("Aiming")]
    [SerializeField] float mouseSensitivity = 1.5f;
    [SerializeField] float controllerSensitivity = 3f;
    [SerializeField] float aimingRange = 10f;
    
    // Core components
    Bomb currentBomb;
    public Bomb CurrentBomb => currentBomb;
    Camera playerCamera;
    
    // Trajectory visualization
    private List<GameObject> trajectoryDots = new List<GameObject>();
    private List<Material> trajectoryDotMaterials = new List<Material>(); // Pre-created materials for performance
    private GameObject landingMarker;
    private List<GameObject> bounceMarkers = new List<GameObject>();
    private int activeDotCount = 0;
    
    // Aiming state
    bool isAiming;
    Vector3 aimDirection;
    Vector3 targetAimDirection;
    List<Vector3> trajectoryPoints = new();
    float timeStep = 0.02f; // Smaller timestep for more accurate trajectory calculation
    [SerializeField] float aimSmoothSpeed = 8f;
    
    // Performance optimization for trajectory calculation
    [Header("Performance")]
    [SerializeField] float trajectoryUpdateRate = 10f; // Updates per second
    [SerializeField] float aimDirectionThreshold = 0.05f; // Minimum change to trigger recalculation
    private float lastTrajectoryUpdate = 0f;
    private Vector3 lastCachedAimDirection = Vector3.zero;
    private ThrowType lastCachedThrowType = ThrowType.Short;
    private bool trajectoryNeedsUpdate = true;
    
    // Throw type state
    public enum ThrowType { Short, Lob }
    [SyncVar(hook = nameof(OnThrowTypeChanged))] ThrowType currentThrowType = ThrowType.Short;
    
    // Local landing marker (only visible to this player)
    Vector3 localLandingPosition = Vector3.zero;
    bool showLocalLandingMarker = false;
    
    // Input
    InputAction toggleThrowTypesAct, aimAct, holdAimAct, cancelAimAct;
    PlayerInput playerInput;
    bool inputSubscribed;
    Vector2 currentAimInput;
    bool isHoldingAim;
    
    // Animation
    private PlayerAnimator playerAnimator;
    private Animator animator;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerCamera = Camera.main;
        
        playerAnimator = GetComponent<PlayerAnimator>();
        animator = GetComponent<Animator>();
        
        InitializeTrajectoryVisualization();
    }

    void InitializeTrajectoryVisualization()
    {
        // Create landing marker
        if (landingMarkerPrefab != null)
        {
            landingMarker = Instantiate(landingMarkerPrefab);
            landingMarker.SetActive(false);
            
            // Set the sprite if provided
            if (landingMarkerSprite != null)
            {
                var spriteRenderer = landingMarker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = landingMarkerSprite;
                    spriteRenderer.sortingLayerName = landingMarkerLayer;
                    spriteRenderer.sortingOrder = 10; // Ensure it's on top
                }
            }
        }
        else
        {
            // Create a simple landing marker if no prefab provided
            landingMarker = new GameObject("LandingMarker");
            var spriteRenderer = landingMarker.AddComponent<SpriteRenderer>();
            if (landingMarkerSprite != null)
            {
                spriteRenderer.sprite = landingMarkerSprite;
            }
            else
            {
                // Create a simple circle sprite if none provided
                spriteRenderer.sprite = CreateCircleSprite();
            }
            spriteRenderer.sortingLayerName = landingMarkerLayer;
            spriteRenderer.sortingOrder = 10;
            landingMarker.transform.localScale = Vector3.one * 2f; // Adjust size
            landingMarker.SetActive(false);
        }
    }

    Sprite CreateCircleSprite()
    {
        // Create a simple circle texture for the landing marker
        Texture2D tex = new Texture2D(64, 64);
        Vector2 center = new Vector2(32, 32);
        float radius = 30;
        
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = 1f - (dist / radius);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.8f));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Landing marker is now only local - no need for network sync
    }

    void OnDestroy()
    {
        // Clean up trajectory dots and materials
        foreach (var dot in trajectoryDots)
        {
            if (dot != null) Destroy(dot);
        }
        trajectoryDots.Clear();
        
        foreach (var material in trajectoryDotMaterials)
        {
            if (material != null) Destroy(material);
        }
        trajectoryDotMaterials.Clear();
        
        // Clean up bounce markers
        foreach (var marker in bounceMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        bounceMarkers.Clear();
        
        // Clean up landing marker
        if (landingMarker != null) Destroy(landingMarker);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SubscribeToInput();
    }

    void SubscribeToInput()
    {
        if (inputSubscribed) return;

        toggleThrowTypesAct = playerInput.actions["ToggleThrowTypes"];
        aimAct = playerInput.actions["Aim"];
        holdAimAct = playerInput.actions["HoldAim"];
        cancelAimAct = playerInput.actions["CancelAim"];

        toggleThrowTypesAct.performed += ToggleThrowTypes;
        holdAimAct.started += StartAiming;
        holdAimAct.canceled += ExecuteThrow;
        cancelAimAct.performed += CancelAiming;

        inputSubscribed = true;
    }

    void OnDisable()
    {
        if (!isLocalPlayer || !inputSubscribed) return;
        
        toggleThrowTypesAct.performed -= ToggleThrowTypes;
        holdAimAct.started -= StartAiming;
        holdAimAct.canceled -= ExecuteThrow;
        cancelAimAct.performed -= CancelAiming;
        
        inputSubscribed = false;
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

        if (isAiming)
        {
            UpdateAimDirection();
            
            // Check if trajectory needs updating based on aim direction changes and update rate
            bool aimChanged = Vector3.Distance(aimDirection, lastCachedAimDirection) > aimDirectionThreshold;
            bool throwTypeChanged = currentThrowType != lastCachedThrowType;
            bool timeForUpdate = Time.time - lastTrajectoryUpdate >= (1f / trajectoryUpdateRate);
            
            if (trajectoryNeedsUpdate || aimChanged || throwTypeChanged || timeForUpdate)
            {
                DrawTrajectory();
                lastTrajectoryUpdate = Time.time;
                lastCachedAimDirection = aimDirection;
                lastCachedThrowType = currentThrowType;
                trajectoryNeedsUpdate = false;
            }
        }

        if (currentBomb == null && isAiming)
        {
            HideTrajectory();
            isAiming = false;
        }
    }

    void UpdateAimDirection()
    {
        if (!isAiming) return;

        Vector2 aimInput = aimAct.ReadValue<Vector2>();
        Vector3 newTargetDirection = targetAimDirection;
        
        if (aimInput.magnitude > 0.1f)
        {
            Vector3 inputDirection = new Vector3(aimInput.x, 0, aimInput.y);
            float sensitivity = playerInput.currentControlScheme == "KeyboardMouse" ? 
                mouseSensitivity : controllerSensitivity;
            inputDirection *= sensitivity * Time.deltaTime;
            newTargetDirection = (targetAimDirection + inputDirection).normalized;
        }
        
        targetAimDirection = newTargetDirection;
        aimDirection = Vector3.Slerp(aimDirection, targetAimDirection, aimSmoothSpeed * Time.deltaTime);
    }

    public void SetBomb(Bomb b)
    {
        currentBomb = b;
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
        UpdateHandAnimationState();
    }

    void UpdateHandAnimationState()
    {
        if (animator != null && currentBomb != null)
        {
            animator.SetInteger("activeHand", 2); // Right hand
        }
        else if (animator != null)
        {
            animator.SetInteger("activeHand", 0);
        }
    }

    void ToggleThrowTypes(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        CmdToggleThrowTypes();
    }

    void OnThrowTypeChanged(ThrowType oldType, ThrowType newType)
    {
        // Update visual feedback when throw type changes
        if (isAiming)
        {
            trajectoryNeedsUpdate = true; // Force recalculation on throw type change
            UpdateTrajectoryColors();
        }
    }

    [Command]
    void CmdToggleThrowTypes()
    {
        currentThrowType = currentThrowType == ThrowType.Short ? ThrowType.Lob : ThrowType.Short;
    }

    [Command]
    void CmdThrowBomb(Vector3 direction, ThrowType throwType)
    {
        if (currentBomb && currentBomb.Holder == gameObject && currentBomb.CurrentTimer > 1.0f)
        {
            if (playerAnimator != null)
                playerAnimator.OnBombThrow();
            
            bool useShortThrow = throwType == ThrowType.Short;
            currentBomb.ThrowBomb(direction, useShortThrow);
            
            // Landing marker will be hidden locally when aiming stops
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

        Transform origin = transform.Find("RightHoldPoint");
        if (!origin)
        {
            HideTrajectory();
            return;
        }

        // Get throw parameters from the bomb itself to match exact physics
        float speed = currentThrowType == ThrowType.Short ? currentBomb.NormalThrowSpeed : currentBomb.LobThrowSpeed;
        float upward = currentThrowType == ThrowType.Short ? currentBomb.NormalThrowUpward : currentBomb.LobThrowUpward;

        Vector3 startPos = origin.position;
        
        // Calculate initial velocity using the same physics as the bomb
        // The bomb uses AddForce with ForceMode.Impulse, which applies force directly as velocity change
        // Since the bomb's initial mass is 1f and gets multiplied by flightMassMultiplier during throw,
        // and ForceMode.Impulse applies force as velocity change, we can directly use the force as velocity
        Vector3 force = aimDirection * speed + Vector3.up * upward;
        Vector3 velocity = force; // ForceMode.Impulse with mass=1 means force equals velocity
        
        Vector3 lastPos = startPos;
        Vector3 landingPos = Vector3.zero;
        bool foundLanding = false;

        trajectoryPoints.Add(lastPos);

        float t = 0f;
        Vector3 currentVelocity = velocity;
        Vector3 currentPos = startPos;
        List<Vector3> bouncePoints = new List<Vector3>();
        int maxBounces = 3; // Limit bounces to prevent infinite loops
        int bounceCount = 0;
        
        for (int i = 0; i < maxPoints; ++i)
        {
            t += timeStep;
            
            // Apply gravity to velocity
            currentVelocity += Physics.gravity * timeStep;
            
            // Calculate next position
            Vector3 next = currentPos + currentVelocity * timeStep;
            
            if (Physics.Raycast(lastPos, next - lastPos, out RaycastHit hit,
                                (next - lastPos).magnitude, collisionMask))
            {
                trajectoryPoints.Add(hit.point);
                
                // Check if this is a bounce or final landing
                Vector3 surfaceNormal = hit.normal;
                float dotProduct = Vector3.Dot(currentVelocity.normalized, -surfaceNormal);
                
                // If hitting at a shallow angle and haven't exceeded max bounces, treat as bounce
                if (dotProduct < 0.7f && bounceCount < maxBounces && currentVelocity.magnitude > 5f)
                {
                    // This is a bounce
                    bouncePoints.Add(hit.point);
                    bounceCount++;
                    
                    // Calculate bounce velocity (simplified physics)
                    Vector3 bounceVelocity = Vector3.Reflect(currentVelocity, surfaceNormal);
                    bounceVelocity *= 0.6f; // Energy loss on bounce
                    
                    // Continue trajectory from bounce point
                    currentVelocity = bounceVelocity;
                    currentPos = hit.point + surfaceNormal * 0.01f; // Slightly offset from surface
                    lastPos = currentPos;
                    
                    continue;
                }
                else
                {
                    // This is the final landing
                    landingPos = hit.point;
                    foundLanding = true;
                    break;
                }
            }
            
            trajectoryPoints.Add(next);
            lastPos = next;
            currentPos = next;
        }
        
        // Update bounce markers
        UpdateBounceMarkers(bouncePoints);

        // Update dotted line trajectory
        UpdateDottedTrajectory();
        
        // Update landing marker (local only)
        if (foundLanding && isLocalPlayer)
        {
            UpdateLocalLandingMarker(landingPos);
        }
    }

    void UpdateDottedTrajectory()
    {
        // Ensure we have enough dots
        while (trajectoryDots.Count < trajectoryPoints.Count)
        {
            GameObject dot;
            Material dotMaterial = null;
            
            if (trajectoryPointPrefab != null)
            {
                dot = Instantiate(trajectoryPointPrefab);
            }
            else
            {
                // Create simple sphere dots if no prefab
                dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.transform.localScale = Vector3.one * 0.2f;
                Destroy(dot.GetComponent<Collider>());
            }
            
            // Pre-create and configure material for transparency
            Renderer renderer = dot.GetComponent<Renderer>();
            if (renderer != null)
            {
                dotMaterial = new Material(renderer.material);
                // Configure material for transparency once
                dotMaterial.SetFloat("_Mode", 3);
                dotMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dotMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dotMaterial.SetInt("_ZWrite", 0);
                dotMaterial.DisableKeyword("_ALPHATEST_ON");
                dotMaterial.EnableKeyword("_ALPHABLEND_ON");
                dotMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                dotMaterial.renderQueue = 3000;
                renderer.material = dotMaterial;
            }
            
            dot.SetActive(false);
            trajectoryDots.Add(dot);
            trajectoryDotMaterials.Add(dotMaterial);
        }

        // Reset active count
        activeDotCount = 0;
        
        // Position dots along trajectory with spacing
        float accumulatedDistance = 0f;
        Vector3 lastDotPos = trajectoryPoints[0];
        
        for (int i = 1; i < trajectoryPoints.Count; i++)
        {
            Vector3 currentPoint = trajectoryPoints[i];
            float segmentDistance = Vector3.Distance(trajectoryPoints[i-1], currentPoint);
            accumulatedDistance += segmentDistance;
            
            if (accumulatedDistance >= trajectoryPointSpacing)
            {
                if (activeDotCount < trajectoryDots.Count)
                {
                    GameObject dot = trajectoryDots[activeDotCount];
                    dot.SetActive(true);
                    dot.transform.position = currentPoint;
                    
                    // Update color with fade - material properties already configured
                    if (activeDotCount < trajectoryDotMaterials.Count && trajectoryDotMaterials[activeDotCount] != null)
                    {
                        Color baseColor = currentThrowType == ThrowType.Short ? shortThrowColor : lobThrowColor;
                        float fadeFactor = 1f - (i / (float)trajectoryPoints.Count) * trajectoryFadeDistance;
                        baseColor.a *= fadeFactor;
                        
                        trajectoryDotMaterials[activeDotCount].color = baseColor;
                    }
                    
                    activeDotCount++;
                    accumulatedDistance = 0f;
                    lastDotPos = currentPoint;
                }
            }
        }
        
        // Hide unused dots
        for (int i = activeDotCount; i < trajectoryDots.Count; i++)
        {
            trajectoryDots[i].SetActive(false);
        }
    }

    void UpdateTrajectoryColors()
    {
        // Update colors of active dots when throw type changes
        for (int i = 0; i < activeDotCount; i++)
        {
            if (i < trajectoryDotMaterials.Count && trajectoryDotMaterials[i] != null)
            {
                Color baseColor = currentThrowType == ThrowType.Short ? shortThrowColor : lobThrowColor;
                float fadeFactor = 1f - (i / (float)activeDotCount) * trajectoryFadeDistance;
                baseColor.a *= fadeFactor;
                trajectoryDotMaterials[i].color = baseColor;
            }
        }
    }

    void UpdateBounceMarkers(List<Vector3> bouncePoints)
    {
        if (!isLocalPlayer) return;
        
        // Hide all existing bounce markers first
        foreach (var marker in bounceMarkers)
        {
            if (marker != null) marker.SetActive(false);
        }
        
        // Ensure we have enough bounce markers
        while (bounceMarkers.Count < bouncePoints.Count)
        {
            GameObject bounceMarker;
            if (landingMarkerPrefab != null)
            {
                bounceMarker = Instantiate(landingMarkerPrefab);
            }
            else
            {
                // Create simple bounce marker if no prefab
                bounceMarker = new GameObject("BounceMarker");
                var spriteRenderer = bounceMarker.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateCircleSprite();
                spriteRenderer.sortingLayerName = landingMarkerLayer;
                spriteRenderer.sortingOrder = 9; // Slightly below landing marker
                bounceMarker.transform.localScale = Vector3.one * 1.5f; // Smaller than landing marker
            }
            
            bounceMarker.SetActive(false);
            bounceMarkers.Add(bounceMarker);
        }
        
        // Position and show bounce markers
        for (int i = 0; i < bouncePoints.Count; i++)
        {
            if (i < bounceMarkers.Count)
            {
                GameObject marker = bounceMarkers[i];
                marker.SetActive(true);
                marker.transform.position = bouncePoints[i] + Vector3.up * 0.05f; // Slightly above ground
                
                // Color bounce markers differently (orange-ish)
                var spriteRenderer = marker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = new Color(1f, 0.7f, 0.3f, 0.7f); // Orange with transparency
                }
            }
        }
    }

    void UpdateLocalLandingMarker(Vector3 position)
    {
        localLandingPosition = position;
        showLocalLandingMarker = true;
        UpdateLandingMarkerVisual(position);
    }

    void UpdateLandingMarkerVisual(Vector3 position)
    {
        if (landingMarker != null && isLocalPlayer)
        {
            landingMarker.SetActive(showLocalLandingMarker);
            landingMarker.transform.position = position + Vector3.up * 0.1f; // Slightly above ground
            
            // Add visual feedback based on throw type
            var spriteRenderer = landingMarker.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                Color markerColor = currentThrowType == ThrowType.Short ? 
                    new Color(1f, 1f, 1f, 0.8f) : new Color(1f, 1f, 0.8f, 0.8f);
                spriteRenderer.color = markerColor;
            }
        }
    }

    void HideTrajectory()
    {
        // Hide all trajectory dots
        foreach (var dot in trajectoryDots)
        {
            if (dot != null)
                dot.SetActive(false);
        }
        activeDotCount = 0;
        
        // Hide landing marker (local only)
        if (isLocalPlayer)
        {
            showLocalLandingMarker = false;
            if (landingMarker != null)
                landingMarker.SetActive(false);
                
            // Hide bounce markers
            foreach (var marker in bounceMarkers)
            {
                if (marker != null) marker.SetActive(false);
            }
        }
    }

    void StartAiming(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || currentBomb == null) return;
        
        if (!currentBomb.IsHeld || currentBomb.Holder != gameObject)
        {
            return;
        }
        
        isAiming = true;
        isHoldingAim = true;
        aimDirection = transform.forward;
        targetAimDirection = transform.forward;
        trajectoryNeedsUpdate = true; // Force initial calculation
    }

    void CancelAiming(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !isAiming) return;
        
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
    }

    void ExecuteThrow(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !isAiming || currentBomb == null) return;
        
        if (!currentBomb.IsHeld || currentBomb.Holder != gameObject)
        {
            return;
        }
        
        Vector3 throwDirection = aimDirection;
        ThrowType throwType = currentThrowType;
        
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
        
        if (animator != null)
            animator.SetTrigger("Throw");
        
        CmdThrowBomb(throwDirection, throwType);
    }

    public ThrowType GetCurrentThrowType()
    {
        return currentThrowType;
    }

    public Vector3 GetAimDirection()
    {
        return aimDirection;
    }
}