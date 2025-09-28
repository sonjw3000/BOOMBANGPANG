using System;
using Unity.Mathematics;
using UnityEngine;

public class TilemapGenerator: MonoBehaviour
{
    private GameObject tileParent;
    private GameObject robotParent;
    private Resources resources;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resources = GameObject.Find("Resources").GetComponent<Resources>();
        if (resources == null)
        {
            Debug.LogError("no Resources Object");
        }
        else
        {
            GenerateMap();
        }
    }

    // Update is called once per frame
    void Update()
    {
        //printMap();
    }

    void clearObjectParents()
    {
        if (tileParent != null)
        {
            DestroyImmediate(tileParent);
        }
        else
        {
            GameObject old = GameObject.Find("TileParent");
            if (old != null)
            {
                DestroyImmediate(old);
            }
        }
        tileParent = new GameObject("TileParent");
        tileParent.transform.parent = transform;

        if (robotParent != null)
        {
            DestroyImmediate(robotParent);
            Debug.Log("robotParent Delete");
        }
        else
        {
            GameObject old = GameObject.Find("RobotParent");
            if (old != null)
            {
                DestroyImmediate(old);
            }
        }
        robotParent = new GameObject("RobotParent");
        robotParent.transform.parent = transform;
    }

    void GenerateMap()
    {
        clearObjectParents();

        Cell[,,] map = resources.mapRef;
        if(map == null)
        {
            Debug.Log("너가 문제였구나");
        }
        GameObject[] Prefabs = resources.Prefabs;
        int3 mapSize = resources.mapSize;
        // X*Z 크기의 커다란 QUAD 하나 생성 및 배치
        GameObject Tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Tile.transform.parent = tileParent.transform;

        Tile.transform.localScale = new Vector3Int(mapSize.x, mapSize.z, mapSize.y);
        Tile.transform.Rotate(90, 0, 0);
        Tile.transform.position = new Vector3(mapSize.x / 2 - 0.5f, 0, mapSize.z / 2-0.5f); // Y값 수정 요망 09.28  ( 층수가 다른 quad 배치해야함 )

        //for (int y = 0; y < mapSize.y; ++y)
        //{
        //    for(int x = 0; x < mapSize.x; ++x)
        //    {
        //        for(int z = 0; z < mapSize.z; ++z)
        //        {
        //            map[x, y, z].type = 0;
        //            Vector3 pos = new Vector3(x+0.5f, Prefabs[0].transform.position.y, z+0.5f);
        //            map[x, y, z].obj = Instantiate(Prefabs[0], pos, Prefabs[0].transform.rotation, tileParent.transform);
        //            map[x, y, z].obj.name = $"{x},{y},{z}";
        //        }
        //    }
        //}

        // 맵에 빌딩 배치
        foreach (ObjectData buildingData in resources.mapJsonRef.buildingData)
        {
            map[buildingData.x,buildingData.y,buildingData.z].type = buildingData.type;
            Vector3 pos = new Vector3(buildingData.x, Prefabs[buildingData.type].transform.position.y, buildingData.z);
            map[buildingData.x,buildingData.y,buildingData.z].obj = Instantiate(Prefabs[buildingData.type], pos, Prefabs[buildingData.type].transform.rotation, tileParent.transform);
        }
        resources.mapJsonRef.buildingData.Clear();

        // 맵에 로봇 배치
        foreach (ObjectData robotData in resources.mapJsonRef.robotdata)
        {
            map[robotData.x, robotData.y, robotData.z].type = robotData.type;
            Vector3 pos = new Vector3(robotData.x, Prefabs[robotData.type].transform.position.y, robotData.z);
            map[robotData.x, robotData.y, robotData.z].obj = Instantiate(Prefabs[robotData.type], pos, Prefabs[robotData.type].transform.rotation, robotParent.transform);

            FindRoute findroute = map[robotData.x, robotData.y, robotData.z].obj.GetComponent<FindRoute>();
            findroute.type = robotData.type;
            findroute.enabled = true;
        }
        resources.mapJsonRef.robotdata.Clear(); // 딕셔너리로 다 옮겼으니 초기화하자
    }

    void printMap()
    {
        Cell[,,] map = resources.mapRef;
        string field = "";
        for (int y = 0; y < resources.mapSize.y; ++y)
        {
            for (int z = 0; z < resources.mapSize.z; ++z)
            {
                for (int x = 0; x < resources.mapSize.x; ++x)
                {
                    field += map[x, y, z].type + " ";
                }
                field += "\n";
            }
        }
        Debug.Log(field);
    }
}
