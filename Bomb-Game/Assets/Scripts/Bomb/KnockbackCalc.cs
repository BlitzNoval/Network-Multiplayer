using UnityEngine;
using Mirror;

public class KnockbackCalculator : MonoBehaviour
{
    [Header("Knockback Settings")]
    [SerializeField] private float baseKnockback = 10f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private AnimationCurve sectorFalloffCurve;
    
    [Header("Sector Configuration")]
    [SerializeField] private float[] sectorRadii = { 1f, 2f, 3.5f, 5f }; // Percentage of explosion radius
    [SerializeField] private float[] sectorMultipliers = { 1f, 0.8f, 0.5f, 0.2f };
    
    [Header("Physics Settings")]
    [SerializeField] private float upwardBias = 0.15f; // 15% of force goes upward
    [SerializeField] private float horizontalBias = 0.85f; // 85% goes horizontal
    [SerializeField] private float massInfluence = 0.5f; // How much mass affects knockback
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSectors = false;
    [SerializeField] private float debugDisplayDuration = 2f;
    [SerializeField] private Color[] sectorColors = { Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green };
    
    private void Awake()
    {
        // Initialize falloff curve if not set
        if (sectorFalloffCurve == null || sectorFalloffCurve.keys.Length == 0)
        {
            sectorFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }
    }
    
    public KnockbackResult CalculateKnockback(Vector3 explosionPos, GameObject target, float percentageKnockback, bool isHolder)
    {
        var result = new KnockbackResult();
        
        // Calculate distance and direction
        Vector3 targetPos = target.transform.position;
        Vector3 direction = (targetPos - explosionPos);
        float distance = direction.magnitude;
        direction.Normalize();
        
        // Determine sector and base multiplier
        float normalizedDistance = distance / explosionRadius;
        float sectorMultiplier = GetSectorMultiplier(normalizedDistance);
        
        if (sectorMultiplier <= 0f)
        {
            result.affected = false;
            return result;
        }
        
        // Calculate percentage modifier with exponential scaling
        float percentageModifier = 1f + Mathf.Pow(percentageKnockback / 100f, 1.5f);
        
        // Apply holder bonus
        float holderMultiplier = isHolder ? 1.5f : 1f;
        
        // Get mass modifier (lighter = more knockback)
        float massModifier = 1f;
        if (target.TryGetComponent<Rigidbody>(out var rb))
        {
            float standardMass = 1f; // Assuming standard player mass
            massModifier = Mathf.Lerp(1f, standardMass / rb.mass, massInfluence);
        }
        
        // Calculate final knockback magnitude
        float knockbackMagnitude = baseKnockback * sectorMultiplier * percentageModifier * holderMultiplier * massModifier;
        
        // Calculate knockback direction with upward bias
        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 knockbackDirection = (horizontalDir * horizontalBias + Vector3.up * upwardBias).normalized;
        
        // Add slight randomness for more natural feel
        knockbackDirection += Random.insideUnitSphere * 0.1f;
        knockbackDirection.Normalize();
        
        result.affected = true;
        result.force = knockbackDirection * knockbackMagnitude;
        result.magnitude = knockbackMagnitude;
        result.sector = GetSectorIndex(normalizedDistance);
        result.direction = knockbackDirection;
        
        // Debug visualization
        if (showDebugSectors)
        {
            DrawDebugSector(explosionPos, targetPos, result.sector);
        }
        
        return result;
    }
    
    private float GetSectorMultiplier(float normalizedDistance)
    {
        // Use smooth falloff curve for better feel
        if (normalizedDistance > 1f) return 0f;
        
        // Find which sector we're in
        for (int i = 0; i < sectorRadii.Length; i++)
        {
            if (normalizedDistance <= sectorRadii[i])
            {
                // Smooth interpolation between sectors
                float sectorStart = i > 0 ? sectorRadii[i - 1] : 0f;
                float sectorEnd = sectorRadii[i];
                float t = Mathf.InverseLerp(sectorStart, sectorEnd, normalizedDistance);
                
                float currentMultiplier = sectorMultipliers[i];
                float nextMultiplier = i < sectorMultipliers.Length - 1 ? sectorMultipliers[i + 1] : 0f;
                
                // Use curve for smooth falloff
                float curveValue = sectorFalloffCurve.Evaluate(t);
                return Mathf.Lerp(currentMultiplier, nextMultiplier, 1f - curveValue);
            }
        }
        
        return 0f;
    }
    
    private int GetSectorIndex(float normalizedDistance)
    {
        for (int i = 0; i < sectorRadii.Length; i++)
        {
            if (normalizedDistance <= sectorRadii[i])
                return i + 1; // 1-indexed for display
        }
        return 0; // Outside all sectors
    }
    
    private void DrawDebugSector(Vector3 explosionPos, Vector3 targetPos, int sector)
    {
        if (sector < 1 || sector > sectorColors.Length) return;
        
        Color color = sectorColors[sector - 1];
        Debug.DrawLine(explosionPos, targetPos, color, debugDisplayDuration);
        Debug.DrawRay(targetPos, Vector3.up * 2f, color, debugDisplayDuration);
    }
    
    public void DrawDebugSectors(Vector3 center)
    {
        if (!showDebugSectors) return;
        
        for (int i = 0; i < sectorRadii.Length; i++)
        {
            float radius = sectorRadii[i] * explosionRadius;
            DrawDebugCircle(center, radius, sectorColors[i], 32);
        }
    }
    
    private void DrawDebugCircle(Vector3 center, float radius, Color color, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Debug.DrawLine(prevPoint, newPoint, color, Time.deltaTime);
            prevPoint = newPoint;
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugSectors) return;
        
        // Draw sector circles as gizmos
        Vector3 center = transform.position;
        
        for (int i = 0; i < sectorRadii.Length; i++)
        {
            float radius = sectorRadii[i] * explosionRadius;
            Gizmos.color = sectorColors[i];
            DrawGizmoCircle(center, radius, 32);
        }
    }
    
    private void DrawGizmoCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    void Update()
    {
        // Draw runtime debug circles if enabled
        if (showDebugSectors)
        {
            DrawRuntimeDebugSectors();
        }
    }
    
    private void DrawRuntimeDebugSectors()
    {
        Vector3 center = transform.position;
        
        for (int i = 0; i < sectorRadii.Length; i++)
        {
            float radius = sectorRadii[i] * explosionRadius;
            DrawDebugCircle(center, radius, sectorColors[i], 48);
        }
    }
    
    public void SetDebugMode(bool enabled)
    {
        showDebugSectors = enabled;
    }
    
    public void UpdateSettings(float newBaseKnockback, float newExplosionRadius)
    {
        baseKnockback = newBaseKnockback;
        explosionRadius = newExplosionRadius;
    }
}

[System.Serializable]
public struct KnockbackResult
{
    public bool affected;
    public Vector3 force;
    public float magnitude;
    public int sector;
    public Vector3 direction;
}