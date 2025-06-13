using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TutorialPanelManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialButton
    {
        public Button button;
        [TextArea(3, 10)]
        public string textContent;
    }

    [Header("Panel References")]
    public GameObject textDisplayPanel;
    public TextMeshProUGUI displayText;
    public Button backButton;
    
    [Header("Tutorial Buttons")]
    public List<TutorialButton> tutorialButtons = new List<TutorialButton>();
    
    [Header("Typing Animation Settings")]
    public float typingSpeed = 0.05f;
    public bool skipTypingOnClick = true;
    
    // Private variables
    private bool isTyping = false;
    private bool panelWasClosed = false;
    private string currentText = "";
    private string lastDisplayedText = "";
    private Coroutine typingCoroutine;
    
    private void Start()
    {
        SetupButtons();
        InitializePanel();
    }
    
    private void SetupButtons()
    {
        // Setup tutorial buttons
        foreach (TutorialButton tutButton in tutorialButtons)
        {
            if (tutButton.button != null)
            {
                tutButton.button.onClick.AddListener(() => OnTutorialButtonClicked(tutButton.textContent));
            }
        }
        
        // Setup back button
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }
        
        // Setup text clicking to skip typing
        if (displayText != null && skipTypingOnClick)
        {
            // Add a button component to the text if it doesn't exist
            Button textButton = displayText.GetComponent<Button>();
            if (textButton == null)
            {
                textButton = displayText.gameObject.AddComponent<Button>();
                textButton.transition = Selectable.Transition.None; // No visual transition
            }
            textButton.onClick.AddListener(SkipTyping);
        }
    }
    
    private void InitializePanel()
    {
        // Hide the text panel initially
        if (textDisplayPanel != null)
        {
            textDisplayPanel.SetActive(false);
        }
        
        // Clear display text
        if (displayText != null)
        {
            displayText.text = "";
        }
        
        panelWasClosed = false;
    }
    
    public void OnTutorialButtonClicked(string textToDisplay)
    {
        // Show the panel if it's hidden
        if (textDisplayPanel != null && !textDisplayPanel.activeInHierarchy)
        {
            textDisplayPanel.SetActive(true);
        }
        
        // If this is the same text that's already displayed and we're not coming back from a closed panel, don't retype
        if (textToDisplay == lastDisplayedText && !panelWasClosed && !isTyping)
        {
            return; // Don't retype the same text
        }
        
        // Stop any current typing animation
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        
        // Start typing the new text
        currentText = textToDisplay;
        lastDisplayedText = textToDisplay;
        panelWasClosed = false; // Reset the closed flag since we're displaying new text
        typingCoroutine = StartCoroutine(TypeText(textToDisplay));
    }
    
    public void OnBackButtonClicked()
    {
        // Mark that the panel was closed
        panelWasClosed = true;
        
        // Stop typing animation if running
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        // Hide the panel
        if (textDisplayPanel != null)
        {
            textDisplayPanel.SetActive(false);
        }
        
        // Clear the text
        if (displayText != null)
        {
            displayText.text = "";
        }
        
        isTyping = false;
    }
    
    public void OnPanelOpened()
    {
        // Call this method when the tutorial panel is opened from elsewhere
        // This will trigger re-animation if the panel was previously closed
        
        if (panelWasClosed && !string.IsNullOrEmpty(currentText))
        {
            // Re-animate the last displayed text
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeText(currentText));
            panelWasClosed = false;
        }
    }
    
    private void SkipTyping()
    {
        if (isTyping && !string.IsNullOrEmpty(currentText))
        {
            // Stop the typing coroutine
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            
            // Display the full text immediately
            if (displayText != null)
            {
                displayText.text = currentText;
            }
            
            isTyping = false;
        }
    }
    
    private IEnumerator TypeText(string textToType)
    {
        isTyping = true;
        
        if (displayText != null)
        {
            displayText.text = "";
            
            for (int i = 0; i <= textToType.Length; i++)
            {
                displayText.text = textToType.Substring(0, i);
                yield return new WaitForSeconds(typingSpeed);
            }
        }
        
        isTyping = false;
        typingCoroutine = null;
    }
    
    // Public methods for external control
    public void SetTypingSpeed(float speed)
    {
        typingSpeed = speed;
    }
    
    public bool IsTyping()
    {
        return isTyping;
    }
    
    public void ClearText()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        if (displayText != null)
        {
            displayText.text = "";
        }
        
        currentText = "";
        isTyping = false;
    }
    
    // Method to manually trigger text display (useful for testing)
    public void DisplayText(string text)
    {
        OnTutorialButtonClicked(text);
    }
}