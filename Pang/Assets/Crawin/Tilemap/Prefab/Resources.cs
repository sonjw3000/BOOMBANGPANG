using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class ObjectData
{
    public int type;
    public int x, y, z;
    public ObjectData(int x,int y, int z, int type)
    {
        this.type = type;
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

[System.Serializable]
public class MapJson
{
    public int X,Y,Z;
    public List<ObjectData> buildingData;
    public List<ObjectData> robotdata;
    public MapJson()
    {
        buildingData = new List<ObjectData>();
        robotdata = new List<ObjectData>();
    }
}

public class Cell
{
    public int type;
    public GameObject obj;
}

public class Resources : MonoBehaviour
{
    public TextAsset mapJsonFile;
    public GameObject[] Prefabs;
    private MapJson mapJson;
    public ref MapJson mapJsonRef => ref mapJson;

    [HideInInspector]
    public int3 mapSize;

    private Cell[,,] map;
    public ref Cell[,,] mapRef => ref map;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mapJson = JsonUtility.FromJson<MapJson>(mapJsonFile.text);
        mapSize = new int3(mapJson.X, mapJson.Y, mapJson.Z);
        map = new Cell[mapSize.x, mapSize.y, mapSize.z];
        for(int y = 0; y < mapSize.y; ++y)
        {
            for(int x = 0; x < mapSize.x; ++x)
            {
                for(int z = 0; z < mapSize.z; ++z)
                {
                    map[x, y, z] = new Cell();
                    map[x, y, z].type = 0;
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnValidate()
    {

    }

    public int getNewRobotID()
    {
        int cnt = 0;
        //while (robots.ContainsKey(cnt)) {
        //    ++cnt;
        //}
        return cnt;
    }
}
