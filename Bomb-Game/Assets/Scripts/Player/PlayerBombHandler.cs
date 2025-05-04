using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer), typeof(PlayerInput))]
public class PlayerBombHandler : MonoBehaviour

{
        private Bomb currentBomb;
    
    // Trajectory visualization
    private LineRenderer trajectoryLine;
    private bool isAiming;
    private List<Vector3> trajectoryPoints = new List<Vector3>();
    
    // Input actions
    private InputAction swapAction;
    private InputAction throwAction;
    
    [Header("Trajectory Settings")]
    [SerializeField, Range(10, 300)] private int maxPoints = 100;
    [SerializeField] private LayerMask collisionMask;
    
    private float timeStep;
    
    // Public accessors
    public bool IsAiming => isAiming;
    public Vector3[] TrajectoryPoints => trajectoryPoints.ToArray();
    public Bomb CurrentBomb => currentBomb; // Added property

    void Awake()
    {
        trajectoryLine = GetComponent<LineRenderer>();
        trajectoryLine.positionCount = 0;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.1f;
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        
        var playerInput = GetComponent<PlayerInput>();
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        
        swapAction = playerInput.actions["SwapBomb"];
        throwAction = playerInput.actions["Throw"];
        
        timeStep = Time.fixedDeltaTime;
    }


    void OnEnable()
    {
        // Subscribe to input events
        swapAction.performed += OnSwapBombPerformed;
        throwAction.started += OnThrowStarted;
        throwAction.canceled += OnThrowCanceled;
    }

    void OnDisable()
    {
        // Unsubscribe from input events
        swapAction.performed -= OnSwapBombPerformed;
        throwAction.started -= OnThrowStarted;
        throwAction.canceled -= OnThrowCanceled;
    }

    void Start()
    {
        // Register with GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(gameObject);
        }
    }

    void OnDestroy()
    {
        // Unregister from GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(gameObject);
        }
    }

    public void SetBomb(Bomb bomb)
    {
        currentBomb = bomb;
    }

    public void ClearBomb()
    {
        currentBomb = null;
        isAiming = false;
        HideTrajectory();
    }

    private void OnSwapBombPerformed(InputAction.CallbackContext context)
    {
        if (currentBomb != null)
        {
            currentBomb.SwapHoldPoint();
        }
    }

    private void OnThrowStarted(InputAction.CallbackContext context)
    {
        StartAiming();
    }

    private void OnThrowCanceled(InputAction.CallbackContext context)
    {
        ThrowBomb();
    }

    private void StartAiming()
    {
        if (currentBomb == null || isAiming) return;
        
        isAiming = true;
        StartCoroutine(AimLoop());
    }

    private IEnumerator AimLoop()
    {
        while (isAiming)
        {
            SimulateTrajectory();
            yield return new WaitForSeconds(timeStep);
        }
    }

    private void ThrowBomb()
    {
        if (!isAiming || currentBomb == null) return;
        
        currentBomb.ThrowBomb();
        isAiming = false;
        HideTrajectory();
    }

    void Update()
    {
        if (currentBomb == null && isAiming)
        {
            isAiming = false;
            HideTrajectory();
        }
    }

    private void SimulateTrajectory()
    {
        trajectoryPoints.Clear();
        if (currentBomb == null) return;

        string holdPointName = currentBomb.IsOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        Transform holdPoint = transform.Find(holdPointName);
        if (holdPoint == null) return;

        Vector3 startPos = holdPoint.position;
        float throwSpeed = currentBomb.IsOnRight ? currentBomb.NormalThrowSpeed : currentBomb.LobThrowSpeed;
        float upwardForce = currentBomb.IsOnRight ? currentBomb.NormalThrowUpward : currentBomb.LobThrowUpward;

        Vector3 initialVelocity = holdPoint.forward * throwSpeed + Vector3.up * upwardForce;
        trajectoryLine.startColor = currentBomb.IsOnRight ? Color.blue : Color.yellow;
        trajectoryLine.endColor = trajectoryLine.startColor;

        Vector3 lastPos = startPos;
        trajectoryPoints.Add(lastPos);
        float time = 0f;

        for (int i = 0; i < maxPoints; i++)
        {
            time += timeStep;
            Vector3 newPos = startPos + initialVelocity * time + 0.5f * Physics.gravity * time * time;
            Vector3 direction = newPos - lastPos;
            float distance = direction.magnitude;

            if (distance > 0f && Physics.Raycast(lastPos, direction.normalized, out RaycastHit hit, distance, collisionMask))
            {
                if (hit.collider.CompareTag("Map"))
                {
                    trajectoryPoints.Add(hit.point);
                    break;
                }
            }

            trajectoryPoints.Add(newPos);
            lastPos = newPos;

            // Early exit if projectile is clearly descending
            if ((initialVelocity + Physics.gravity * time).y < 0f && newPos.y <= startPos.y)
            {
                break;
            }
        }

        trajectoryLine.positionCount = trajectoryPoints.Count;
        trajectoryLine.SetPositions(trajectoryPoints.ToArray());
    }

    private void HideTrajectory()
    {
        trajectoryLine.positionCount = 0;
    }
}