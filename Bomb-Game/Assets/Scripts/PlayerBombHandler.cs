using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer), typeof(PlayerInput))]
public class PlayerBombHandler : MonoBehaviour
{
    private Bomb currentBomb;
    private LineRenderer trajectoryLine;
    private bool isAiming;
    private List<Vector3> trajectoryPoints = new();

    [Header("Trajectory Settings")]
    [SerializeField, Range(10, 300)] private int maxPoints = 100;
    [SerializeField] private LayerMask collisionMask;

    private float timeStep;
    public bool IsAiming => isAiming;
    public Vector3[] TrajectoryPoints => trajectoryPoints.ToArray();

    void Awake()
    {
        trajectoryLine = GetComponent<LineRenderer>();
        trajectoryLine.positionCount = 0;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth   = 0.1f;
        trajectoryLine.material   = new Material(Shader.Find("Sprites/Default"));

        timeStep = Time.fixedDeltaTime;
    }

    void Start()
    {
        // --- Input setup ---
        var input = GetComponent<PlayerInput>();
        if (input != null)
        {
            var swapAction = input.actions.FindAction("SwapBomb");
            if (swapAction != null)
                swapAction.performed += ctx => OnSwapBomb(ctx.ReadValueAsButton());

            var throwAction = input.actions.FindAction("Throw");
            if (throwAction != null)
            {
                throwAction.started  += ctx => OnThrow(true);
                throwAction.canceled += ctx => OnThrow(false);
            }
        }

        // Register with GameManager
        GameManager.Instance?.RegisterPlayer(gameObject);
    }

    void OnDestroy()
    {
        GameManager.Instance?.UnregisterPlayer(gameObject);
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

    public void OnSwapBomb(bool isPressed)
    {
        if (isPressed)
            currentBomb?.SwapHoldPoint();
    }

    public void OnThrow(bool isPressed)
    {
        if (isPressed) StartAiming();
        else          ThrowBomb();
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

        // Determine hold point
        var holdName = currentBomb.IsOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        var holdPoint = transform.Find(holdName);
        if (holdPoint == null) return;

        Vector3 startPos = holdPoint.position;
        float speed  = currentBomb.IsOnRight
                     ? currentBomb.NormalThrowSpeed
                     : currentBomb.LobThrowSpeed;
        float upward = currentBomb.IsOnRight
                     ? currentBomb.NormalThrowUpward
                     : currentBomb.LobThrowUpward;

        Vector3 v0 = holdPoint.forward * speed + Vector3.up * upward;
        Color c    = currentBomb.IsOnRight ? Color.blue : Color.yellow;
        trajectoryLine.startColor = c;
        trajectoryLine.endColor   = c;

        Vector3 lastPos = startPos;
        trajectoryPoints.Add(lastPos);
        float t = 0f;

        for (int i = 0; i < maxPoints; i++)
        {
            t += timeStep;
            Vector3 newPos = startPos + v0 * t + 0.5f * Physics.gravity * t * t;
            Vector3 dir    = newPos - lastPos;
            float   dist   = dir.magnitude;

            if (dist > 0f &&
                Physics.Raycast(lastPos, dir.normalized, out var hit, dist, collisionMask) &&
                hit.collider.CompareTag("Map"))
            {
                trajectoryPoints.Add(hit.point);
                break;
            }

            trajectoryPoints.Add(newPos);
            if ((v0 + Physics.gravity * t).magnitude < 0.1f)
                break;

            lastPos = newPos;
        }

        trajectoryLine.positionCount = trajectoryPoints.Count;
        trajectoryLine.SetPositions(trajectoryPoints.ToArray());
    }

    private void HideTrajectory()
    {
        trajectoryLine.positionCount = 0;
    }
}
