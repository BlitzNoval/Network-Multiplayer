using UnityEngine;

[System.Serializable]
public class MapSpawnData
{
    public string mapName;
    public GameObject mapPrefab;
    
    public GameObject floorReference;
    
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