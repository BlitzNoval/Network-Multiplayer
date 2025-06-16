using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

[RequireComponent(typeof(PlayerInput))]
public class PlayerBombHandler : NetworkBehaviour
{
    [Header("Trajectory - Underarm Throw")]
    [SerializeField, Range(10,500)] int underarmThrowMaxPoints = 200;
    [SerializeField] private float underarmThrowPointSpacing = 0.4f; // Space between dots
    [SerializeField] private float underarmThrowFadeDistance = 0.7f; // Fade dots over distance
    [SerializeField] private Color underarmThrowColor = Color.white;
    
    [Header("Trajectory - Lob Throw")]
    [SerializeField, Range(10,500)] int lobThrowMaxPoints = 400;
    [SerializeField] private float lobThrowPointSpacing = 0.6f; // Space between dots
    [SerializeField] private float lobThrowFadeDistance = 0.9f; // Fade dots over distance
    [SerializeField] private Color lobThrowColor = new Color(1f, 1f, 0.2f, 0.8f); // Yellow for lob
    
    [Header("Trajectory General")]
    [SerializeField] LayerMask collisionMask;
    [SerializeField] private GameObject trajectoryPointPrefab; // Prefab for dotted line points
    
    [Header("Landing Markers")]
    [SerializeField] private GameObject underarmThrowMarkerPrefab; // Prefab for underarm throw landing marker
    [SerializeField] private GameObject lobThrowMarkerPrefab; // Prefab for lob throw landing marker
    [SerializeField] private GameObject bounceMarkerPrefab; // Prefab for bounce points (shared)
    [SerializeField] private Sprite landingMarkerSprite; // Fallback sprite for landing indicators
    [SerializeField] private string landingMarkerLayer = "UI"; // Layer for landing markers
    
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
    private GameObject underarmThrowMarker;
    private GameObject lobThrowMarker;
    private List<GameObject> bounceMarkers = new List<GameObject>();
    private int activeDotCount = 0;
    
    // Arrow visualization for underarm throw
    private GameObject arrowLine;
    private LineRenderer arrowLineRenderer;
    private GameObject arrowHead;
    
    // Aiming state
    bool isAiming;
    Vector3 aimDirection;
    Vector3 targetAimDirection;
    List<Vector3> trajectoryPoints = new();
    float timeStep = 0.01f; // Smaller timestep for more accurate trajectory calculation
    [SerializeField] float aimSmoothSpeed = 8f;
    
    // Performance optimization for trajectory calculation
    [Header("Performance")]
    [SerializeField] float trajectoryUpdateRate = 10f; // Updates per second
    [SerializeField] float aimDirectionThreshold = 0.05f; // Minimum change to trigger recalculation
    private float lastTrajectoryUpdate = 0f;
    private Vector3 lastCachedAimDirection = Vector3.zero;
    private ThrowType lastCachedThrowType = ThrowType.Underarm;
    private bool trajectoryNeedsUpdate = true;
    
    // Throw type state
    public enum ThrowType { Underarm, Lob }
    [SyncVar(hook = nameof(OnThrowTypeChanged))] ThrowType currentThrowType = ThrowType.Underarm;
    
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
        // Create underarm throw landing marker
        if (underarmThrowMarkerPrefab != null)
        {
            underarmThrowMarker = Instantiate(underarmThrowMarkerPrefab);
            underarmThrowMarker.name = "UnderarmThrowMarker";
            underarmThrowMarker.SetActive(false);
            
            // Set the sprite if provided
            if (landingMarkerSprite != null)
            {
                var spriteRenderer = underarmThrowMarker.GetComponent<SpriteRenderer>();
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
            // Create a simple underarm throw marker if no prefab provided
            underarmThrowMarker = new GameObject("UnderarmThrowMarker");
            var spriteRenderer = underarmThrowMarker.AddComponent<SpriteRenderer>();
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
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.9f); // White for underarm
            underarmThrowMarker.transform.localScale = Vector3.one * 2f; // Standard size
            underarmThrowMarker.SetActive(false);
        }
        
