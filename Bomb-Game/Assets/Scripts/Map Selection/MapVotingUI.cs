using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Linq;

public class MapVotingUI : MonoBehaviour
{
    [SerializeField] private Button cityButton;
    [SerializeField] private Button islandButton;
    [SerializeField] private Button shipButton;
    
    [SerializeField] private GameObject cityPanel;
    [SerializeField] private GameObject islandPanel;
    [SerializeField] private GameObject shipPanel;
    
    [SerializeField] private GameObject[] playerVoteIcons = new GameObject[4];
    
    [SerializeField] private TextMeshProUGUI cityVoteText;
    [SerializeField] private TextMeshProUGUI islandVoteText;
    [SerializeField] private TextMeshProUGUI shipVoteText;
    
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject votingPanel;
    
    [SerializeField] private Transform randomSpinner;
    
    [SerializeField] private MapPreviewController mapPreviewController;
    
    [SerializeField] private string initialVoteText = "Vote for a map!";
    [SerializeField] private string playerVotedText = "You voted for {0}! (Press Ready when done)";
    [SerializeField] private string votingEndedText = "Voting has ended!";
    [SerializeField] private string notConnectedText = "Not connected to server!";
    [SerializeField] private string tieDetectedText = "TIE between {0}!";
    [SerializeField] private string breakingTieText = "Breaking tie randomly!";
    [SerializeField] private string mapSelectedText = "Map Selected: {0}! Starting game...";
    [SerializeField] private string selectedWinnerText = "Selected: {0}!";
    
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color votedButtonColor = Color.green;
    [SerializeField] private Color disabledButtonColor = Color.gray;
    [SerializeField] private Color winnerGoldColor = Color.yellow;
    
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
        if (cityButton != null)
            cityButton.onClick.AddListener(() => VoteForMap("City"));
        if (islandButton != null)
            islandButton.onClick.AddListener(() => VoteForMap("Island"));
        if (shipButton != null)
            shipButton.onClick.AddListener(() => VoteForMap("Ship"));
        
        UpdateVoteDisplay("0", "0", "0");
        
        if (votingPanel != null) votingPanel.SetActive(true);
        
        if (randomSpinner != null) randomSpinner.gameObject.SetActive(false);
        
        if (statusText != null) statusText.text = initialVoteText;
        
