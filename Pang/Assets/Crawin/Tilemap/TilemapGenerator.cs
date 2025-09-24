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
        if(resources == null)
        {
            Debug.LogError("no Resources Object");
        }
        GenerateMap();
    }

    // Update is called once per frame
    void Update()
    {
    }

    void GenerateMap()
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

        MapJson map = resources.mapRef;
        if(map == null)
        {
            Debug.Log("너가 문제였구나");
        }

        Vector2Int v2i = new Vector2Int();
        GameObject[] Prefabs = resources.Prefabs;
        for (int z = 0; z < map.rows; ++z)
        {
            for (int x = 0; x < map.cols; ++x)
            {
                int value = map.data[z * map.cols + x];
                v2i.x = z;
                v2i.y = x;
                Vector3 pos = new Vector3(x, Prefabs[value].transform.position.y, z);
                //일단 타일을 깔아
                //Instantiate(Prefabs[0], pos, Prefabs[0].transform.rotation, tileParent.transform);
                //if (value == 1) // 벽이면 타일 위에 벽을 설치해
                //{
                //    buildings[v2i] = Instantiate(Prefabs[1], pos, Prefabs[1].transform.rotation, tileParent.transform);
                //}

                resources.buildingsRef[v2i] = Instantiate(Prefabs[value], pos, Prefabs[value].transform.rotation, tileParent.transform);

            }
        }
        // 맵 배치가 끝났으니 이제 로봇들을 배치할 차례
        foreach (RobotData robotData in map.robotdata)
        {
            //map.data[robotData.z * map.cols + robotData.x] = robotData.type;
            Vector3 pos = new Vector3(robotData.x, Prefabs[robotData.type].transform.position.y, robotData.z);
            GameObject robot = Instantiate(Prefabs[robotData.type], pos, Prefabs[robotData.type].transform.rotation, robotParent.transform);
            FindRoute findRoute = robot.GetComponent<FindRoute>();
            findRoute.enabled = true;
            findRoute.robotIndex = robotData.id;
            resources.robotsRef[robotData.id] = robotData;
        }
        map.robotdata.Clear(); // 딕셔너리로 다 옮겼으니 초기화하자
    }

    public void printMap()
    {
        MapJson map = resources.mapRef;
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
