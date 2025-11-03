using System;
using System.IO;
using UnityEngine;

public class TilemapSave : MonoBehaviour
{
    private Resources resources;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resources = GameObject.Find("Resources").GetComponent<Resources>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ExportMap()
    {
        MapJson mapJson = new MapJson();
        mapJson.X = resources.mapSize.x;
        mapJson.Y = resources.mapSize.y;
        mapJson.Z = resources.mapSize.z;

        Cell[,,] map = resources.mapRef;
        if (map == null)
        {
            Debug.Log("map이 없네");
        }
        for (int y = 0; y < mapJson.Y; ++y)
        {
            for (int x = 0; x < mapJson.X; ++x)
            {
                for (int z = 0; z < mapJson.Z; ++z)
                {
                    switch (map[x, y, z].type)
                    {
                        case 0:
                        case int.MaxValue:
                            //이동중인 출발 타일 위치이거나 통로이면 저장 x
                            continue;
                        case 1:
                            float head = Mathf.Round(map[x, y, z].obj.transform.eulerAngles.y / 90f);
                            ObjectData od = new ObjectData(x, y, z, 1, (int)head);
                            mapJson.buildingData.Add(od);
                            break;

                        default:
                            float h = Mathf.Round(map[x, y, z].obj.transform.eulerAngles.y / 90f);
                            ObjectData rd = new ObjectData(x, y, z, map[x, y, z].type, (int)h);
                            mapJson.robotdata.Add(rd);
                            //Debug.Log("로봇 찾았당");
                            break;
                    }
                }
            }

            string outputPath = Path.Combine(Application.dataPath, "currentmap.json");
            string result = JsonUtility.ToJson(mapJson, true);
            File.WriteAllText(outputPath, result);
            Debug.Log("Export Json");
        }
    }
}
