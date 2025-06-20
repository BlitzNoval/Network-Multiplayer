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
    [SerializeField] private float underarmThrowPointSpacing = 0.4f;
    [SerializeField] private float underarmThrowFadeDistance = 0.7f;
    [SerializeField] private Color underarmThrowColor = Color.white;
    
    [Header("Trajectory - Lob Throw")]
    [SerializeField, Range(10,500)] int lobThrowMaxPoints = 400;
    [SerializeField] private float lobThrowPointSpacing = 0.6f;
    [SerializeField] private float lobThrowFadeDistance = 0.9f;
    [SerializeField] private Color lobThrowColor = new Color(1f, 1f, 0.2f, 0.8f);
    
    [Header("Trajectory General")]
    [SerializeField] LayerMask collisionMask;
    [SerializeField] private GameObject trajectoryPointPrefab;
    [SerializeField] private GameObject shortThrowPrefab;
    
    [Header("Landing Markers")]
    [SerializeField] private GameObject underarmThrowMarkerPrefab;
    [SerializeField] private GameObject lobThrowMarkerPrefab;
    [SerializeField] private GameObject bounceMarkerPrefab;
    [SerializeField] private Sprite landingMarkerSprite;
    [SerializeField] private string landingMarkerLayer = "UI";
    
    [Header("Aiming")]
    [SerializeField] float mouseSensitivity = 1.5f;
    [SerializeField] float controllerSensitivity = 3f;
    [SerializeField] float aimingRange = 10f;
    
    [SerializeField] float throwWindUp = 0.10f;

    [Header("Hand Points")]
    [SerializeField] public Transform leftHandPoint;
    [SerializeField] public Transform rightHandPoint;
    
    Bomb currentBomb;
    public Bomb CurrentBomb => currentBomb;
    Camera playerCamera;
    
    private List<GameObject> trajectoryDots = new List<GameObject>();
    private List<Material> trajectoryDotMaterials = new List<Material>();
    private GameObject underarmThrowMarker;
    private GameObject lobThrowMarker;
    private List<GameObject> bounceMarkers = new List<GameObject>();
    private int activeDotCount = 0;
    
    private List<GameObject> arrowDots = new List<GameObject>();
    private List<Material> arrowDotMaterials = new List<Material>();
    private int activeArrowCount = 0;
    
    bool isAiming;
    Vector3 aimDirection;
    Vector3 targetAimDirection;
    List<Vector3> trajectoryPoints = new();
    float timeStep = 0.01f;
    [SerializeField] float aimSmoothSpeed = 8f;
    
    [Header("Performance")]
    [SerializeField] float trajectoryUpdateRate = 10f;
    [SerializeField] float aimDirectionThreshold = 0.05f;
    private float lastTrajectoryUpdate = 0f;
    private Vector3 lastCachedAimDirection = Vector3.zero;
    private ThrowType lastCachedThrowType = ThrowType.Underarm;
    private bool trajectoryNeedsUpdate = true;
    
    public enum ThrowType { Underarm, Lob }
    [SyncVar(hook = nameof(OnThrowTypeChanged))] ThrowType currentThrowType = ThrowType.Underarm;
    
    Vector3 localLandingPosition = Vector3.zero;
    bool showLocalLandingMarker = false;
    
    InputAction toggleThrowTypesAct, aimAct, holdAimAct, cancelAimAct;
    PlayerInput playerInput;
    bool inputSubscribed;
    Vector2 currentAimInput;
    bool isHoldingAim;
    
    private PlayerAnimator playerAnimator;
    private Animator animator;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerCamera = Camera.main;
        
        playerAnimator = GetComponent<PlayerAnimator>();
        animator = GetComponent<Animator>();
        
        InitializeTrajectoryVisualization();

        if (rightHandPoint == null)
        {
            Debug.Log("RightHandPoint not set in inspector. Using transform.Find.");
        }
        if (leftHandPoint == null)
        {
            Debug.Log("LeftHandPoint not set in inspector. Using transform.Find.");
        }
    }

    void InitializeTrajectoryVisualization()
    {
        if (underarmThrowMarkerPrefab != null)
        {
            underarmThrowMarker = Instantiate(underarmThrowMarkerPrefab);
            underarmThrowMarker.name = "UnderarmThrowMarker";
            underarmThrowMarker.SetActive(false);
            
            if (landingMarkerSprite != null)
            {
                var spriteRenderer = underarmThrowMarker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = landingMarkerSprite;
                    spriteRenderer.sortingLayerName = landingMarkerLayer;
                    spriteRenderer.sortingOrder = 10;
                }
            }
        }
        else
        {
            underarmThrowMarker = new GameObject("UnderarmThrowMarker");
            var spriteRenderer = underarmThrowMarker.AddComponent<SpriteRenderer>();
            if (landingMarkerSprite != null)
            {
                spriteRenderer.sprite = landingMarkerSprite;
            }
            else
            {
                spriteRenderer.sprite = CreateCircleSprite();
            }
            spriteRenderer.sortingLayerName = landingMarkerLayer;
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.9f);
            underarmThrowMarker.transform.localScale = Vector3.one * 2f;
            underarmThrowMarker.SetActive(false);
        }
        
        if (lobThrowMarkerPrefab != null)
        {
            lobThrowMarker = Instantiate(lobThrowMarkerPrefab);
            lobThrowMarker.name = "LobThrowMarker";
            lobThrowMarker.SetActive(false);
            
            if (landingMarkerSprite != null)
            {
                var spriteRenderer = lobThrowMarker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = landingMarkerSprite;
                    spriteRenderer.sortingLayerName = landingMarkerLayer;
                    spriteRenderer.sortingOrder = 10;
                }
            }
        }
        else
        {
            lobThrowMarker = new GameObject("LobThrowMarker");
            var spriteRenderer = lobThrowMarker.AddComponent<SpriteRenderer>();
            if (landingMarkerSprite != null)
            {
                spriteRenderer.sprite = landingMarkerSprite;
            }
            else
            {
                spriteRenderer.sprite = CreateCircleSprite();
            }
            spriteRenderer.sortingLayerName = landingMarkerLayer;
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.color = new Color(1f, 1f, 0.2f, 0.9f);
            lobThrowMarker.transform.localScale = Vector3.one * 2.5f;
            lobThrowMarker.SetActive(false);
        }
        
        InitializeArrowVisualization();
    }

    Sprite CreateCircleSprite()
    {
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
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    void OnDestroy()
    {
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
        
        foreach (var marker in bounceMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        bounceMarkers.Clear();
        
        if (underarmThrowMarker != null) Destroy(underarmThrowMarker);
        if (lobThrowMarker != null) Destroy(lobThrowMarker);
        
        foreach (var arrow in arrowDots)
        {
            if (arrow != null) Destroy(arrow);
        }
        arrowDots.Clear();
        
        foreach (var material in arrowDotMaterials)
        {
            if (material != null) Destroy(material);
        }
        arrowDotMaterials.Clear();
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
        // Note: PlayerAnimator.UpdateActiveHand() handles animation state
        if (currentBomb != null)
        {
            Transform holdPoint = currentBomb.IsOnRight ? rightHandPoint : leftHandPoint;
            if (holdPoint != null)
            {
                currentBomb.transform.SetParent(holdPoint);
                currentBomb.transform.localPosition = Vector3.zero;
                currentBomb.transform.localRotation = Quaternion.identity;
                Debug.Log("Bomb parented to " + holdPoint.name + " on " + gameObject.name);
            }
            else
            {
                Debug.LogError("Hand point not set for " + (currentBomb.IsOnRight ? "right" : "left") + " hand");
            }
        }
    }

    public void ClearBomb()
    {
        if (currentBomb != null)
        {
            currentBomb.transform.SetParent(null);
        }
        currentBomb = null;
        // Note: PlayerAnimator.UpdateActiveHand() handles animation state
        if (isAiming)
        {
            HideTrajectory();
            isAiming = false;
        }
    }


    void ToggleThrowTypes(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || currentBomb == null) return;
        CmdSwapHands();
    }

    void OnThrowTypeChanged(ThrowType oldType, ThrowType newType)
    {
        if (isAiming)
        {
            trajectoryNeedsUpdate = true;
            UpdateTrajectoryColors();
        }
    }

    [Command]
    void CmdSwapHands()
    {
        if (currentBomb != null && currentBomb.IsHeld)
        {
            currentBomb.SwapHoldPoint();
            Debug.Log($"CmdSwapHands: Swapped bomb to {(currentBomb.IsOnRight ? "right" : "left")} hand", this);
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

        Transform origin;
        if (currentBomb.IsOnRight)
        {
            origin = rightHandPoint != null ? rightHandPoint : transform.Find("RightHoldPoint");
        }
        else
        {
            origin = leftHandPoint != null ? leftHandPoint : transform.Find("LeftHoldPoint");
        }
        if (origin == null)
        {
            Debug.LogError("Hand point not found for " + (currentBomb.IsOnRight ? "right" : "left") + " hand!");
            HideTrajectory();
            return;
        }

        float speed = currentThrowType == ThrowType.Underarm ? currentBomb.NormalThrowSpeed : currentBomb.LobThrowSpeed;
        float upward = currentThrowType == ThrowType.Underarm ? currentBomb.NormalThrowUpward : currentBomb.LobThrowUpward;
        
        int maxPoints = currentThrowType == ThrowType.Underarm ? underarmThrowMaxPoints : lobThrowMaxPoints;
        float pointSpacing = currentThrowType == ThrowType.Underarm ? underarmThrowPointSpacing : lobThrowPointSpacing;
        float fadeDistance = currentThrowType == ThrowType.Underarm ? underarmThrowFadeDistance : lobThrowFadeDistance;

        Vector3 startPos = origin.position;
        
        Vector3 force = aimDirection.normalized * speed + Vector3.up * upward;
        float effectiveMass = 1f * currentBomb.FlightMassMultiplier;
        Vector3 velocity = force / effectiveMass;
        
        Vector3 lastPos = startPos;
        Vector3 landingPos = Vector3.zero;
        bool foundLanding = false;

        trajectoryPoints.Add(lastPos);

        float t = 0f;
        Vector3 currentVelocity = velocity;
        Vector3 currentPos = startPos;
        List<Vector3> bouncePoints = new List<Vector3>();
        int maxBounces = 3;
        int bounceCount = 0;
        
        for (int i = 0; i < maxPoints; ++i)
        {
            t += timeStep;
            
            currentVelocity += Physics.gravity * timeStep;
            
            Vector3 next = currentPos + currentVelocity * timeStep;
            
            Vector3 rayDirection = (next - lastPos).normalized;
            float rayDistance = (next - lastPos).magnitude;
            
            if (rayDistance > 0.001f)
            {
                bool hitSomething = Physics.Raycast(lastPos, rayDirection, out RaycastHit hit, rayDistance + 0.1f, collisionMask);
                
                if (!hitSomething && currentVelocity.y < 0)
                {
                    Vector3 downwardRay = rayDirection + Vector3.down * 0.3f;
                    hitSomething = Physics.Raycast(lastPos, downwardRay.normalized, out hit, rayDistance * 1.5f, collisionMask);
                }
                
                if (hitSomething)
                {
                    trajectoryPoints.Add(hit.point);
                    
                    Vector3 surfaceNormal = hit.normal;
                    float dotProduct = Vector3.Dot(currentVelocity.normalized, -surfaceNormal);
                    
                    if (dotProduct < 0.7f && bounceCount < maxBounces && currentVelocity.magnitude > 5f)
                    {
                        bouncePoints.Add(hit.point);
                        bounceCount++;
                        
                        Vector3 bounceVelocity = Vector3.Reflect(currentVelocity, surfaceNormal);
                        bounceVelocity *= 0.6f;
                        
                        currentVelocity = bounceVelocity;
                        currentPos = hit.point + surfaceNormal * 0.01f;
                        lastPos = currentPos;
                        
                        continue;
                    }
                    else
                    {
                        landingPos = hit.point;
                        foundLanding = true;
                        break;
                    }
                }
            }
            
            if (next.y < -10f)
            {
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
        
        UpdateBounceMarkers(bouncePoints);

        if (currentThrowType == ThrowType.Underarm)
        {
            UpdateArrowTrajectory();
            HideDottedTrajectory();
        }
        else
        {
            UpdateDottedTrajectory();
            HideArrowTrajectory();
        }
        
        if (isLocalPlayer)
        {
            if (foundLanding)
            {
                UpdateLocalLandingMarker(landingPos);
            }
            else
            {
                HideLandingMarkers();
            }
        }
    }

    void UpdateDottedTrajectory()
    {
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
                dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.transform.localScale = Vector3.one * 0.2f;
                Destroy(dot.GetComponent<Collider>());
            }
            
            Renderer renderer = dot.GetComponent<Renderer>();
            if (renderer != null)
            {
                dotMaterial = new Material(renderer.material);
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

        activeDotCount = 0;
        
        if (trajectoryPoints.Count == 0)
        {
            Debug.LogWarning("No trajectory points to display");
            return;
        }
        
        float currentPointSpacing = currentThrowType == ThrowType.Underarm ? underarmThrowPointSpacing : lobThrowPointSpacing;
        float currentFadeDistance = currentThrowType == ThrowType.Underarm ? underarmThrowFadeDistance : lobThrowFadeDistance;
        Color currentColor = currentThrowType == ThrowType.Underarm ? underarmThrowColor : lobThrowColor;
        
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
                    
                    if (activeDotCount < trajectoryDotMaterials.Count && trajectoryDotMaterials[activeDotCount] != null)
                    {
                        Color baseColor = currentColor;
                        float fadeFactor = 1f - (i / (float)trajectoryPoints.Count) * currentFadeDistance;
                        baseColor.a *= Mathf.Max(0.2f, fadeFactor);
                        
                        trajectoryDotMaterials[activeDotCount].color = baseColor;
                    }
                    
                    activeDotCount++;
                    accumulatedDistance = 0f;
                    lastDotPos = currentPoint;
                }
                else
                {
                    break;
                }
            }
        }
        
        for (int i = activeDotCount; i < trajectoryDots.Count; i++)
        {
            trajectoryDots[i].SetActive(false);
        }
    }

    void UpdateArrowTrajectory()
    {
        while (arrowDots.Count < trajectoryPoints.Count)
        {
            GameObject arrow;
            Material arrowMaterial = null;
            
            if (shortThrowPrefab != null)
            {
                arrow = Instantiate(shortThrowPrefab);
            }
            else
            {
                arrow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                arrow.transform.localScale = Vector3.one * 0.2f;
                Destroy(arrow.GetComponent<Collider>());
            }
            
            Renderer renderer = arrow.GetComponent<Renderer>();
            if (renderer != null)
            {
                arrowMaterial = new Material(renderer.material);
                arrowMaterial.SetFloat("_Mode", 3);
                arrowMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                arrowMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                arrowMaterial.SetInt("_ZWrite", 0);
                arrowMaterial.DisableKeyword("_ALPHATEST_ON");
                arrowMaterial.EnableKeyword("_ALPHABLEND_ON");
                arrowMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                arrowMaterial.renderQueue = 3000;
                renderer.material = arrowMaterial;
            }
            
            arrow.SetActive(false);
            arrowDots.Add(arrow);
            arrowDotMaterials.Add(arrowMaterial);
        }

        activeArrowCount = 0;
        
        if (trajectoryPoints.Count == 0)
        {
            return;
        }
        
        float currentPointSpacing = underarmThrowPointSpacing;
        float currentFadeDistance = underarmThrowFadeDistance;
        Color currentColor = underarmThrowColor;
        
        float accumulatedDistance = 0f;
        Vector3 lastArrowPos = trajectoryPoints[0];
        Vector3 lastDirection = Vector3.forward;
        
        for (int i = 1; i < trajectoryPoints.Count; i++)
        {
            Vector3 currentPoint = trajectoryPoints[i];
            float segmentDistance = Vector3.Distance(trajectoryPoints[i-1], currentPoint);
            accumulatedDistance += segmentDistance;
            
            if (accumulatedDistance >= currentPointSpacing)
            {
                if (activeArrowCount < arrowDots.Count)
                {
                    GameObject arrow = arrowDots[activeArrowCount];
                    arrow.SetActive(true);
                    arrow.transform.position = currentPoint;
                    
                    if (activeArrowCount < arrowDotMaterials.Count && arrowDotMaterials[activeArrowCount] != null)
                    {
                        Color baseColor = currentColor;
                        float fadeFactor = 1f - (i / (float)trajectoryPoints.Count) * currentFadeDistance;
                        baseColor.a *= Mathf.Max(0.2f, fadeFactor);
                        
                        arrowDotMaterials[activeArrowCount].color = baseColor;
                    }
                    
                    activeArrowCount++;
                    accumulatedDistance = 0f;
                    lastArrowPos = currentPoint;
                }
                else
                {
                    break;
                }
            }
        }
        
        for (int i = activeArrowCount; i < arrowDots.Count; i++)
        {
            arrowDots[i].SetActive(false);
        }
    }

    void HideDottedTrajectory()
    {
        foreach (var dot in trajectoryDots)
        {
            if (dot != null)
                dot.SetActive(false);
        }
        activeDotCount = 0;
    }

    void HideArrowTrajectory()
    {
        foreach (var arrow in arrowDots)
        {
            if (arrow != null)
                arrow.SetActive(false);
        }
        activeArrowCount = 0;
    }

    void UpdateTrajectoryColors()
    {
        Color currentColor = currentThrowType == ThrowType.Underarm ? underarmThrowColor : lobThrowColor;
        float currentFadeDistance = currentThrowType == ThrowType.Underarm ? underarmThrowFadeDistance : lobThrowFadeDistance;
        
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
        
        foreach (var marker in bounceMarkers)
        {
            if (marker != null) marker.SetActive(false);
        }
        
        if (currentThrowType == ThrowType.Underarm) return;
        
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
                bounceMarker = new GameObject($"BounceMarker_{bounceMarkers.Count}");
                var spriteRenderer = bounceMarker.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateCircleSprite();
                spriteRenderer.sortingLayerName = landingMarkerLayer;
                spriteRenderer.sortingOrder = 9;
                bounceMarker.transform.localScale = Vector3.one * 1.5f;
                spriteRenderer.color = new Color(1f, 0.7f, 0.3f, 0.8f);
            }
            
            bounceMarker.SetActive(false);
            bounceMarkers.Add(bounceMarker);
        }
        
        for (int i = 0; i < bouncePoints.Count; i++)
        {
            if (i < bounceMarkers.Count)
            {
                GameObject marker = bounceMarkers[i];
                marker.SetActive(true);
                marker.transform.position = bouncePoints[i] + Vector3.up * 0.05f;
                
                var spriteRenderer = marker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = currentThrowType == ThrowType.Underarm 
                        ? new Color(1f, 1f, 1f, 0.8f) 
                        : new Color(1f, 0.7f, 0.3f, 0.7f);
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
        
        GameObject currentMarker = currentThrowType == ThrowType.Underarm ? underarmThrowMarker : lobThrowMarker;
        GameObject otherMarker = currentThrowType == ThrowType.Underarm ? lobThrowMarker : underarmThrowMarker;
        
        if (otherMarker != null)
        {
            otherMarker.SetActive(false);
        }
        
        if (currentMarker != null)
        {
            currentMarker.SetActive(showLocalLandingMarker);
            if (showLocalLandingMarker)
            {
                currentMarker.transform.position = position + Vector3.up * 0.1f;
                
                var spriteRenderer = currentMarker.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Color markerColor = currentThrowType == ThrowType.Underarm 
                        ? new Color(1f, 1f, 1f, 0.9f) 
                        : new Color(1f, 1f, 0.2f, 0.9f);
                    spriteRenderer.color = markerColor;
                }
            }
        }
        else
        {
            Debug.LogWarning($"No landing marker found for throw type: {currentThrowType}");
        }
    }

    void HideTrajectory()
    {
        foreach (var dot in trajectoryDots)
        {
            if (dot != null)
                dot.SetActive(false);
        }
        activeDotCount = 0;
        
        HideArrowTrajectory();
        
        if (isLocalPlayer)
        {
            HideLandingMarkers();
                
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
        trajectoryNeedsUpdate = true;
    }

    void CancelAiming(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !isAiming) return;
        
        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
    }

    void ExecuteThrow(InputAction.CallbackContext ctx)
    {
        if (!isLocalPlayer || !isAiming || currentBomb == null) return;
        if (!currentBomb.IsHeld || currentBomb.Holder != gameObject) return;

        // Store the throw parameters for when animation completes
        Vector3 throwDirection = aimDirection;
        ThrowType throwType = currentThrowType;

        // Subscribe to animation completion callback
        if (playerAnimator != null)
        {
            playerAnimator.OnThrowAnimationComplete = () => {
                Debug.Log("Animation completed, executing server throw", this);
                CmdStartServerThrow(throwDirection, throwType);
                playerAnimator.OnThrowAnimationComplete = null; // Clear callback
            };
            
            playerAnimator.PlayThrowLocal();
        }

        isAiming = false;
        isHoldingAim = false;
        HideTrajectory();
    }

    [Command]
    void CmdStartServerThrow(Vector3 dir, ThrowType tType)
    {
        if (currentBomb == null || currentBomb.Holder != gameObject) return;
        if (currentBomb.CurrentTimer <= 1f) return;

        StartCoroutine(ServerThrowAfterAnim(dir, tType));
    }

    IEnumerator ServerThrowAfterAnim(Vector3 dir, ThrowType tType)
    {
        // No delay needed - animation completion is already timed correctly
        bool underarm = tType == ThrowType.Underarm;
        currentBomb?.ThrowBomb(dir, underarm);
        yield break;
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