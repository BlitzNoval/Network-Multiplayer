using UnityEngine;
using UnityEngine.InputSystem;

public class KnockbackDebugController : MonoBehaviour
{
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F1;
    [SerializeField] private bool debugModeEnabled = false;
    
    private Bomb currentBomb;
    
    void Update()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugModeEnabled = !debugModeEnabled;
            UpdateDebugMode();
            
            Debug.Log($"Knockback Debug Mode: {(debugModeEnabled ? "ENABLED" : "DISABLED")}", this);
        }
        
        if (currentBomb == null)
        {
            currentBomb = Object.FindObjectOfType<Bomb>();
            if (currentBomb != null && debugModeEnabled)
            {
                currentBomb.SetKnockbackDebugMode(debugModeEnabled);
            }
        }
    }
    
    private void UpdateDebugMode()
    {
        var bombs = Object.FindObjectsOfType<Bomb>();
        foreach (var bomb in bombs)
        {
            bomb.SetKnockbackDebugMode(debugModeEnabled);
        }
        
        var calculators = Object.FindObjectsOfType<KnockbackCalculator>();
        foreach (var calc in calculators)
        {
            calc.SetDebugMode(debugModeEnabled);
        }
    }
    
    void OnGUI()
    {
        if (!debugModeEnabled) return;
        
        GUI.Label(new Rect(10, 10, 300, 20), "Knockback Debug Mode: ON (Press F1 to toggle)");
        
        var players = Object.FindObjectsOfType<PlayerLifeManager>();
        int yOffset = 40;
        
        foreach (var player in players)
        {
            string info = $"Player {player.PlayerNumber}: {player.PercentageKnockback:F1}% knockback";
            GUI.Label(new Rect(10, yOffset, 300, 20), info);
            yOffset += 25;
        }
        
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