        // Create lob throw landing marker
        if (lobThrowMarkerPrefab != null)
        {
            lobThrowMarker = Instantiate(lobThrowMarkerPrefab);
            lobThrowMarker.name = "LobThrowMarker";
            lobThrowMarker.SetActive(false);
            
            // Set the sprite if provided
            if (landingMarkerSprite != null)
            {
                var spriteRenderer = lobThrowMarker.GetComponent<SpriteRenderer>();
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
            // Create a simple lob landing marker if no prefab provided
            lobThrowMarker = new GameObject("LobThrowMarker");
            var spriteRenderer = lobThrowMarker.AddComponent<SpriteRenderer>();
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
            spriteRenderer.color = new Color(1f, 1f, 0.2f, 0.9f); // Yellow for lob
            lobThrowMarker.transform.localScale = Vector3.one * 2.5f; // Larger size for lob
            lobThrowMarker.SetActive(false);
        }
        
        // Initialize arrow visualization for underarm throw
        InitializeArrowVisualization();
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

    void InitializeArrowVisualization()
    {
        // Create arrow line
        arrowLine = new GameObject("UnderarmArrowLine");
        arrowLineRenderer = arrowLine.AddComponent<LineRenderer>();
        arrowLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        arrowLineRenderer.material.color = underarmThrowColor;
        arrowLineRenderer.startWidth = 0.1f;
        arrowLineRenderer.endWidth = 0.1f;
        arrowLineRenderer.positionCount = 2;
        arrowLineRenderer.useWorldSpace = true;
        arrowLine.SetActive(false);
        
        // Create arrow head (simple triangle)
        arrowHead = new GameObject("UnderarmArrowHead");
        var meshFilter = arrowHead.AddComponent<MeshFilter>();
        var meshRenderer = arrowHead.AddComponent<MeshRenderer>();
        
        // Create arrow head mesh
        Mesh arrowMesh = new Mesh();
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0, 0.2f, 0),      // Top
            new Vector3(-0.15f, -0.2f, 0), // Bottom left
            new Vector3(0.15f, -0.2f, 0)   // Bottom right
        };
        int[] triangles = new int[] { 0, 1, 2 };
        arrowMesh.vertices = vertices;
        arrowMesh.triangles = triangles;
        arrowMesh.RecalculateNormals();
        
        meshFilter.mesh = arrowMesh;
        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material.color = underarmThrowColor;
        arrowHead.SetActive(false);
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
        
        // Clean up landing markers
        if (underarmThrowMarker != null) Destroy(underarmThrowMarker);
        if (lobThrowMarker != null) Destroy(lobThrowMarker);
        
