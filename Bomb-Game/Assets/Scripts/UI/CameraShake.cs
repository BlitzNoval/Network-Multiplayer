using UnityEngine;

public class CameraShake : MonoBehaviour 
{
    [SerializeField] private float shakeAmount = 0.02f;
    private Vector3 initialPos;
    private bool isShaking = false;
    
    void Awake() 
    {
        initialPos = transform.position;
    }
    
    void Update() 
    {
        if (isShaking)
        {
            transform.position = initialPos + Random.insideUnitSphere * shakeAmount;
        }
    }
    
    public void StartShake(float duration)
    {
        if (!isShaking)
        {
            initialPos = transform.position;
            isShaking = true;
            Invoke("StopShake", duration);
        }
    }
    
    public void StopShake()
    {
        isShaking = false;
        transform.position = initialPos;
    }
    
    public void SetShakeAmount(float amount)
    {
        shakeAmount = amount;
    }
}