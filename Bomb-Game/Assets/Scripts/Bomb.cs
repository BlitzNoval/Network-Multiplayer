using UnityEngine;
using UnityEngine.InputSystem;

public class Bomb : MonoBehaviour
{
    private Rigidbody rb;
    public GameObject holder; // The player currently holding the bomb
    private bool isOnRight = true; // Tracks which hold point (true = right, false = left)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody missing on Bomb!");
        }
    }

    public void AssignToPlayer(GameObject player)
    {
        holder = player;
        if (holder != null)
        {
            // Default to right hold point
            UpdateHoldPoint();
        }
        else
        {
            Debug.LogError("No player assigned to bomb!");
        }
    }

    void UpdateHoldPoint()
    {
        if (holder == null) return;

        // Find the appropriate hold point
        string holdPointName = isOnRight ? "RightHoldPoint" : "LeftHoldPoint";
        Transform holdPoint = holder.transform.Find(holdPointName);
        if (holdPoint != null)
        {
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            rb.isKinematic = true; // Held, no physics
        }
        else
        {
            Debug.LogError($"{holdPointName} not found on player: " + holder.name);
        }
    }

    // Called by PlayerInput when "SwapBomb" action is triggered
    public void OnSwapBomb(bool isPressed)
    {
        if (isPressed && holder != null && holder.GetComponent<PlayerMovement>() != null)
        {
            // Swap hold point
            isOnRight = !isOnRight;
            UpdateHoldPoint();
        }
    }
}