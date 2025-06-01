using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] public Transform playerTransform; // Assign the player's transform in the Inspector
    [SerializeField] private Vector3 offset = new Vector3(0, 2, 0); // Offset above the player's head

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (playerTransform == null)
        {
            Debug.LogError("PlayerTransform not assigned in Billboard script!", this);
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null || playerTransform == null) return;

        // Calculate the desired position above the player's head
        Vector3 desiredPosition = playerTransform.position + offset;

        // Convert to screen space to check visibility
        Vector3 screenPos = mainCamera.WorldToScreenPoint(desiredPosition);

        // Check if the player is off-screen
        if (screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height)
        {
            // Clamp to screen boundaries with a margin (10 pixels)
            screenPos.x = Mathf.Clamp(screenPos.x, 10, Screen.width - 10);
            screenPos.y = Mathf.Clamp(screenPos.y, 10, Screen.height - 10);
            // Ensure the position is in front of the camera
            screenPos.z = Mathf.Max(screenPos.z, 1f);
            // Convert back to world space
            Vector3 clampedPos = mainCamera.ScreenToWorldPoint(screenPos);
            transform.position = clampedPos;
        }
        else
        {
            // If on-screen, set to the desired position
            transform.position = desiredPosition;
        }

        // Orient the tag toward the camera
        transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                         mainCamera.transform.rotation * Vector3.up);
    }
}