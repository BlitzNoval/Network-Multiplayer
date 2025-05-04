using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerBombHandler : MonoBehaviour
{
    private Bomb currentBomb;
    public LineRenderer trajectoryLine;
    private bool isAiming = false;
    private List<Vector3> trajectoryPoints = new List<Vector3>();

    // Tweak in Inspector if you want fewer/more points
    private const int MAX_TRAJECTORY_POINTS = 100;
    private float timeStep;

    // Public getters for state syncing
    public bool IsAiming => isAiming;
    public Vector3[] TrajectoryPoints => trajectoryPoints.ToArray();

    void Awake()
    {
        trajectoryLine = GetComponent<LineRenderer>();
        trajectoryLine.positionCount = 0;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.1f;
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));

        // We’ll step in fixed-delta intervals
        timeStep = Time.fixedDeltaTime;
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

    // Input handling for swapping bomb hold point
    public void OnSwapBomb(bool isPressed)
    {
        if (isPressed && currentBomb != null)
        {
            SwapBombHoldPoint();
        }
    }

    // Input handling for throwing bomb
    public void OnThrow(bool isPressed)
    {
        if (isPressed)
        {
            StartAiming();
        }
        else
        {
            ThrowBomb();
        }
    }

    // Start aiming the bomb (intended for local player input)
    public void StartAiming()
    {
        if (currentBomb != null)
        {
            isAiming = true;
        }
    }

    // Stop aiming the bomb (intended for local player input)
    public void StopAiming()
    {
        isAiming = false;
        HideTrajectory();
    }

    // Swap the bomb's hold point (intended for server-side call when networked)
    public void SwapBombHoldPoint()
    {
        if (currentBomb != null)
        {
            currentBomb.SwapHoldPoint();
        }
    }

    // Throw the bomb (intended for server-side call when networked)
    public void ThrowBomb()
    {
        if (isAiming && currentBomb != null)
        {
            currentBomb.ThrowBomb();
            isAiming = false;
            HideTrajectory();
        }
    }

    void Update()
    {
        // If the bomb exploded mid-aim, clear the line
        if (currentBomb == null && isAiming)
        {
            isAiming = false;
            HideTrajectory();
            return;
        }

        if (isAiming)
            SimulateTrajectory();
    }

    private void SimulateTrajectory()
    {
        trajectoryPoints.Clear();

        // Find the correct hold-point
        if (currentBomb == null) return;
        string holdName = currentBomb.IsOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        Transform holdPoint = transform.Find(holdName);
        if (holdPoint == null) return;

        // Initial conditions
        Vector3 startPos = holdPoint.position;
        bool isRight = currentBomb.IsOnRight;
        float speed = isRight ? currentBomb.normalThrowSpeed : currentBomb.lobThrowSpeed;
        float upward = isRight ? currentBomb.normalThrowUpward : currentBomb.lobThrowUpward;

        Vector3 v0 = holdPoint.forward * speed + Vector3.up * upward;

        // Color-code the line
        Color c = isRight ? Color.blue : Color.yellow;
        trajectoryLine.startColor = c;
        trajectoryLine.endColor = c;

        // Step through time, sample points
        Vector3 lastPos = startPos;
        trajectoryPoints.Add(lastPos);
        float t = 0f;

        for (int i = 0; i < MAX_TRAJECTORY_POINTS; i++)
        {
            t += timeStep;
            // Equation: p = p0 + v0*t + ½*g*t²
            Vector3 newPos = startPos + v0 * t + 0.5f * Physics.gravity * (t * t);

            Vector3 dir = newPos - lastPos;
            float dist = dir.magnitude;

            if (dist > 0f)
            {
                // Raycast against real world colliders
                if (Physics.Raycast(lastPos, dir.normalized, out RaycastHit hit, dist))
                {
                    // If we hit the floor/map, add the exact hit
                    if (hit.collider.CompareTag("Map"))
                    {
                        trajectoryPoints.Add(hit.point);
                        break;
                    }
                }
            }

            trajectoryPoints.Add(newPos);

            // Optional early-stop if it’s nearly stopped
            Vector3 vel = v0 + Physics.gravity * t;
            if (vel.magnitude < 0.1f) break;

            lastPos = newPos;
        }

        // Push into the line renderer
        trajectoryLine.positionCount = trajectoryPoints.Count;
        trajectoryLine.SetPositions(trajectoryPoints.ToArray());
    }

    private void HideTrajectory()
    {
        trajectoryLine.positionCount = 0;
    }
}