        HideAllVoteIcons();
    }
    
    void FindVotingManager()
    {
        votingManager = MapVotingManager.Instance;
        
        if (votingManager == null)
        {
            votingManager = FindObjectOfType<MapVotingManager>();
        }
        
        if (votingManager == null)
        {
            Debug.LogError("MapVotingManager not found! Make sure it exists in the scene with NetworkIdentity.");
            return;
        }
        
        Debug.Log("Found MapVotingManager - subscribing to events");
        
        votingManager.OnVoteCountUpdated += UpdateVoteDisplay;
        votingManager.OnMapSelected += OnMapSelected;
        votingManager.OnTieDetected += OnTieDetected;
        votingManager.OnRouletteStart += OnRouletteStart;
        votingManager.OnRouletteComplete += OnRouletteComplete;
        votingManager.OnPlayerVoteStatusUpdate += UpdatePlayerVoteIcons;
    }
    
    public void VoteForMap(string mapName)
    {
        if (!NetworkClient.isConnected)
        {
            if (statusText != null) statusText.text = notConnectedText;
            return;
        }
        
        if (votingManager != null && votingManager.votingComplete)
        {
            if (statusText != null) statusText.text = votingEndedText;
            return;
        }
        
        if (votingManager != null)
        {
            votingManager.CmdVoteForMap(mapName);
            myVote = mapName;
            hasVoted = true;
            
            UpdateButtonStates();
            if (statusText != null) statusText.text = string.Format(playerVotedText, mapName);
        }
    }
    
    void UpdateButtonStates()
    {
        UpdateButtonColor(cityButton, "City");
        UpdateButtonColor(islandButton, "Island");
        UpdateButtonColor(shipButton, "Ship");
        
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
        HighlightWinnerPanel(selectedMap);
        
        if (statusText != null) statusText.text = string.Format(mapSelectedText, selectedMap);
        
        if (randomSpinner != null) randomSpinner.gameObject.SetActive(false);
        
        SetButtonsInteractable(false);
        
        if (mapPreviewController != null)
        {
            mapPreviewController.ShowSelectedMap(selectedMap);
        }
        
        Debug.Log($"Map voting complete. Selected: {selectedMap}");
    }
    
    void OnTieDetected(string[] tiedMaps)
    {
        if (statusText != null) statusText.text = string.Format(tieDetectedText, string.Join(", ", tiedMaps));
        Debug.Log($"Tie detected between: {string.Join(", ", tiedMaps)}");
    }
    
    void OnRouletteStart()
    {
        if (statusText != null) statusText.text = breakingTieText;
        
        if (randomSpinner != null)
        {
            randomSpinner.gameObject.SetActive(true);
            StartCoroutine(SpinRandom());
        }
    }
    
    void OnRouletteComplete(string winner)
    {
        if (statusText != null) statusText.text = string.Format(selectedWinnerText, winner);
    }
    
    System.Collections.IEnumerator SpinRandom()
    {
        float spinDuration = 3f;
        float elapsedTime = 0f;
        
        while (elapsedTime < spinDuration)
        {
            if (randomSpinner != null)
            {
                randomSpinner.Rotate(0, 0, 360f * Time.deltaTime * 3f);
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
    
    void HighlightWinnerPanel(string winnerMap)
    {
        ResetPanelColors();
        
        GameObject winnerPanel = null;
        switch (winnerMap)
        {
            case "City":
                winnerPanel = cityPanel;
                break;
            case "Island":
                winnerPanel = islandPanel;
                break;
            case "Ship":
                winnerPanel = shipPanel;
                break;
        }
        
        if (winnerPanel != null)
        {
            var image = winnerPanel.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.color = winnerGoldColor;
            }
        }
    }
    
    void ResetPanelColors()
    {
        SetPanelColor(cityPanel, normalButtonColor);
        SetPanelColor(islandPanel, normalButtonColor);
        SetPanelColor(shipPanel, normalButtonColor);
    }
    
    void SetPanelColor(GameObject panel, Color color)
    {
        if (panel != null)
        {
            var image = panel.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.color = color;
            }
        }
    }
    
    public void ResetVoting()
    {
        hasVoted = false;
        myVote = "";
        
        if (votingPanel != null) votingPanel.SetActive(true);
        if (randomSpinner != null) randomSpinner.gameObject.SetActive(false);
        
        ResetPanelColors();
        
        SetButtonsInteractable(true);
        UpdateButtonStates();
        UpdateVoteDisplay("0", "0", "0");
        
        if (statusText != null) statusText.text = initialVoteText;
        
        HideAllVoteIcons();
    }
    
    void HideAllVoteIcons()
    {
        for (int i = 0; i < playerVoteIcons.Length; i++)
        {
            if (playerVoteIcons[i] != null)
                playerVoteIcons[i].SetActive(false);
        }
    }
    
    void ShowVoteIcon(int playerIndex, bool hasVoted)
    {
        if (playerIndex >= 0 && playerIndex < playerVoteIcons.Length && playerVoteIcons[playerIndex] != null)
        {
            playerVoteIcons[playerIndex].SetActive(hasVoted);
        }
    }
    
    void UpdatePlayerVoteIcons(int totalVotes)
    {
        int connectedPlayers = 2;
        
        if (Mirror.NetworkServer.active)
        {
            connectedPlayers = Mirror.NetworkServer.connections.Count;
        }
        else if (Mirror.NetworkClient.isConnected)
        {
            connectedPlayers = Mathf.Max(2, totalVotes);
        }
        
        for (int i = 0; i < playerVoteIcons.Length; i++)
        {
            if (i < totalVotes)
            {
                ShowVoteIcon(i, true);
            }
            else if (i < connectedPlayers)
            {
                ShowVoteIcon(i, false);
            }
            else
            {
                ShowVoteIcon(i, false);
            }
        }
        
        Debug.Log($"Updated vote icons: {totalVotes} votes from {connectedPlayers} players");
    }
    
    void OnDestroy()
    {
        if (votingManager != null)
        {
            votingManager.OnVoteCountUpdated -= UpdateVoteDisplay;
            votingManager.OnMapSelected -= OnMapSelected;
            votingManager.OnTieDetected -= OnTieDetected;
            votingManager.OnRouletteStart -= OnRouletteStart;
            votingManager.OnRouletteComplete -= OnRouletteComplete;
            votingManager.OnPlayerVoteStatusUpdate -= UpdatePlayerVoteIcons;
        }
    }
}