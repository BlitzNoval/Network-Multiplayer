using UnityEngine;
using Mirror;

public class KnockbackCalculator : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionRadius = 2f; // Small for testing
    [SerializeField] private AnimationCurve sectorFalloffCurve;
    
    [Header("Sector Configuration")]
    [SerializeField] private float[] sectorRadii = { 0.5f, 1f, 1.5f, 2f }; // Small sectors for testing
    [SerializeField] private float[] sectorMultipliers = { 1f, 0.75f, 0.5f, 0.25f }; // Sector damage multipliers
    
    [Header("Arc Knockback Settings")]
    [SerializeField] private float baseDistance = 3f; // Base knockback distance
    [SerializeField] private float arcHeight = 1f; // Height of parabolic arc
    [SerializeField] private float arcDuration = 1f; // Time to complete the arc
    [SerializeField] private float holderDistanceMultiplier = 1.5f; // Extra distance for bomb holder
    [SerializeField] private float percentageDistanceMultiplier = 0.01f; // Distance per percentage point
    
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
        
        // Calculate knockback distance based on all factors
        float knockbackDistance = baseDistance;
        knockbackDistance *= sectorMultiplier; // Sector influence
        knockbackDistance += percentageKnockback * percentageDistanceMultiplier; // Percentage influence
        if (isHolder) knockbackDistance *= holderDistanceMultiplier; // Holder bonus
        
        // Calculate arc points
        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 startPoint = targetPos;
        Vector3 endPoint = startPoint + horizontalDir * knockbackDistance;
        
        // Generate arc points
        arcData.affected = true;
        arcData.startPoint = startPoint;
        arcData.endPoint = endPoint;
        arcData.arcHeight = arcHeight;
        arcData.duration = arcDuration;
        arcData.sector = sector;
        arcData.arcPoints = GenerateArcPoints(startPoint, endPoint, arcHeight);
        
        // Draw visualization
        if (GlobalDebugEnabled || showArcGizmos)
        {
            DrawPlayerArc(startPoint, endPoint, arcHeight, playerArcColor);
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
                Vector3 endPoint = center + direction * baseDistance;
                
                DrawGizmoParabolicArc(center, endPoint, arcHeight);
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
}