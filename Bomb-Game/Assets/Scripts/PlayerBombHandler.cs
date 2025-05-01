using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class PlayerBombHandler : MonoBehaviour
{
    private Bomb currentBomb;
    public LineRenderer trajectoryLine;
    private bool isAiming = false;
    private List<Vector3> trajectoryPoints = new List<Vector3>();

    void Awake()
    {
        trajectoryLine = GetComponent<LineRenderer>();
        trajectoryLine.positionCount = 0;
        // Configure Line Renderer
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.1f;
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.startColor = Color.red; // Adjust later for normal/lob
        trajectoryLine.endColor = Color.red;
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
        {
            currentBomb.SwapHoldPoint();
        }
    }

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

    private void StartAiming()
    {
        if (currentBomb != null)
        {
            isAiming = true;
        }
    }

    private void ThrowBomb()
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
        if (isAiming)
        {
            SimulateTrajectory();
        }
    }

    private void SimulateTrajectory()
    {
        if (currentBomb == null) return;

        trajectoryPoints.Clear();
        Scene parallelScene = GameManager.Instance.parallelScene;
        Transform holdPoint = transform.Find(currentBomb.IsOnRight ? "RightHoldPoint" : "LeftHoldPoint");
        if (holdPoint == null) return;

        GameObject simulationBomb = Instantiate(currentBomb.gameObject, holdPoint.position, holdPoint.rotation);
        SceneManager.MoveGameObjectToScene(simulationBomb, parallelScene);

        Rigidbody simRb = simulationBomb.GetComponent<Rigidbody>();
        simRb.isKinematic = false;
        simRb.useGravity = true;

        bool isRightHand = currentBomb.IsOnRight;
        float speed = isRightHand ? currentBomb.normalThrowSpeed : currentBomb.lobThrowSpeed;
        float upward = isRightHand ? currentBomb.normalThrowUpward : currentBomb.lobThrowUpward;
        Vector3 initialVelocity = transform.forward * speed + Vector3.up * upward;
        simRb.linearVelocity = initialVelocity;

        trajectoryLine.startColor = isRightHand ? Color.blue : Color.yellow;
        trajectoryLine.endColor = isRightHand ? Color.blue : Color.yellow;

        PhysicsScene physicsScene = parallelScene.GetPhysicsScene();
        int maxPoints = 100;
        for (int i = 0; i < maxPoints; i++)
        {
            physicsScene.Simulate(Time.fixedDeltaTime);
            trajectoryPoints.Add(simulationBomb.transform.position);
            if (i > 10 && simRb.linearVelocity.magnitude < 0.1f) break; // Stop if nearly still
        }

        Destroy(simulationBomb);
        trajectoryLine.positionCount = trajectoryPoints.Count;
        trajectoryLine.SetPositions(trajectoryPoints.ToArray());
    }

    private void HideTrajectory()
    {
        trajectoryLine.positionCount = 0;
    }
}