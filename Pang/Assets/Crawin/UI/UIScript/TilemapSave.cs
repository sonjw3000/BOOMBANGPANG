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
        string json = JsonUtility.ToJson(resources.mapRef, true);
        MapJson copyMap = JsonUtility.FromJson<MapJson>(json);

        for(int i = 0; i < copyMap.data.Length; ++i)
        {
            if (copyMap.data[i] > 1)
            {
                //int x = i % copyMap.cols;
                //int z = i / copyMap.cols;
                //RobotData robot = new RobotData();
                //robot.x = x;
                //robot.z = z;
                //robot.type = copyMap.data[i];
                //copyMap.robotdata.Add(robot);
                //Debug.Log(robot.type + "작성 완료");
                copyMap.data[i] = 0;
            }
        }   // 타일을 다 수정했다.
        // 이제 dic에 있는 로봇들을 넣을 차례
        foreach (RobotData data in resources.robotsRef.Values)
        {
            copyMap.robotdata.Add(data);
        }

        json = JsonUtility.ToJson(copyMap,true);
        string outputPath = Path.Combine(Application.dataPath, "currentmap.json");
        File.WriteAllText(outputPath, json);
        Debug.Log("Export Json");
    }
}
