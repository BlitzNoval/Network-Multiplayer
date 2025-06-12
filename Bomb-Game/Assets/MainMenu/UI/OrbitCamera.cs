using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5.0f;
    public float rotationSpeed = 20.0f;
    public float heightOffset = 1.0f;

    public float introDuration = 3.0f;
    public float pauseAfterIntro = 2.0f;
    public Vector3 introStartOffset = new Vector3(0, 10, -20);

    public float settleDuration = 0.6f;
    public float overshootStrength = 0.3f;

    public float pauseAfterRotationDuration = 2.0f;

    public Canvas introCompleteCanvas; // 👈 Add this Canvas reference

    private float currentAngle = 0f;
    private float timer = 0f;
    private float settleTimer = 0f;

    private float initialOrbitAngle = 0f;
    private float angleSinceStart = 0f;
    private float rotationPauseTimer = 0f;
    private bool isRotationPaused = false;

    private enum CameraState { Intro, Settle, Pause, Orbit }
    private CameraState state = CameraState.Intro;

    private Vector3 targetOrbitOffset;
    private Vector3 targetLookOffset;
    private Vector3 introStartPos;
    private Vector3 overshootPosition;
    private Vector3 cachedTargetPos;

    void Start()
    {
        if (target == null)
            return;

        if (introCompleteCanvas != null)
            introCompleteCanvas.enabled = false; // 👈 Hide UI initially

        targetOrbitOffset = new Vector3(0f, heightOffset, distance);
        targetLookOffset = Vector3.up * heightOffset;

        cachedTargetPos = target.position;
        introStartPos = cachedTargetPos + introStartOffset;
        transform.position = introStartPos;
        transform.LookAt(cachedTargetPos + targetLookOffset);

        currentAngle = 0f;
        Vector3 initialOrbitPos = cachedTargetPos + Quaternion.Euler(0, currentAngle, 0) * targetOrbitOffset;

        Vector3 toTarget = (initialOrbitPos - transform.position).normalized;
        overshootPosition = initialOrbitPos + toTarget * overshootStrength;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        cachedTargetPos = target.position;

        switch (state)
        {
            case CameraState.Intro:
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / introDuration);
                transform.position = Vector3.Lerp(introStartPos, overshootPosition, SmoothStep(t));
                transform.LookAt(cachedTargetPos + targetLookOffset);

                if (t >= 1.0f)
                {
                    timer = 0f;
                    settleTimer = 0f;
                    state = CameraState.Settle;
                }
                break;

            case CameraState.Settle:
                settleTimer += Time.deltaTime;
                float s = Mathf.Clamp01(settleTimer / settleDuration);
                float spring = Mathf.Sin(s * Mathf.PI);

                Vector3 finalOrbitPos = cachedTargetPos + Quaternion.Euler(0, currentAngle, 0) * targetOrbitOffset;
                transform.position = Vector3.LerpUnclamped(overshootPosition, finalOrbitPos, s + spring * (1f - s) * 0.1f);
                transform.LookAt(cachedTargetPos + targetLookOffset);

                if (s >= 1.0f)
                {
                    timer = 0f;
                    initialOrbitAngle = currentAngle;
                    angleSinceStart = 0f;

                   if (introCompleteCanvas != null)
                    introCompleteCanvas.enabled = true;

            
                UIMenuHub uiHub = FindObjectOfType<UIMenuHub>();
                if (uiHub != null)
                    uiHub.OnIntroComplete();

                    state = CameraState.Pause;
                }
                break;

            case CameraState.Pause:
                timer += Time.deltaTime;
                transform.position = cachedTargetPos + Quaternion.Euler(0, currentAngle, 0) * targetOrbitOffset;
                transform.LookAt(cachedTargetPos + targetLookOffset);

                if (timer >= pauseAfterIntro)
                {
                    timer = 0f;
                    state = CameraState.Orbit;
                }
                break;

            case CameraState.Orbit:
                if (!isRotationPaused)
                {
                    float angleStep = rotationSpeed * Time.deltaTime;
                    currentAngle += angleStep;
                    angleSinceStart += angleStep;

                    Vector3 orbitPos = cachedTargetPos + Quaternion.Euler(0, currentAngle, 0) * targetOrbitOffset;
                    transform.position = Vector3.Lerp(transform.position, orbitPos, Time.deltaTime * 2f);

                    Vector3 lookDir = (cachedTargetPos + targetLookOffset) - transform.position;
                    Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);

                    if (angleSinceStart >= 360f)
                    {
                        angleSinceStart = 0f;
                        isRotationPaused = true;
                        rotationPauseTimer = 0f;
                    }
                }
                else
                {
                    rotationPauseTimer += Time.deltaTime;
                    if (rotationPauseTimer >= pauseAfterRotationDuration)
                    {
                        isRotationPaused = false;
                    }
                }
                break;
        }
    }

    float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
