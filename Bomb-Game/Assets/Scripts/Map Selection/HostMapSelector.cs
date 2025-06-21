using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror; 

public class HostMapSelector : MonoBehaviour
{
    [SerializeField] private Button cityButton;
    [SerializeField] private Button islandButton;
    [SerializeField] private Button shipButton;
    [SerializeField] private TextMeshProUGUI selectedMapText;
    [SerializeField] private GameObject hostPanel;
    
    [SerializeField] private MapCollection mapCollection;
    
    private string selectedMap = "City";
    
    void Start()
    {
        SetupHostUI();
    }
    
    void SetupHostUI()
    {
        if (hostPanel != null)
            hostPanel.SetActive(true);
            
        if (cityButton != null)
            cityButton.onClick.AddListener(() => SelectMap("City"));
        if (islandButton != null)
            islandButton.onClick.AddListener(() => SelectMap("Island"));
        if (shipButton != null)
            shipButton.onClick.AddListener(() => SelectMap("Ship"));
        
        SelectMap("City");
    }
    
    public void SelectMap(string mapName)
    {
        if (!NetworkServer.active)
        {
            Debug.Log("Only host can select maps!");
            return;
        }
        
        selectedMap = mapName;
        
        if (NetworkServer.active)
        {
            if (MyRoomManager.Singleton != null)
            {
                MyRoomManager.Singleton.selectedMapName = mapName;
                Debug.Log($"Host selected map: {mapName}");
            }
            
            UpdateUIForSelection(mapName);
        }
    }
    
    void UpdateUIForSelection(string mapName)
    {
        if (selectedMapText != null)
            selectedMapText.text = $"Selected: {mapName}";
        
        UpdateButtonColors();
    }
    
    void UpdateButtonColors()
    {
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