using UnityEngine;
using Mirror;

public class KnockbackCalculator : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private AnimationCurve sectorFalloffCurve;
    
    [Header("Damage Sectors")]
    [SerializeField] private float[] sectorRadii = { 0.5f, 1f, 1.5f, 2f };
    [SerializeField] private float[] sectorMultipliers = { 1f, 0.75f, 0.5f, 0.25f };
    
    [Header("Base Arc Properties")]
    [SerializeField] private float baseKnockbackDistance = 3f;
    [SerializeField] private float baseArcHeight = 1f;
    [SerializeField] private float baseArcDuration = 1f;
    [SerializeField] private float bombHolderBonus = 1.5f;
    
    [Header("Percentage Scaling (0%, 25%, 50%, 75%, 100%=350%)")]
    [SerializeField] private float[] percentageThresholds = { 0f, 87.5f, 175f, 262.5f, 350f };
    [SerializeField] private float[] distanceMultipliers = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    [SerializeField] private float[] heightMultipliers = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    [SerializeField] private float[] durationMultipliers = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    
    [Header("Daze Settings")]
    [SerializeField] private float dazeTime = 1.5f; // Time player is dazed after landing
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSectors = false;
    [SerializeField] private bool showArcGizmos = true;
    [SerializeField] private int arcResolution = 30;
    [SerializeField] private Color arcColor = Color.red;
    [SerializeField] private Color playerArcColor = Color.yellow;
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
    }

    public KnockbackArcData CalculateKnockbackArc(Vector3 explosionPos, GameObject target, float percentageKnockback, bool isHolder)
    {
        var arcData = new KnockbackArcData();
        
        // Calculate distance and direction from explosion
        Vector3 targetPos = target.transform.position;
        Vector3 direction = (targetPos - explosionPos);
        float distance = direction.magnitude;
        direction.Normalize();
        
        // Determine sector and check if affected
        float normalizedDistance = distance / explosionRadius;
        float sectorMultiplier = GetSectorMultiplier(normalizedDistance);
        int sector = GetSectorIndex(normalizedDistance);
        
        if (sectorMultiplier <= 0f)
        {
            arcData.affected = false;
            return arcData;
        }
        
        // Calculate percentage-based multipliers
        float distanceBonus = GetPercentageMultiplier(percentageKnockback, distanceMultipliers);
        float heightBonus = GetPercentageMultiplier(percentageKnockback, heightMultipliers);
        float durationBonus = GetPercentageMultiplier(percentageKnockback, durationMultipliers);
        
        // Calculate final arc properties
        float finalDistance = baseKnockbackDistance * (1f + distanceBonus) * sectorMultiplier;
        float finalHeight = baseArcHeight * (1f + heightBonus);
        float finalDuration = baseArcDuration * (1f + durationBonus);
        
        // Apply bomb holder bonus
        if (isHolder) finalDistance *= bombHolderBonus;
        
        // Calculate arc points with scaled values
        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 startPoint = targetPos;
        Vector3 endPoint = startPoint + horizontalDir * finalDistance;
        
        // Generate arc data with scaled properties
        arcData.affected = true;
        arcData.startPoint = startPoint;
        arcData.endPoint = endPoint;
        arcData.arcHeight = finalHeight;
        arcData.duration = finalDuration;
        arcData.sector = sector;
        arcData.arcPoints = GenerateArcPoints(startPoint, endPoint, finalHeight);
        arcData.dazeTime = dazeTime;
        
        // Draw visualization
        if (GlobalDebugEnabled || showArcGizmos)
        {
            DrawPlayerArc(startPoint, endPoint, finalHeight, playerArcColor);
        }
        
        return arcData;
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

    private Vector3[] GenerateArcPoints(Vector3 startPos, Vector3 endPos, float height)
    {
        Vector3[] arcPoints = new Vector3[arcResolution];
        
        for (int i = 0; i < arcResolution; i++)
        {
            float t = (float)i / (arcResolution - 1);
            
            // Linear interpolation for horizontal movement
            Vector3 horizontalPos = Vector3.Lerp(startPos, endPos, t);
            
            // Parabolic curve for vertical movement (perfect arc)
            float verticalOffset = 4 * height * t * (1 - t);
            
            arcPoints[i] = new Vector3(horizontalPos.x, startPos.y + verticalOffset, horizontalPos.z);
        }
        
        return arcPoints;
    }
    
    private void DrawPlayerArc(Vector3 startPos, Vector3 endPos, float height, Color color)
    {
        Vector3[] arcPoints = GenerateArcPoints(startPos, endPos, height);
        
        // Draw the arc with debug lines
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            Debug.DrawLine(arcPoints[i], arcPoints[i + 1], color, debugDisplayDuration);
        }
        
        // Draw start and end markers
        Debug.DrawRay(startPos, Vector3.up * 0.5f, Color.green, debugDisplayDuration);
        Debug.DrawRay(endPos, Vector3.up * 0.5f, Color.red, debugDisplayDuration);
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
        if (!GlobalDebugEnabled && !showDebugSectors && !showArcGizmos) return;
        
        // Draw sector circles as gizmos
        Vector3 center = transform.position;
        
        if (GlobalDebugEnabled || showDebugSectors)
        {
            for (int i = 0; i < sectorRadii.Length; i++)
            {
                float radius = sectorRadii[i] * explosionRadius;
                Gizmos.color = sectorColors[i];
                DrawGizmoCircle(center, radius, 32);
            }
        }
        
        // Draw sample parabolic arcs from explosion center
        if (showArcGizmos)
        {
            Gizmos.color = arcColor;
            
            // Draw sample arcs in 8 directions
            for (int dir = 0; dir < 8; dir++)
            {
                float angle = dir * 45f * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 endPoint = center + direction * baseKnockbackDistance;
                
                DrawGizmoParabolicArc(center, endPoint, baseArcHeight);
            }
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
    
    private void DrawGizmoParabolicArc(Vector3 startPos, Vector3 endPos, float height)
    {
        Vector3 prevPoint = startPos;
        
        for (int i = 1; i <= arcResolution; i++)
        {
            float t = (float)i / arcResolution;
            
            // Linear interpolation for horizontal movement
            Vector3 horizontalPos = Vector3.Lerp(startPos, endPos, t);
            
            // Parabolic curve for vertical movement (perfect half circle arc)
            float verticalOffset = 4 * height * t * (1 - t);
            
            Vector3 currentPoint = new Vector3(horizontalPos.x, startPos.y + verticalOffset, horizontalPos.z);
            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
        
        // Draw markers at start and end
        Gizmos.DrawWireSphere(startPos, 0.1f);
        Gizmos.DrawWireSphere(endPos, 0.1f);
    }

    void Update()
    {
        // Handle F1 debug toggle
        if (Input.GetKeyDown(KeyCode.F1))
        {
            GlobalDebugEnabled = !GlobalDebugEnabled;
            Debug.Log($"Knockback Debug Visualization: {(GlobalDebugEnabled ? "ENABLED" : "DISABLED")}");
        }
    }

    public void SetDebugMode(bool enabled)
    {
        showDebugSectors = enabled;
    }
    
    private float GetPercentageMultiplier(float currentPercentage, float[] multipliers)
    {
        // Find which threshold we're between
        for (int i = 0; i < percentageThresholds.Length - 1; i++)
        {
            if (currentPercentage <= percentageThresholds[i + 1])
            {
                float t = Mathf.InverseLerp(percentageThresholds[i], percentageThresholds[i + 1], currentPercentage);
                return Mathf.Lerp(multipliers[i], multipliers[i + 1], t);
            }
        }
        // If above max threshold, use max multiplier
        return multipliers[multipliers.Length - 1];
    }
    
    
    public float GetDynamicKnockbackRate(float currentPercentage)
    {
        // Simple rate calculation for percentage increase over time
        float baseRate = 10f;
        if (currentPercentage >= 300f) return baseRate * 1.6f;
        else if (currentPercentage >= 200f) return baseRate * 1.4f;
        else if (currentPercentage >= 100f) return baseRate * 1.2f;
        else return baseRate;
    }
}

[System.Serializable]
public struct KnockbackArcData
{
    public bool affected;
    public Vector3 startPoint;
    public Vector3 endPoint;
    public float arcHeight;
    public float duration;
    public int sector;
    public Vector3[] arcPoints;
    public float dazeTime;
}