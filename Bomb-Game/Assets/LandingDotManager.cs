using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class LandingDotManager : MonoBehaviour
{
    private static LandingDotManager instance;
    public static LandingDotManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("LandingDotManager");
                instance = go.AddComponent<LandingDotManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("Landing Dot Settings")]
    [SerializeField] private GameObject landingDotPrefab;
    [SerializeField] private float dotDisplayDuration = 1f;
    
    private Dictionary<int, GameObject> playerLandingDots = new Dictionary<int, GameObject>();
    private Dictionary<int, Coroutine> dotTimers = new Dictionary<int, Coroutine>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void SetLandingDotPrefab(GameObject prefab)
    {
        landingDotPrefab = prefab;
    }

    public void ShowLandingDots(LandingDotData[] dotData)
    {
        if (landingDotPrefab == null)
        {
            Debug.LogWarning("Landing dot prefab not set!");
            return;
        }

        ClearAllLandingDots();

        foreach (var data in dotData)
        {
            ShowLandingDotForPlayer(data.position, data.playerNumber);
        }
    }

    private void ShowLandingDotForPlayer(Vector3 landingPosition, int playerNumber)
    {
        HideLandingDotForPlayer(playerNumber);

        GameObject dot = Instantiate(landingDotPrefab, landingPosition, Quaternion.identity);
        
        if (Camera.main != null)
        {
            dot.transform.LookAt(
                dot.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up
            );
        }

        playerLandingDots[playerNumber] = dot;

        if (dotTimers.ContainsKey(playerNumber))
        {
            StopCoroutine(dotTimers[playerNumber]);
        }
        dotTimers[playerNumber] = StartCoroutine(RemoveDotAfterDelay(playerNumber, dotDisplayDuration));
    }

    private IEnumerator RemoveDotAfterDelay(int playerNumber, float delay)
    {
        yield return new WaitForSeconds(delay);
        HideLandingDotForPlayer(playerNumber);
    }

    public void HideLandingDotForPlayer(int playerNumber)
    {
        if (dotTimers.TryGetValue(playerNumber, out Coroutine timer))
        {
            if (timer != null) StopCoroutine(timer);
            dotTimers.Remove(playerNumber);
        }

        if (playerLandingDots.TryGetValue(playerNumber, out GameObject dot))
        {
            if (dot != null) Destroy(dot);
            playerLandingDots.Remove(playerNumber);
        }
    }

    public void ClearAllLandingDots()
    {
        foreach (var timer in dotTimers.Values)
        {
            if (timer != null) StopCoroutine(timer);
        }
        dotTimers.Clear();

        foreach (var dot in playerLandingDots.Values)
        {
            if (dot != null) Destroy(dot);
        }
        playerLandingDots.Clear();
    }

    void OnDestroy()
    {
        ClearAllLandingDots();
    }
}