using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerBombHandler : MonoBehaviour
{
    private Bomb currentBomb;
    public LineRenderer trajectoryLine;
    private bool isAiming = false;
    private List<Vector3> trajectoryPoints = new List<Vector3>();

    // tweak in Inspector if you want fewer/more points
    private const int MAX_TRAJECTORY_POINTS = 100;
    private float timeStep;

    void Awake()
    {
        trajectoryLine = GetComponent<LineRenderer>();
        trajectoryLine.positionCount = 0;
        trajectoryLine.startWidth   = 0.1f;
        trajectoryLine.endWidth     = 0.1f;
        trajectoryLine.material     = new Material(Shader.Find("Sprites/Default"));

        // we’ll step in fixed‑delta intervals
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

    public void OnSwapBomb(bool isPressed)
    {
        if (isPressed && currentBomb != null)
            currentBomb.SwapHoldPoint();
    }

    public void OnThrow(bool isPressed)
    {
        if (isPressed)
        {
            if (currentBomb != null) isAiming = true;
        }
        else
        {
            if (isAiming && currentBomb != null)
            {
                currentBomb.ThrowBomb();
                isAiming = false;
                HideTrajectory();
            }
        }
    }

    void Update()
    {
        // if the bomb exploded mid‑aim, clear the line
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

        // find the correct hold‑point
        string holdName = currentBomb.IsOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        Transform holdPoint = transform.Find(holdName);
        if (holdPoint == null) return;

        // initial conditions
        Vector3 startPos = holdPoint.position;
        bool   isRight  = currentBomb.IsOnRight;
        float  speed    = isRight
                         ? currentBomb.normalThrowSpeed
                         : currentBomb.lobThrowSpeed;
        float  upward   = isRight
                         ? currentBomb.normalThrowUpward
                         : currentBomb.lobThrowUpward;

        Vector3 v0 = holdPoint.forward * speed + Vector3.up * upward;

        // color‐code the line
        Color c = isRight ? Color.blue : Color.yellow;
        trajectoryLine.startColor = c;
        trajectoryLine.endColor   = c;

        // step through time, sample points
        Vector3 lastPos = startPos;
        trajectoryPoints.Add(lastPos);
        float t = 0f;

        for (int i = 0; i < MAX_TRAJECTORY_POINTS; i++)
        {
            t += timeStep;
            // equation: p = p0 + v0*t + ½*g*t²
            Vector3 newPos = startPos
                           + v0 * t
                           + 0.5f * Physics.gravity * (t * t);

            Vector3 dir = newPos - lastPos;
            float   dist = dir.magnitude;

            if (dist > 0f)
            {
                // raycast against real world colliders
                if (Physics.Raycast(lastPos,
                                    dir.normalized,
                                    out RaycastHit hit,
                                    dist))
                {
                    // if we hit your floor/map, add the exact hit
                    if (hit.collider.CompareTag("Map"))
                    {
                        trajectoryPoints.Add(hit.point);
                        break;
                    }
                }
            }

            trajectoryPoints.Add(newPos);

            // optional early‐stop if it’s nearly stopped
            Vector3 vel = v0 + Physics.gravity * t;
            if (vel.magnitude < 0.1f) break;

            lastPos = newPos;
        }

        // push into the line renderer
        trajectoryLine.positionCount = trajectoryPoints.Count;
        trajectoryLine.SetPositions(trajectoryPoints.ToArray());
    }

    private void HideTrajectory()
    {
        trajectoryLine.positionCount = 0;
    }
}
