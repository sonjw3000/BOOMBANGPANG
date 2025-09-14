using System.IO;
using UnityEngine;

public class TilemapSave : MonoBehaviour
{
    public GameObject mapParent;
    TilemapGenerator gen;
    private MapJson map;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gen = mapParent.GetComponent<TilemapGenerator>();
        map = gen.mapRef;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void ExportMap()
    {
        string json = JsonUtility.ToJson(map);
        MapJson copyMap = JsonUtility.FromJson<MapJson>(json);

        for(int i = 0; i < copyMap.data.Length; ++i)
        {
            if (copyMap.data[i] == 2)
                copyMap.data[i] = 0;
        }
        json = JsonUtility.ToJson(copyMap);
        string outputPath = Path.Combine(Application.dataPath, "currentmap.json");
        File.WriteAllText(outputPath, json);
        Debug.Log("Export Json");
    }
}
