using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] public Transform playerTransform;
    [SerializeField] private Vector3 offset = new Vector3(0, 2, 0);

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

        Vector3 desiredPosition = playerTransform.position + offset;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(desiredPosition);

        if (screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height)
        {
            screenPos.x = Mathf.Clamp(screenPos.x, 10, Screen.width - 10);
            screenPos.y = Mathf.Clamp(screenPos.y, 10, Screen.height - 10);
            screenPos.z = Mathf.Max(screenPos.z, 1f);
            Vector3 clampedPos = mainCamera.ScreenToWorldPoint(screenPos);
            transform.position = clampedPos;
        }
        else
        {
            transform.position = desiredPosition;
        }

        transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                         mainCamera.transform.rotation * Vector3.up);
    }
}