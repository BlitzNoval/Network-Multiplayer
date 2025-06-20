using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class SimpleEmoticonPanel : MonoBehaviour
{
    [SerializeField] Button button1;
    [SerializeField] Button button2;
    [SerializeField] Button button3;
    
    [SerializeField] Image[] emoticonImages = new Image[3];
    
    [SerializeField] float wiggleDuration = 1f;
    [SerializeField] float wiggleAngle = 15f;
    [SerializeField] int wiggleCount = 3;
    
    private Sequence currentWiggleSequence;
    
    [SerializeField] int playerNumber = 1;
    
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
        if (playerPanels.ContainsKey(playerNumber))
        {
            Debug.LogWarning($"Player {playerNumber} already has a SimpleEmoticonPanel registered! Overwriting...");
        }
        playerPanels[playerNumber] = this;
        Debug.Log($"Registered SimpleEmoticonPanel for player {playerNumber}");
        
        gameObject.SetActive(false);
        
        for (int i = 0; i < emoticonImages.Length; i++)
        {
            if (emoticonImages[i] != null)
                emoticonImages[i].gameObject.SetActive(false);
        }
        
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
        
        HidePanel();
        
        PlayerMovement localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.isEmoticonPanelOpen = false;
            
            localPlayer.SelectEmoticon(emoticonIndex);
        }
        else
        {
            Debug.LogError("Could not find local player to send emoticon command!");
        }
        
        ShowEmoticonAnimation(emoticonIndex);
    }
    
    public void ShowEmoticonAnimation(int emoticonIndex)
    {
        if (emoticonIndex < 0 || emoticonIndex >= emoticonImages.Length) return;
        if (emoticonImages[emoticonIndex] == null) return;
        
        Debug.Log($"Playing emoticon wiggle animation for index {emoticonIndex}");
        
        if (currentWiggleSequence != null)
        {
            currentWiggleSequence.Kill();
            Debug.Log("Stopped previous emoticon animation");
        }
        
        for (int i = 0; i < emoticonImages.Length; i++)
        {
            if (emoticonImages[i] != null)
                emoticonImages[i].gameObject.SetActive(false);
        }
        
        Image emoticon = emoticonImages[emoticonIndex];
        emoticon.gameObject.SetActive(true);
        
        Vector3 originalRotation = emoticon.transform.localEulerAngles;
        
        currentWiggleSequence = DOTween.Sequence();
        
        float timePerWiggle = wiggleDuration / wiggleCount;
        float quarterTime = timePerWiggle / 4f;
        
        for (int i = 0; i < wiggleCount; i++)
        {
            currentWiggleSequence.Append(emoticon.transform.DORotate(
                new Vector3(originalRotation.x, originalRotation.y, originalRotation.z - wiggleAngle), 
                quarterTime).SetEase(Ease.InOutSine));
            
            currentWiggleSequence.Append(emoticon.transform.DORotate(
                originalRotation, 
                quarterTime).SetEase(Ease.InOutSine));
            
            currentWiggleSequence.Append(emoticon.transform.DORotate(
                new Vector3(originalRotation.x, originalRotation.y, originalRotation.z + wiggleAngle), 
                quarterTime).SetEase(Ease.InOutSine));
            
            currentWiggleSequence.Append(emoticon.transform.DORotate(
                originalRotation, 
                quarterTime).SetEase(Ease.InOutSine));
        }
        
        currentWiggleSequence.OnComplete(() => {
            emoticon.transform.localEulerAngles = originalRotation;
            emoticon.gameObject.SetActive(false);
            currentWiggleSequence = null;
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
        if (playerPanels.ContainsKey(playerNumber) && playerPanels[playerNumber] == this)
        {
            playerPanels.Remove(playerNumber);
            Debug.Log($"Unregistered SimpleEmoticonPanel for player {playerNumber}");
        }
    }
}