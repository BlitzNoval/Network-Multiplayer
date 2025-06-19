using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class HostMapSelector : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button cityButton;
    [SerializeField] private Button islandButton;
    [SerializeField] private Button shipButton;
    [SerializeField] private TextMeshProUGUI selectedMapText;
    [SerializeField] private GameObject hostPanel;
    
    [Header("Map Configuration")]
    [SerializeField] private MapCollection mapCollection;
    
    private string selectedMap = "City"; // Default to City
    
    void Start()
    {
        SetupHostUI(); // Show UI for everyone, but only host can actually select
    }
    
    void SetupHostUI()
    {
        if (hostPanel != null)
            hostPanel.SetActive(true);
            
        // Set up button listeners
        if (cityButton != null)
            cityButton.onClick.AddListener(() => SelectMap("City"));
        if (islandButton != null)
            islandButton.onClick.AddListener(() => SelectMap("Island"));
        if (shipButton != null)
            shipButton.onClick.AddListener(() => SelectMap("Ship"));
        
        // Set initial selection
        SelectMap("City");
    }
    
    public void SelectMap(string mapName)
    {
        // Only allow host to select maps
        if (!NetworkServer.active)
        {
            Debug.Log("Only host can select maps!");
            return;
        }
        
        selectedMap = mapName;
        
        // Update UI for everyone via RPC
        if (NetworkServer.active)
        {
            // Store selection in RoomManager
            if (MyRoomManager.Singleton != null)
            {
                MyRoomManager.Singleton.selectedMapName = mapName;
                Debug.Log($"Host selected map: {mapName}");
            }
            
            // Update UI locally
            UpdateUIForSelection(mapName);
        }
    }
    
    void UpdateUIForSelection(string mapName)
    {
        // Update UI
        if (selectedMapText != null)
            selectedMapText.text = $"Selected: {mapName}";
        
        // Update button colors
        UpdateButtonColors();
    }
    
    void UpdateButtonColors()
    {
        // Reset all buttons
        SetButtonColor(cityButton, selectedMap == "City");
        SetButtonColor(islandButton, selectedMap == "Island");
        SetButtonColor(shipButton, selectedMap == "Ship");
    }
    
    void SetButtonColor(Button button, bool isSelected)
    {
        if (button == null) return;
        
        ColorBlock colors = button.colors;
        colors.normalColor = isSelected ? Color.green : Color.white;
        button.colors = colors;
    }
    
    public string GetSelectedMap()
    {
        return selectedMap;
    }
}