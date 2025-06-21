using UnityEngine;
using System.Collections;

public class MapPreviewController : MonoBehaviour   
{
    [SerializeField] private MapCollection mapCollection;
    [SerializeField] private Transform mapPreviewPosition;
    [SerializeField] private Camera previewCamera;
    
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private float cameraDistance = 15f;
    [SerializeField] private float cameraHeight = 8f;
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero;
    
    [SerializeField] private float mapDisplayTime = 5f;
    [SerializeField] private float transitionTime = 1f;
    
    [SerializeField] private float selectedMapDisplayTime = 3f;
    
    private GameObject[] mapInstances = new GameObject[3];
    private int currentMapIndex = 0;
    private bool isShowingSelectedMap = false;
    private string selectedMapName = "";
    private Coroutine rotationCoroutine;
    private Coroutine swappingCoroutine;
    
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
        
        for (int i = 0; i < mapCollection.maps.Length && i < 3; i++)
        {
            var mapData = mapCollection.maps[i];
            if (mapData.mapPrefab != null)
            {
                mapInstances[i] = Instantiate(mapData.mapPrefab, mapPreviewPosition.position, mapPreviewPosition.rotation);
                mapInstances[i].SetActive(false);
                
                var networkIdentities = mapInstances[i].GetComponentsInChildren<Mirror.NetworkIdentity>();
                foreach (var netId in networkIdentities)
                {
                    DestroyImmediate(netId);
                }
                
                Debug.Log($"Instantiated map preview: {mapData.mapName}");
            }
        }
        
        if (previewCamera != null)
        {
            SetupCamera();
        }
    }
    
    void SetupCamera()
    {
        Vector3 mapCenter = mapPreviewPosition.position + lookAtOffset;
        Vector3 cameraPos = mapCenter + Vector3.back * cameraDistance + Vector3.up * cameraHeight;
        
        previewCamera.transform.position = cameraPos;
        previewCamera.transform.LookAt(mapCenter);
    }
    
    public void StartMapPreview()
    {
        if (isShowingSelectedMap) return;
        
        OnPreviewStarted?.Invoke();
        
        ShowMap(0);
        
        if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
        rotationCoroutine = StartCoroutine(RotateCamera());
        
        if (swappingCoroutine != null) StopCoroutine(swappingCoroutine);
        swappingCoroutine = StartCoroutine(SwapMaps());
    }
    
    public void ShowSelectedMap(string mapName)
    {
        selectedMapName = mapName;
        isShowingSelectedMap = true;
        
        if (swappingCoroutine != null) StopCoroutine(swappingCoroutine);
        if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
        
        for (int i = 0; i < mapCollection.maps.Length; i++)
        {
            if (mapCollection.maps[i].mapName == mapName)
            {
                ShowMap(i);
                
                rotationCoroutine = StartCoroutine(RotateAroundSelectedMap());
                
                StartCoroutine(FinishSelectedMapDisplay());
                break;
            }
        }
    }
    
    void ShowMap(int mapIndex)
    {
        for (int i = 0; i < mapInstances.Length; i++)
        {
            if (mapInstances[i] != null)
                mapInstances[i].SetActive(false);
        }
        
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
            
            yield return StartCoroutine(TransitionToNextMap());
        }
    }
    
    System.Collections.IEnumerator TransitionToNextMap()
    {
        int nextMapIndex = (currentMapIndex + 1) % 3;
        
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
        if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
        if (swappingCoroutine != null) StopCoroutine(swappingCoroutine);
        
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
        for (int i = 0; i < mapInstances.Length; i++)
        {
            if (mapInstances[i] != null)
                DestroyImmediate(mapInstances[i]);
        }
    }
}