        // Clean up arrow visualization
        if (arrowLine != null) Destroy(arrowLine);
        if (arrowHead != null) Destroy(arrowHead);
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
        currentThrowType = currentThrowType == ThrowType.Underarm ? ThrowType.Lob : ThrowType.Underarm;
    }

    [Command]
    void CmdThrowBomb(Vector3 direction, ThrowType throwType)
    {
        if (currentBomb && currentBomb.Holder == gameObject && currentBomb.CurrentTimer > 1.0f)
        {
            if (playerAnimator != null)
                playerAnimator.OnBombThrow();
            
            bool useUnderarmThrow = throwType == ThrowType.Underarm;
            currentBomb.ThrowBomb(direction, useUnderarmThrow);
            
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
        float speed = currentThrowType == ThrowType.Underarm ? currentBomb.NormalThrowSpeed : currentBomb.LobThrowSpeed;
        float upward = currentThrowType == ThrowType.Underarm ? currentBomb.NormalThrowUpward : currentBomb.LobThrowUpward;
        
        // Get trajectory settings based on throw type
        int maxPoints = currentThrowType == ThrowType.Underarm ? underarmThrowMaxPoints : lobThrowMaxPoints;
        float pointSpacing = currentThrowType == ThrowType.Underarm ? underarmThrowPointSpacing : lobThrowPointSpacing;
        float fadeDistance = currentThrowType == ThrowType.Underarm ? underarmThrowFadeDistance : lobThrowFadeDistance;

        Vector3 startPos = origin.position;
        
        // Calculate initial velocity using the exact same physics as the bomb
        // The bomb's mass gets multiplied by flightMassMultiplier during throw (line 350 in Bomb.cs)
        // ForceMode.Impulse applies force/mass as velocity change, so we need to account for the mass change
        Vector3 force = aimDirection.normalized * speed + Vector3.up * upward;
        float effectiveMass = 1f * currentBomb.FlightMassMultiplier; // Match bomb's mass calculation
        Vector3 velocity = force / effectiveMass; // ForceMode.Impulse: velocity = force / mass
        
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
            
            // Use a more generous raycast distance for better collision detection
            Vector3 rayDirection = (next - lastPos).normalized;
            float rayDistance = (next - lastPos).magnitude;
            
            if (rayDistance > 0.001f)
            {
                // First try the exact ray
                bool hitSomething = Physics.Raycast(lastPos, rayDirection, out RaycastHit hit, rayDistance + 0.1f, collisionMask);
                
                // If no hit, try a slightly downward ray to catch ground better (especially for lob throws)
                if (!hitSomething && currentVelocity.y < 0)
                {
                    Vector3 downwardRay = rayDirection + Vector3.down * 0.3f;
                    hitSomething = Physics.Raycast(lastPos, downwardRay.normalized, out hit, rayDistance * 1.5f, collisionMask);
                }
                
                if (hitSomething)
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
            }
            
            // Check if we've fallen below a reasonable floor level (like -10) to prevent infinite falling
            if (next.y < -10f)
            {
                // Try to find the last valid ground position by raycasting downward
                if (Physics.Raycast(new Vector3(next.x, 50f, next.z), Vector3.down, out RaycastHit groundHit, 100f, collisionMask))
                {
                    landingPos = groundHit.point;
                    foundLanding = true;
                }
                else
                {
                    landingPos = next;
                    foundLanding = true;
                }
                break;
            }
            
            trajectoryPoints.Add(next);
            lastPos = next;
            currentPos = next;
        }
        
        // Update bounce markers
        UpdateBounceMarkers(bouncePoints);

        // Update trajectory visualization based on throw type
        if (currentThrowType == ThrowType.Underarm)
        {
            UpdateArrowTrajectory();
            HideDottedTrajectory(); // Hide dots when showing arrow
        }
        else
        {
            UpdateDottedTrajectory();
            HideArrowTrajectory(); // Hide arrow when showing dots
        }
        
        // Update landing marker (local only) - always show if we have a valid landing position
        if (isLocalPlayer)
        {
            if (foundLanding)
            {
                UpdateLocalLandingMarker(landingPos);
            }
            else
            {
                // If no landing found, hide the landing marker but keep trajectory visible
                HideLandingMarkers();
            }
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
        
        // Only show dots if we have trajectory points
        if (trajectoryPoints.Count == 0)
        {
            Debug.LogWarning("No trajectory points to display");
            return;
        }
        
        // Get current throw type settings
        float currentPointSpacing = currentThrowType == ThrowType.Underarm ? underarmThrowPointSpacing : lobThrowPointSpacing;
        float currentFadeDistance = currentThrowType == ThrowType.Underarm ? underarmThrowFadeDistance : lobThrowFadeDistance;
        Color currentColor = currentThrowType == ThrowType.Underarm ? underarmThrowColor : lobThrowColor;
        
        // Position dots along trajectory with spacing
        float accumulatedDistance = 0f;
        Vector3 lastDotPos = trajectoryPoints[0];
        
        for (int i = 1; i < trajectoryPoints.Count; i++)
        {
            Vector3 currentPoint = trajectoryPoints[i];
            float segmentDistance = Vector3.Distance(trajectoryPoints[i-1], currentPoint);
            accumulatedDistance += segmentDistance;
            
            if (accumulatedDistance >= currentPointSpacing)
            {
                if (activeDotCount < trajectoryDots.Count)
                {
                    GameObject dot = trajectoryDots[activeDotCount];
                    dot.SetActive(true);
                    dot.transform.position = currentPoint;
                    
                    // Update color with fade - material properties already configured
                    if (activeDotCount < trajectoryDotMaterials.Count && trajectoryDotMaterials[activeDotCount] != null)
                    {
                        Color baseColor = currentColor;
                        float fadeFactor = 1f - (i / (float)trajectoryPoints.Count) * currentFadeDistance;
                        baseColor.a *= Mathf.Max(0.2f, fadeFactor); // Ensure minimum visibility
                        
                        trajectoryDotMaterials[activeDotCount].color = baseColor;
                    }
                    
                    activeDotCount++;
                    accumulatedDistance = 0f;
                    lastDotPos = currentPoint;
                }
                else
                {
                    // If we need more dots, break and log it
                    // Need more trajectory dots - increase maxPoints if needed
                    break;
                }
            }
        }
        
        // Trajectory dots updated successfully
        
        // Hide unused dots
        for (int i = activeDotCount; i < trajectoryDots.Count; i++)
        {
            trajectoryDots[i].SetActive(false);
        }
    }

    void UpdateArrowTrajectory()
    {
        if (trajectoryPoints.Count < 2 || arrowLineRenderer == null) return;
        
        // For underarm throw, draw a straight arrow from start to landing point
        Vector3 startPos = trajectoryPoints[0];
        Vector3 endPos = trajectoryPoints[trajectoryPoints.Count - 1];
        
        // Set up the line renderer
        arrowLineRenderer.SetPosition(0, startPos);
        arrowLineRenderer.SetPosition(1, endPos);
        arrowLineRenderer.material.color = underarmThrowColor;
        arrowLine.SetActive(true);
        
        // Position and orient the arrow head at the end
        if (arrowHead != null)
        {
            arrowHead.transform.position = endPos;
            
            // Point the arrow head in the direction of the throw
            Vector3 direction = (endPos - startPos).normalized;
            if (direction != Vector3.zero)
            {
                arrowHead.transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
            }
            
            arrowHead.SetActive(true);
        }
    }

    void HideDottedTrajectory()
    {
        // Hide all trajectory dots
        foreach (var dot in trajectoryDots)
        {
            if (dot != null)
                dot.SetActive(false);
        }
        activeDotCount = 0;
    }

    void HideArrowTrajectory()
    {
        if (arrowLine != null) arrowLine.SetActive(false);
        if (arrowHead != null) arrowHead.SetActive(false);
    }

    void UpdateTrajectoryColors()
    {
        // Get current throw type settings
        Color currentColor = currentThrowType == ThrowType.Underarm ? underarmThrowColor : lobThrowColor;
        float currentFadeDistance = currentThrowType == ThrowType.Underarm ? underarmThrowFadeDistance : lobThrowFadeDistance;
        
        // Update colors of active dots when throw type changes
        for (int i = 0; i < activeDotCount; i++)
        {
            if (i < trajectoryDotMaterials.Count && trajectoryDotMaterials[i] != null)
            {
                Color baseColor = currentColor;
                float fadeFactor = 1f - (i / (float)activeDotCount) * currentFadeDistance;
                baseColor.a *= Mathf.Max(0.2f, fadeFactor);
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
            
            if (bounceMarkerPrefab != null)
            {
                bounceMarker = Instantiate(bounceMarkerPrefab);
                bounceMarker.name = $"BounceMarker_{bounceMarkers.Count}";
            }
            else
            {
                // Create simple bounce marker if no prefab
                bounceMarker = new GameObject($"BounceMarker_{bounceMarkers.Count}");
                var spriteRenderer = bounceMarker.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateCircleSprite();
                spriteRenderer.sortingLayerName = landingMarkerLayer;
                spriteRenderer.sortingOrder = 9; // Slightly below landing marker
                bounceMarker.transform.localScale = Vector3.one * 1.5f; // Smaller than landing marker
                // Orange color for bounce markers
                spriteRenderer.color = new Color(1f, 0.7f, 0.3f, 0.8f);
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
    
    void HideLandingMarkers()
    {
        showLocalLandingMarker = false;
        if (underarmThrowMarker != null)
            underarmThrowMarker.SetActive(false);
        if (lobThrowMarker != null)
            lobThrowMarker.SetActive(false);
    }

    void UpdateLandingMarkerVisual(Vector3 position)
    {
        if (!isLocalPlayer) return;
        
        // Get the appropriate marker based on throw type
        GameObject currentMarker = currentThrowType == ThrowType.Underarm ? underarmThrowMarker : lobThrowMarker;
        GameObject otherMarker = currentThrowType == ThrowType.Underarm ? lobThrowMarker : underarmThrowMarker;
        
        // Update landing marker visual for current throw type
        
        // Always hide the other marker first
        if (otherMarker != null)
        {
            otherMarker.SetActive(false);
        }
        
        // Show the current marker if we should show it
        if (currentMarker != null)
        {
            currentMarker.SetActive(showLocalLandingMarker);
            if (showLocalLandingMarker)
            {
                currentMarker.transform.position = position + Vector3.up * 0.1f; // Slightly above ground
                
                // Add visual feedback based on throw type (if using fallback marker creation)
                var spriteRenderer = currentMarker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    // Set colors to distinguish between throw types
                    Color markerColor = currentThrowType == ThrowType.Underarm ? 
                        new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 0.2f, 0.9f); // White for underarm, yellow for lob
                    spriteRenderer.color = markerColor;
                }
                
                // Landing marker positioned successfully
            }
        }
        else
        {
            Debug.LogWarning($"No landing marker found for throw type: {currentThrowType}");
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
        
        // Hide arrow trajectory
        HideArrowTrajectory();
        
        // Hide landing markers (local only)
        if (isLocalPlayer)
        {
            HideLandingMarkers();
                
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