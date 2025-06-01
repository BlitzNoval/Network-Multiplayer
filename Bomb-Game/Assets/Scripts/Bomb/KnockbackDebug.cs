using UnityEngine;
using UnityEngine.InputSystem;

public class KnockbackDebugController : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F1;
    [SerializeField] private bool debugModeEnabled = false;
    
    private Bomb currentBomb;
    
    void Update()
    {
        // Toggle debug mode
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugModeEnabled = !debugModeEnabled;
            UpdateDebugMode();
            
            Debug.Log($"Knockback Debug Mode: {(debugModeEnabled ? "ENABLED" : "DISABLED")}", this);
        }
        
        // Find bomb if we don't have reference
        if (currentBomb == null)
        {
            currentBomb = Object.FindObjectOfType<Bomb>(); // Updated to FindObjectOfType
            if (currentBomb != null && debugModeEnabled)
            {
                currentBomb.SetKnockbackDebugMode(debugModeEnabled);
            }
        }
    }
    
    private void UpdateDebugMode()
    {
        // Update all bombs in scene
        var bombs = Object.FindObjectsOfType<Bomb>(); // Updated to FindObjectsOfType
        foreach (var bomb in bombs)
        {
            bomb.SetKnockbackDebugMode(debugModeEnabled);
        }
        
        // Update all knockback calculators
        var calculators = Object.FindObjectsOfType<KnockbackCalculator>(); // Updated to FindObjectsOfType
        foreach (var calc in calculators)
        {
            calc.SetDebugMode(debugModeEnabled);
        }
    }
    
    void OnGUI()
    {
        if (!debugModeEnabled) return;
        
        // Display debug info
        GUI.Label(new Rect(10, 10, 300, 20), "Knockback Debug Mode: ON (Press F1 to toggle)");
        
        // Display player knockback percentages
        var players = Object.FindObjectsOfType<PlayerLifeManager>(); // Updated to FindObjectsOfType
        int yOffset = 40;
        
        foreach (var player in players)
        {
            string info = $"Player {player.PlayerNumber}: {player.PercentageKnockback:F1}% knockback";
            GUI.Label(new Rect(10, yOffset, 300, 20), info);
            yOffset += 25;
        }
        
        // Sector color legend
        yOffset += 20;
        GUI.Label(new Rect(10, yOffset, 200, 20), "Sector Colors:");
        yOffset += 25;
        
        GUI.color = Color.red;
        GUI.Label(new Rect(10, yOffset, 200, 20), "S1: 100% (Red)");
        yOffset += 20;
        
        GUI.color = new Color(1f, 0.5f, 0f);
        GUI.Label(new Rect(10, yOffset, 200, 20), "S2: 80% (Orange)");
        yOffset += 20;
        
        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, yOffset, 200, 20), "S3: 50% (Yellow)");
        yOffset += 20;
        
        GUI.color = Color.green;
        GUI.Label(new Rect(10, yOffset, 200, 20), "S4: 20% (Green)");
        
        GUI.color = Color.white;
    }
}