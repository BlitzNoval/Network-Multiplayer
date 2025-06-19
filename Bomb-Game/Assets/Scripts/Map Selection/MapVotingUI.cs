using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class MapVotingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button cityButton;
    [SerializeField] private Button islandButton;
    [SerializeField] private Button shipButton;
    
    [Header("Vote Count Displays")]
    [SerializeField] private TextMeshProUGUI cityVoteText;
    [SerializeField] private TextMeshProUGUI islandVoteText;
    [SerializeField] private TextMeshProUGUI shipVoteText;
    
    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject votingPanel;
    [SerializeField] private GameObject resultsPanel;
    
    [Header("Colors")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color votedButtonColor = Color.green;
    [SerializeField] private Color disabledButtonColor = Color.gray;
    
    private MapVotingManager votingManager;
    private string myVote = "";
    private bool hasVoted = false;
    
    void Start()
    {
        InitializeUI();
        FindVotingManager();
    }
    
    void InitializeUI()
    {
        // Set up button listeners
        if (cityButton != null)
            cityButton.onClick.AddListener(() => VoteForMap("City"));
        if (islandButton != null)
            islandButton.onClick.AddListener(() => VoteForMap("Island"));
        if (shipButton != null)
            shipButton.onClick.AddListener(() => VoteForMap("Ship"));
        
        // Initialize vote count displays
        UpdateVoteDisplay("0", "0", "0");
        
        // Show voting panel by default
        if (votingPanel != null) votingPanel.SetActive(true);
        if (resultsPanel != null) resultsPanel.SetActive(false);
        
        // Set initial status
        if (statusText != null) statusText.text = "Vote for a map!";
    }
    
    void FindVotingManager()
    {
        // Try to find existing voting manager
        votingManager = FindObjectOfType<MapVotingManager>();
        
        if (votingManager == null)
        {
            Debug.LogWarning("MapVotingManager not found! Creating one...");
            // Create voting manager if it doesn't exist
            GameObject managerObj = new GameObject("MapVotingManager");
            votingManager = managerObj.AddComponent<MapVotingManager>();
        }
        
        // Subscribe to events
        votingManager.OnVoteCountUpdated += UpdateVoteDisplay;
        votingManager.OnMapSelected += OnMapSelected;
    }
    
    public void VoteForMap(string mapName)
    {
        if (hasVoted)
        {
            if (statusText != null) statusText.text = "You have already voted!";
            return;
        }
        
        if (!NetworkClient.isConnected)
        {
            if (statusText != null) statusText.text = "Not connected to server!";
            return;
        }
        
        // Send vote to server
        if (votingManager != null)
        {
            votingManager.CmdVoteForMap(mapName);
            myVote = mapName;
            hasVoted = true;
            
            UpdateButtonStates();
            if (statusText != null) statusText.text = $"You voted for {mapName}!";
        }
    }
    
    void UpdateButtonStates()
    {
        // Update button colors based on voting state
        UpdateButtonColor(cityButton, "City");
        UpdateButtonColor(islandButton, "Island");
        UpdateButtonColor(shipButton, "Ship");
        
        // Disable all buttons if voting is complete
        if (votingManager != null && votingManager.votingComplete)
        {
            SetButtonsInteractable(false);
        }
    }
    
    void UpdateButtonColor(Button button, string mapName)
    {
        if (button == null) return;
        
        ColorBlock colors = button.colors;
        
        if (hasVoted && myVote == mapName)
        {
            colors.normalColor = votedButtonColor;
        }
        else if (hasVoted || (votingManager != null && votingManager.votingComplete))
        {
            colors.normalColor = disabledButtonColor;
        }
        else
        {
            colors.normalColor = normalButtonColor;
        }
        
        button.colors = colors;
    }
    
    void SetButtonsInteractable(bool interactable)
    {
        if (cityButton != null) cityButton.interactable = interactable;
        if (islandButton != null) islandButton.interactable = interactable;
        if (shipButton != null) shipButton.interactable = interactable;
    }
    
    void UpdateVoteDisplay(string cityVotes, string islandVotes, string shipVotes)
    {
        if (cityVoteText != null) cityVoteText.text = $"City: {cityVotes}";
        if (islandVoteText != null) islandVoteText.text = $"Island: {islandVotes}";
        if (shipVoteText != null) shipVoteText.text = $"Ship: {shipVotes}";
        
        UpdateButtonStates();
    }
    
    void OnMapSelected(string selectedMap)
    {
        // Update UI to show results
        if (statusText != null) statusText.text = $"Map Selected: {selectedMap}!";
        
        if (votingPanel != null) votingPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(true);
        
        SetButtonsInteractable(false);
        
        // The room manager should handle scene transition
        Debug.Log($"Map voting complete. Selected: {selectedMap}");
    }
    
    // Public method to reset voting (for room restarts)
    public void ResetVoting()
    {
        hasVoted = false;
        myVote = "";
        
        if (votingPanel != null) votingPanel.SetActive(true);
        if (resultsPanel != null) resultsPanel.SetActive(false);
        
        SetButtonsInteractable(true);
        UpdateButtonStates();
        UpdateVoteDisplay("0", "0", "0");
        
        if (statusText != null) statusText.text = "Vote for a map!";
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (votingManager != null)
        {
            votingManager.OnVoteCountUpdated -= UpdateVoteDisplay;
            votingManager.OnMapSelected -= OnMapSelected;
        }
    }
}