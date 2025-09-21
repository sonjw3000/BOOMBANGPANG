using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class RobotData
{
    public int x;
    public int z;
    public int type;
}

[System.Serializable]
public class MapJson
{
    public int rows;
    public int cols;
    public int[] data;
    public RobotData[] robotdata;
}

[ExecuteInEditMode]
public class TilemapGenerator: MonoBehaviour
{
    public TextAsset jsonFile;
    public GameObject tilePrefab;
    public GameObject wallPrefab;
    private bool regenerateMap = true;
    private GameObject tileParent;
    private MapJson map;
    public ref MapJson mapRef => ref map;

    private Dictionary<Vector2Int, GameObject> buildings;
    public ref Dictionary<Vector2Int,GameObject> buildingsRef => ref buildings;

    [Range(1,2)]
    public int Version;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    
    }

    private void Awake()
    {
        if (jsonFile != null && regenerateMap)
        {
            Debug.Log("Generator Awake");
            GenerateMap();
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("update");
        //printMap();
        if (jsonFile != null && regenerateMap)
        {
            GenerateMap();
        }
    }

    private void OnValidate()
    {
        regenerateMap = true;
    }

    void GenerateMap()
    {
        Debug.Log("Delete tileParent");
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
        map = JsonUtility.FromJson<MapJson>(jsonFile.text);
        buildings = new Dictionary<Vector2Int, GameObject>();

        Debug.Log("Generate V." + Version);
        switch (Version)
        {
            case 1:
                Vector2Int v2i = new Vector2Int();
                for (int z = 0; z < map.rows; ++z)
                {
                    for (int x = 0; x < map.cols; ++x)
                    {
                        int value = map.data[z * map.cols + x];
                        v2i.x = z;
                        v2i.y = x;
                        Vector3 pos = new Vector3(x, 0, z);
                        //일단 타일을 깔아
                        Instantiate(tilePrefab, pos, tilePrefab.transform.rotation, tileParent.transform);
                        if (value == 1) // 벽이면 타일 위에 벽을 설치해
                        {
                            pos.y = 0.5f;
                            buildings[v2i] = Instantiate(wallPrefab, pos, wallPrefab.transform.rotation, tileParent.transform);
                        }
                    }
                }
                break;
            case 2:


                break;
            default:
                Debug.LogError("GenerateMap Version Error!");
                break;
        }
        regenerateMap = false;
    }

    public void printMap()
    {
        string field = "";
        for (int z = 0; z < map.rows; ++z)
        {
            for (int x = 0; x < map.cols; ++x)
            {
                field += map.data[z * map.cols + x] + " ";
            }
            field += "\n";
        }
        Debug.Log(field);
    }
}
