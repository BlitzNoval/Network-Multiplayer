using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class SimpleEmoticonPanel : MonoBehaviour
{
    [Header("Emoticon Buttons")]
    [SerializeField] Button button1;
    [SerializeField] Button button2;
    [SerializeField] Button button3;
    
    [Header("Emoticon Display Images")]
    [SerializeField] Image[] emoticonImages = new Image[3]; // Assign your 3 emoticon images here
    
    [Header("Animation Settings")]
    [SerializeField] float wiggleDuration = 1f;
    [SerializeField] float wiggleAngle = 15f; // How far to rotate left and right
    [SerializeField] int wiggleCount = 3; // How many back-and-forth wiggles
    
    [Header("Player Assignment")]
    [SerializeField] int playerNumber = 1; // Set this to 1, 2, 3, or 4 for each panel
    
    // Static registry to track all panels even when inactive
    private static Dictionary<int, SimpleEmoticonPanel> playerPanels = new Dictionary<int, SimpleEmoticonPanel>();
    
    public static SimpleEmoticonPanel GetPanelForPlayer(int playerNum)
    {
        if (playerPanels.ContainsKey(playerNum))
        {
            SimpleEmoticonPanel panel = playerPanels[playerNum];
            if (panel != null)
            {
                Debug.Log($"Found SimpleEmoticonPanel for player {playerNum}");
                return panel;
            }
            else
            {
                Debug.LogWarning($"Panel for player {playerNum} was destroyed, removing from registry");
                playerPanels.Remove(playerNum);
            }
        }
        
        Debug.LogError($"No SimpleEmoticonPanel registered for player {playerNum}. Registered players: {string.Join(", ", playerPanels.Keys)}");
        return null;
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DebugPrintRegisteredPanels()
    {
        Debug.Log($"Registered SimpleEmoticonPanels: {string.Join(", ", playerPanels.Keys)}");
    }
    
    void Awake()
    {
        // Register this panel in the static registry
        if (playerPanels.ContainsKey(playerNumber))
        {
            Debug.LogWarning($"Player {playerNumber} already has a SimpleEmoticonPanel registered! Overwriting...");
        }
        playerPanels[playerNumber] = this;
        Debug.Log($"Registered SimpleEmoticonPanel for player {playerNumber}");
        
        // Hide panel by default
        gameObject.SetActive(false);
        
        // Make sure all emoticon images are inactive
        for (int i = 0; i < emoticonImages.Length; i++)
        {
            if (emoticonImages[i] != null)
                emoticonImages[i].gameObject.SetActive(false);
        }
        
        // Set up button listeners
        SetupButtons();
    }
    
    void SetupButtons()
    {
        if (button1 != null)
            button1.onClick.AddListener(() => OnEmoticonButtonClicked(0));
        
        if (button2 != null)
            button2.onClick.AddListener(() => OnEmoticonButtonClicked(1));
        
        if (button3 != null)
            button3.onClick.AddListener(() => OnEmoticonButtonClicked(2));
    }
    
    public void ShowPanel()
    {
        gameObject.SetActive(true);
        Debug.Log("SimpleEmoticonPanel shown");
    }
    
    public void HidePanel()
    {
        gameObject.SetActive(false);
        Debug.Log("SimpleEmoticonPanel hidden");
    }
    
    void OnEmoticonButtonClicked(int emoticonIndex)
    {
        Debug.Log($"Emoticon button {emoticonIndex + 1} clicked on Player {playerNumber} panel!");
        
        // Hide the panel immediately
        HidePanel();
        
        // Show the emoticon animation locally first
        ShowEmoticonAnimation(emoticonIndex);
        
        // Tell the local player to send network command
        PlayerMovement localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.SelectEmoticon(emoticonIndex);
        }
        else
        {
            Debug.LogError("Could not find local player to send emoticon command!");
        }
    }
    
    public void ShowEmoticonAnimation(int emoticonIndex)
    {
        if (emoticonIndex < 0 || emoticonIndex >= emoticonImages.Length) return;
        if (emoticonImages[emoticonIndex] == null) return;
        
        Debug.Log($"Playing emoticon wiggle animation for index {emoticonIndex}");
        
        Image emoticon = emoticonImages[emoticonIndex];
        emoticon.gameObject.SetActive(true);
        
        // Store original rotation
        Vector3 originalRotation = emoticon.transform.localEulerAngles;
        
        // Create wiggle sequence
        Sequence wiggleSequence = DOTween.Sequence();
        
        // Calculate time per wiggle (each wiggle is left-center-right-center)
        float timePerWiggle = wiggleDuration / wiggleCount;
        float quarterTime = timePerWiggle / 4f;
        
        for (int i = 0; i < wiggleCount; i++)
        {
            // Wiggle left
            wiggleSequence.Append(emoticon.transform.DORotate(
                new Vector3(originalRotation.x, originalRotation.y, originalRotation.z - wiggleAngle), 
                quarterTime).SetEase(Ease.InOutSine));
            
            // Back to center
            wiggleSequence.Append(emoticon.transform.DORotate(
                originalRotation, 
                quarterTime).SetEase(Ease.InOutSine));
            
            // Wiggle right
            wiggleSequence.Append(emoticon.transform.DORotate(
                new Vector3(originalRotation.x, originalRotation.y, originalRotation.z + wiggleAngle), 
                quarterTime).SetEase(Ease.InOutSine));
            
            // Back to center
            wiggleSequence.Append(emoticon.transform.DORotate(
                originalRotation, 
                quarterTime).SetEase(Ease.InOutSine));
        }
        
        // When animation completes, reset and deactivate
        wiggleSequence.OnComplete(() => {
            emoticon.transform.localEulerAngles = originalRotation;
            emoticon.gameObject.SetActive(false);
            Debug.Log($"Emoticon wiggle animation complete for index {emoticonIndex}");
        });
    }
    
    PlayerMovement FindLocalPlayer()
    {
        PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();
        foreach (PlayerMovement player in players)
        {
            if (player.isLocalPlayer)
                return player;
        }
        return null;
    }
    
    void OnDestroy()
    {
        // Unregister this panel when destroyed
        if (playerPanels.ContainsKey(playerNumber) && playerPanels[playerNumber] == this)
        {
            playerPanels.Remove(playerNumber);
            Debug.Log($"Unregistered SimpleEmoticonPanel for player {playerNumber}");
        }
    }
}