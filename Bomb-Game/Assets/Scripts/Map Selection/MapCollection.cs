using UnityEngine;

[System.Serializable]
public class MapSpawnData
{
    [Header("Map Information")]
    public string mapName;
    public GameObject mapPrefab;
    
    [Header("Floor Reference")]
    [Tooltip("The floor reference GameObject for this map")]
    public GameObject floorReference;
    
    [Header("Spawn Positions")]
    [Tooltip("Array of 4 spawn positions for players 1-4")]
    public Vector3[] spawnPositions = new Vector3[4];
    
    public MapSpawnData(string name)
    {
        mapName = name;
        spawnPositions = new Vector3[4];
    }
}

[CreateAssetMenu(fileName = "New Map Collection", menuName = "Map Selection/Map Collection")]
public class MapCollection : ScriptableObject
{
    [Header("Available Maps")]
    public MapSpawnData[] maps = new MapSpawnData[3];
    
    public MapSpawnData GetMapByName(string mapName)
    {
        foreach (var map in maps)
        {
            if (map.mapName == mapName)
                return map;
        }
        return null;
    }
    
    public MapSpawnData GetRandomMap()
    {
        if (maps.Length == 0) return null;
        return maps[Random.Range(0, maps.Length)];
    }
}