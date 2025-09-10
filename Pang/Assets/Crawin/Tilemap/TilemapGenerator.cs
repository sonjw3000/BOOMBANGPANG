using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class MapJson
{
    public int rows;
    public int cols;
    public int[] data;
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    
    }

    private void Awake()
    {
        if (jsonFile != null && regenerateMap)
        {
            GenerateMap();
        }
    }

    // Update is called once per frame
    void Update()
    {
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
        Debug.Log("Generate");
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
        for (int z = 0; z < map.rows; ++z)
        {
            for (int x = 0; x < map.cols; ++x)
            {
                int value = map.data[z * map.cols + x];
                Vector3 pos = new Vector3(x, 0, z);

                if (value == 1)
                    Instantiate(wallPrefab, pos, wallPrefab.transform.rotation, tileParent.transform);
                else
                    Instantiate(tilePrefab, pos, tilePrefab.transform.rotation, tileParent.transform);
            }
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
