using UnityEngine;
using System.Collections;

public class MapPreviewController : MonoBehaviour
{
    [Header("Map Preview Setup")]
    [SerializeField] private MapCollection mapCollection;
    [SerializeField] private Transform mapPreviewPosition; // Where to spawn the maps
    [SerializeField] private Camera previewCamera;
    
    [Header("Camera Rotation Settings")]
    [SerializeField] private float rotationSpeed = 30f; // Degrees per second
    [SerializeField] private float cameraDistance = 15f;
    [SerializeField] private float cameraHeight = 8f;
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero; // Offset from map center to look at
    
    [Header("Map Swapping Settings")]
    [SerializeField] private float mapDisplayTime = 5f; // How long to show each map
    [SerializeField] private float transitionTime = 1f; // Time to fade between maps
    
    [Header("Selected Map Display")]
    [SerializeField] private float selectedMapDisplayTime = 3f; // How long to show selected map
    
    private GameObject[] mapInstances = new GameObject[3];
    private int currentMapIndex = 0;
    private bool isShowingSelectedMap = false;
    private string selectedMapName = "";
    private Coroutine rotationCoroutine;
    private Coroutine swappingCoroutine;
    
    // Events
    public System.Action OnPreviewStarted;
    public System.Action OnPreviewFinished;
    
    void Start()
    {
        SetupMapPreview();
        StartMapPreview();
    }
    
    void SetupMapPreview()
    {
        if (mapCollection == null || mapPreviewPosition == null)
        {
            Debug.LogError("MapPreviewController: Missing required references!");
            return;
        }
        
        // Instantiate all maps at the preview position (initially inactive)
        for (int i = 0; i < mapCollection.maps.Length && i < 3; i++)
        {
            var mapData = mapCollection.maps[i];
            if (mapData.mapPrefab != null)
            {
                mapInstances[i] = Instantiate(mapData.mapPrefab, mapPreviewPosition.position, mapPreviewPosition.rotation);
                mapInstances[i].SetActive(false);
                
                // Remove any NetworkIdentity components since this is just for preview
                var networkIdentities = mapInstances[i].GetComponentsInChildren<Mirror.NetworkIdentity>();
                foreach (var netId in networkIdentities)
                {
                    DestroyImmediate(netId);
                }
                
                Debug.Log($"Instantiated map preview: {mapData.mapName}");
            }
        }
        
        // Position camera
        if (previewCamera != null)
        {
            SetupCamera();
        }
    }
    
    void SetupCamera()
    {
        // Position camera at the specified distance and height
        Vector3 mapCenter = mapPreviewPosition.position + lookAtOffset;
        Vector3 cameraPos = mapCenter + Vector3.back * cameraDistance + Vector3.up * cameraHeight;
        
        previewCamera.transform.position = cameraPos;
        previewCamera.transform.LookAt(mapCenter);
    }
    
    public void StartMapPreview()
    {
        if (isShowingSelectedMap) return;
        
        OnPreviewStarted?.Invoke();
        
        // Start with the first map
        ShowMap(0);
        
        // Start rotation
        if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
        rotationCoroutine = StartCoroutine(RotateCamera());
        
        // Start map swapping
        if (swappingCoroutine != null) StopCoroutine(swappingCoroutine);
        swappingCoroutine = StartCoroutine(SwapMaps());
    }
    
    public void ShowSelectedMap(string mapName)
    {
        selectedMapName = mapName;
        isShowingSelectedMap = true;
        
        // Stop swapping and rotation
        if (swappingCoroutine != null) StopCoroutine(swappingCoroutine);
        if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
        
        // Find and show the selected map
        for (int i = 0; i < mapCollection.maps.Length; i++)
        {
            if (mapCollection.maps[i].mapName == mapName)
            {
                ShowMap(i);
                
                // Start a slower, more focused rotation on the selected map
                rotationCoroutine = StartCoroutine(RotateAroundSelectedMap());
                
                // Auto-finish after display time
                StartCoroutine(FinishSelectedMapDisplay());
                break;
            }
        }
    }
    
    void ShowMap(int mapIndex)
    {
        // Hide all maps
        for (int i = 0; i < mapInstances.Length; i++)
        {
            if (mapInstances[i] != null)
                mapInstances[i].SetActive(false);
        }
        
        // Show the selected map
        if (mapIndex >= 0 && mapIndex < mapInstances.Length && mapInstances[mapIndex] != null)
        {
            mapInstances[mapIndex].SetActive(true);
            currentMapIndex = mapIndex;
            
            string mapName = mapIndex < mapCollection.maps.Length ? mapCollection.maps[mapIndex].mapName : "Unknown";
            Debug.Log($"Showing map preview: {mapName}");
        }
    }
    
    System.Collections.IEnumerator RotateCamera()
    {
        while (!isShowingSelectedMap)
        {
            // Rotate around the map center
            Vector3 mapCenter = mapPreviewPosition.position + lookAtOffset;
            previewCamera.transform.RotateAround(mapCenter, Vector3.up, rotationSpeed * Time.deltaTime);
            previewCamera.transform.LookAt(mapCenter);
            
            yield return null;
        }
    }
    
    System.Collections.IEnumerator RotateAroundSelectedMap()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < selectedMapDisplayTime)
        {
            // Slower rotation for selected map
            Vector3 mapCenter = mapPreviewPosition.position + lookAtOffset;
            previewCamera.transform.RotateAround(mapCenter, Vector3.up, (rotationSpeed * 0.5f) * Time.deltaTime);
            previewCamera.transform.LookAt(mapCenter);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
    
    System.Collections.IEnumerator SwapMaps()
    {
        while (!isShowingSelectedMap)
        {
            yield return new WaitForSeconds(mapDisplayTime);
            
            if (isShowingSelectedMap) break;
            
            // Transition to next map
            yield return StartCoroutine(TransitionToNextMap());
        }
    }
    
    System.Collections.IEnumerator TransitionToNextMap()
    {
        // Simple transition - could be enhanced with fade effects
        int nextMapIndex = (currentMapIndex + 1) % 3;
        
        // Quick fade transition (optional - can be enhanced)
        yield return new WaitForSeconds(transitionTime * 0.5f);
        
        ShowMap(nextMapIndex);
        
        yield return new WaitForSeconds(transitionTime * 0.5f);
    }
    
    System.Collections.IEnumerator FinishSelectedMapDisplay()
    {
        yield return new WaitForSeconds(selectedMapDisplayTime);
        FinishPreview();
    }
    
    public void FinishPreview()
    {
        // Stop all coroutines
        if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
        if (swappingCoroutine != null) StopCoroutine(swappingCoroutine);
        
        // Hide all maps
        for (int i = 0; i < mapInstances.Length; i++)
        {
            if (mapInstances[i] != null)
                mapInstances[i].SetActive(false);
        }
        
        OnPreviewFinished?.Invoke();
        
        Debug.Log("Map preview finished");
    }
    
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }
    
    public void SetMapDisplayTime(float time)
    {
        mapDisplayTime = time;
    }
    
    void OnDestroy()
    {
        // Clean up instantiated maps
        for (int i = 0; i < mapInstances.Length; i++)
        {
            if (mapInstances[i] != null)
                DestroyImmediate(mapInstances[i]);
        }
    }
}