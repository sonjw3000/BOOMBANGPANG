using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static Resources;

[System.Serializable]
public class ObjectData
{
    public int type;
    public int x, y, z;
    public int head;
    public ObjectData(int x,int y, int z, int type)
    {
        this.type = type;
        this.x = x;
        this.y = y;
        this.z = z;
        this.head = 0;
    }

    public ObjectData(int x, int y, int z, int type, int head)
    {
        this.type = type;
        this.x = x;
        this.y = y;
        this.z = z;
        this.head = head;
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
    public List<Material[]> originalMats;

    public List<int3> GetBuildRange(int type)   // 배치된 타입에 따라 범위를 리턴해주는 함수
    {
        List<int3> result = new List<int3>();
        //int3 coord = new int3();
        switch (type)
        {
            case 2:
                break;
            case 3:
                break;
            case 4:
                break;
            case 5:
                break;
        }
        return result;
    }
    public List<int3> GetBuildRange()
    {
        return GetBuildRange(type);
    }

    public void Reset()
    {
        type = 0;
        if (obj != null)
        {
            UnityEngine.Object.Destroy(obj);
            obj = null;
        }
        if(originalMats != null)
        {
            originalMats.Clear();
        }
    }
}

public class Resources : MonoBehaviour
{
    public class RendererTemplate
	{
		// "path" -> sharedMaterials
		public Dictionary<string, Material[]> pathToMaterials = new Dictionary<string, Material[]>();
	}

   public Dictionary<int, RendererTemplate> IndexToMaterials = new Dictionary<int, RendererTemplate>();

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

        BuildRendererTemplates();

    }

    private void BuildRendererTemplates()
    {
        IndexToMaterials.Clear();

        for (int i = 0; i < Prefabs.Length; ++i)
        {
            //foreach (var prefab in Prefabs)
            var rendererTpl = new RendererTemplate();
            var root = Prefabs[i].transform;
            var renderers = Prefabs[i].GetComponentsInChildren<Renderer>(true);

            foreach (var rend in renderers)
            {
                var stack = new Stack<string>();
                for (var cur = rend.transform; cur != root.transform; cur = cur.parent)
                    stack.Push(cur.name);

                string path = string.Join("/", stack);
                rendererTpl.pathToMaterials[path] = rend.sharedMaterials;
            }

            IndexToMaterials[i] = rendererTpl;
        }
    }

    // Update is called once per frame
    //void Update()
    //{
    //}

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
