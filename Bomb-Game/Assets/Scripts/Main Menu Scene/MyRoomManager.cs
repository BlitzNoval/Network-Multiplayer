using UnityEngine;
using Mirror;

public class MyRoomManager : NetworkRoomManager
{
    // Makes this easy to grab from anywhere
    public static MyRoomManager Singleton { get; private set; }

    [Header("Room Matching")]
    // This is the name the host picks (shows up in the lobby)
    public string RoomName = "DefaultRoom";
    // This is what a client types in when they want to join
    [HideInInspector]
    public string DesiredRoomName;

    // Runs when the object wakes up (before anything else)
    public override void Awake()
    {
        base.Awake();        // Let Mirror set itself up
        Singleton = this;    // Store our singleton reference
    }

    // Fired on the client if we ever get disconnected from the room scene
    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();  // Mirror’s built-in clean up
        Debug.Log("[MyRoomManager] Disconnected from room – showing error panel");

        // Try to find our main menu UI in the scene so we can show the “bad room” message  (doesn't really work, but I rtried with some interesting code I found haha)
        #if UNITY_2023_1_OR_NEWER //like bruh
            var menuUI = Object.FindFirstObjectByType<MainMenuUI>();
        #else 
            var menuUI = Object.FindObjectOfType<MainMenuUI>();
        #endif

        // If we found it, pop up the error panel; otherwise, log an error (It literally never finds it, but I tried)
        if (menuUI != null)
            menuUI.ShowInvalidRoomPanel();
        else
            Debug.LogError("Couldn’t find MainMenuUI to display the invalid-room panel!");
    }
}
