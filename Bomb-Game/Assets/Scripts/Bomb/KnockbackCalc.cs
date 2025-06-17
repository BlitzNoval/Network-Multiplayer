using UnityEngine;
using Mirror;

public class KnockbackCalculator : MonoBehaviour
{
    [Header("Knockback Settings")]
    [SerializeField] private float baseKnockback = 15f; // Increased from 10f for stronger knockback
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private AnimationCurve sectorFalloffCurve;
    
    [Header("Sector Configuration")]
    [SerializeField] private float[] sectorRadii = { 1f, 2f, 3.5f, 5f }; // Percentage of explosion radius
    [SerializeField] private float[] sectorMultipliers = { 1f, 0.75f, 0.5f, 0.1f }; // 100%, 75%, 50%, 10%
    
    [Header("Physics Settings")]
    [SerializeField] private float baseUpwardBias = 0.35f; // Base upward force at 0% (increased from 0.3f)
    [SerializeField] private float maxUpwardBias = 0.85f; // Maximum upward force at high % (increased from 0.8f)
    [SerializeField] private float percentageCurveExponent = 1.3f; // How quickly upward bias increases (increased from 1.2f)
    [SerializeField] private float massInfluence = 0.4f; // How much mass affects knockback (reduced from 0.5f for more consistent knockback)
    [SerializeField] private AnimationCurve knockbackAngleCurve; // Custom curve for knockback angle
    
    [Header("Force Multipliers")]
    [SerializeField] private float holderMultiplier = 1.7f; // Knockback bonus for bomb holder
    [SerializeField] private float verticalStrengthMultiplier = 0.9f; // Vertical force multiplier
    
    [Header("Horizontal Force Settings")]
    [SerializeField] private float horizontalStrengthMin = 2.2f; // Minimum horizontal strength
    [SerializeField] private float horizontalStrengthMax = 1.4f; // Maximum horizontal strength (at high upward ratio)
    [SerializeField] private float horizontalBoostMin = 1.7f; // Minimum horizontal boost
    [SerializeField] private float horizontalBoostMax = 2.5f; // Maximum horizontal boost
    
    [Header("Dynamic Knockback Settings")]
    [SerializeField] private float baseKnockbackIncreaseRate = 10f; // Base rate per second
    [SerializeField] private float[] milestoneMultipliers = { 1f, 1.2f, 1.4f, 1.6f }; // At 0%, 100%, 200%, 300%
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSectors = false;
    [SerializeField] private float debugDisplayDuration = 2f;
    [SerializeField] private Color[] sectorColors = { Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green };
    
    // Global debug toggle
    public static bool GlobalDebugEnabled = false;
    
    private void Awake()
    {
        // Initialize falloff curve if not set
        if (sectorFalloffCurve == null || sectorFalloffCurve.keys.Length == 0)
        {
            sectorFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }
        
        // Initialize knockback angle curve if not set (Better horizontal/vertical balance)
        if (knockbackAngleCurve == null || knockbackAngleCurve.keys.Length == 0)
        {
            // 0% = mostly horizontal (0.2), gradual increase to 350% = strong vertical (0.65), 500%+ = max vertical (0.7)
            knockbackAngleCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f),                // 0% knockback - strong horizontal potential
                new Keyframe(50f, 0.25f),              // 50% knockback - slight increase
                new Keyframe(100f, 0.3f),              // 100% knockback - balanced start
                new Keyframe(200f, 0.4f),              // 200% knockback - more balanced
                new Keyframe(350f, 0.65f),             // 350% knockback - strong vertical (max intended)
                new Keyframe(500f, 0.7f)               // 500%+ knockback - max vertical
            );
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
        
        // Apply holder bonus (increased for stronger holder punishment)
        float appliedHolderMultiplier = isHolder ? holderMultiplier : 1f;
        
        // Get mass modifier (lighter = more knockback)
        float massModifier = 1f;
        if (target.TryGetComponent<Rigidbody>(out var rb))
        {
            float standardMass = 1f; // Assuming standard player mass
            massModifier = Mathf.Lerp(1f, standardMass / rb.mass, massInfluence);
        }
        
        // Calculate final knockback magnitude
        float knockbackMagnitude = baseKnockback * sectorMultiplier * percentageModifier * appliedHolderMultiplier * massModifier;
        
        // Calculate balanced horizontal/vertical knockback
        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z).normalized;
        
        // Get upward ratio from curve - this determines vertical vs horizontal balance
        float upwardRatio = knockbackAngleCurve.Evaluate(percentageKnockback);
        
        // Calculate horizontal and vertical components with stronger horizontal emphasis
        float horizontalStrength = Mathf.Lerp(horizontalStrengthMin, horizontalStrengthMax, upwardRatio);
        float verticalStrength = upwardRatio * verticalStrengthMultiplier;
        
        // Factor in player's current movement for momentum-based knockback
        Vector3 playerVelocity = Vector3.zero;
        if (target.TryGetComponent<Rigidbody>(out var playerRb))
        {
            playerVelocity = new Vector3(playerRb.linearVelocity.x, 0, playerRb.linearVelocity.z);
            // Add player momentum to horizontal direction (up to 50% boost)
            horizontalStrength += playerVelocity.magnitude * 0.1f;
        }
        
        // Create knockback direction with enhanced horizontal focus
        Vector3 knockbackDirection = (horizontalDir * horizontalStrength + Vector3.up * verticalStrength).normalized;
        
        // Add slight randomness for natural feel (very minimal)
        float randomness = Mathf.Lerp(0.05f, 0.02f, percentageKnockback / 500f);
        knockbackDirection += Random.insideUnitSphere * randomness;
        knockbackDirection.Normalize();
        
        // Apply strong horizontal boost to ensure knockout potential at any percentage
        float horizontalBoost = Mathf.Lerp(horizontalBoostMin, horizontalBoostMax, percentageKnockback / 500f);
        knockbackDirection.x *= horizontalBoost;
        knockbackDirection.z *= horizontalBoost;
        knockbackDirection.Normalize();
        
        result.affected = true;
        result.force = knockbackDirection * knockbackMagnitude;
        result.magnitude = knockbackMagnitude;
        result.sector = GetSectorIndex(normalizedDistance);
        result.direction = knockbackDirection;
        
        // Debug visualization
        if (GlobalDebugEnabled || showDebugSectors)
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
        if (!GlobalDebugEnabled && !showDebugSectors) return;
        
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
        if (!GlobalDebugEnabled && !showDebugSectors) return;
        
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
        // Handle F1 debug toggle
        if (Input.GetKeyDown(KeyCode.F1))
        {
            GlobalDebugEnabled = !GlobalDebugEnabled;
            Debug.Log($"Knockback Debug Visualization: {(GlobalDebugEnabled ? "ENABLED" : "DISABLED")}");
        }
        
        // Draw runtime debug circles if enabled
        if (GlobalDebugEnabled || showDebugSectors)
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
    
    public float GetDynamicKnockbackRate(float currentPercentage)
    {
        // Calculate rate multiplier based on current percentage milestones
        float rateMultiplier = 1f;
        if (currentPercentage >= 300f) rateMultiplier = milestoneMultipliers[3];
        else if (currentPercentage >= 200f) rateMultiplier = milestoneMultipliers[2];
        else if (currentPercentage >= 100f) rateMultiplier = milestoneMultipliers[1];
        else rateMultiplier = milestoneMultipliers[0];
        
        return baseKnockbackIncreaseRate * rateMultiplier;
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