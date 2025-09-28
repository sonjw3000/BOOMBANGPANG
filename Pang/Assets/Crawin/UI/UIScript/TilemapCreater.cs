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
    public int Xlength;
    public int Ylength;
    public int Zlength;

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
        mapJson.X = Xlength;
        mapJson.Y = Ylength;
        mapJson.Z = Zlength;
        switch (type)
        {
            case (MapType.Empty):

                json = JsonUtility.ToJson(mapJson);
                outputPath = Path.Combine(Application.dataPath, "mapdata.json");
                File.WriteAllText(outputPath, json);
                break;
            case (MapType.Custom):

                break;
            case (MapType.Random):
                for (int y = 0; y < Ylength; ++y)
                {
                    for (int z = 0; z < Zlength; ++z)
                    {
                        for (int x = 0; x < Xlength; ++x)
                        {
                            if (UnityEngine.Random.Range(0, 2) == 1)
                            {
                                ObjectData building = new ObjectData(x, y, z, 1);
                                mapJson.buildingData.Add(building);
                            }
                        }
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
