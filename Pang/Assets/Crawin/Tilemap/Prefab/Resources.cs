using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RobotData
{
    public int id;
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
    public List<RobotData> robotdata;
}

public class Resources : MonoBehaviour
{
    public TextAsset mapJson;
    public GameObject[] Prefabs;
    private MapJson map;
    public ref MapJson mapRef => ref map;

    private Dictionary<Vector2Int, GameObject> buildings;
    public ref Dictionary<Vector2Int, GameObject> buildingsRef => ref buildings;

    private Dictionary<int, RobotData> robots;
    public ref Dictionary<int, RobotData> robotsRef => ref robots;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        map = JsonUtility.FromJson<MapJson>(mapJson.text);
        buildings = new Dictionary<Vector2Int, GameObject>();
        robots = new Dictionary<int, RobotData>();
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
        while (robots.ContainsKey(cnt)) {
            ++cnt;
        }
        return cnt;
    }
}
