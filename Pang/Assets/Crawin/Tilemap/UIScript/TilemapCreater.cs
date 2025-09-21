using NUnit.Framework;
using System;
using System.IO;
using UnityEngine;

public class TilemapCreater : MonoBehaviour
{
    public enum MapType
    {
        Empty,
        Custom,
        Random
    }
    public MapType type;
    public int rows;
    public int cols;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CreateMap()
    {
        MapJson mapJson = new MapJson();
        string json;
        string outputPath;
        switch (type)
        {
            case (MapType.Empty):
                mapJson.rows = rows;
                mapJson.cols = cols;
                mapJson.data = new int[rows * cols];
                for(int y = 0; y < rows; ++y)
                {
                    for (int x = 0; x < cols; ++x)
                    {
                        int index = y * cols + x;
                        mapJson.data[index] = 0;
                    }
                }

                json = JsonUtility.ToJson(mapJson);
                outputPath = Path.Combine(Application.dataPath, "mapdata.json");
                File.WriteAllText(outputPath, json);
                break;
            case (MapType.Custom):

                break;
            case (MapType.Random):
                mapJson.rows = rows;
                mapJson.cols = cols;
                mapJson.data = new int[rows * cols];
                for (int y = 0; y < rows; ++y)
                {
                    for (int x = 0; x < cols; ++x)
                    {
                        int index = y * cols + x;
                        mapJson.data[index] = UnityEngine.Random.Range(0, 2);
                    }
                }

                json = JsonUtility.ToJson(mapJson);
                outputPath = Path.Combine(Application.dataPath, "mapdata.json");
                File.WriteAllText(outputPath, json);
                break;
        }
        Debug.Log("Export Complete");
    }
}
