using System.Collections.Generic;
using UnityEngine;

public class Picking : MonoBehaviour
{
    private Resources resources;
    private MapJson map;
    public GameObject[] buildingPrefab;
    private int buildingPrefabIndex;
    private GameObject previewInstance;
    private Dictionary<Vector2Int, GameObject> buildings;
    private Dictionary<int, RobotData> robots;
    public Material wireframeMat;
    private UIOnOff activate;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resources = GameObject.Find("Resources").GetComponent<Resources>();
        activate = GameObject.Find("ESC").GetComponent<UIOnOff>();

        if (resources == null)
        {
            Debug.LogError("Resources not found!");
        }
        else
        {
            map = resources.mapRef;
            buildings = resources.buildingsRef;
            robots = resources.robotsRef;
        }
        if (map == null)
        {
            Debug.LogError("mapRef is null!");
        }
        if (buildingPrefab == null)
        {
            Debug.LogError("buildingPrefab is null!");
        }
        else
        {
            SyncPreviewAndBuilding();
            previewInstance.name = "Preview";
            previewInstance.SetActive(false);
        }
        buildingPrefabIndex = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (!activate.activateRef)
        {
            KeyboardInput();
            MousePicking();
        }
    }

    void MousePicking()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // y=0 평면
        float distance;

        if (groundPlane.Raycast(ray, out distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);

            // 월드 좌표 → 타일 인덱스
            int tileX = Mathf.FloorToInt(worldPos.x + 0.5f);
            int tileZ = Mathf.FloorToInt(worldPos.z + 0.5f);
            Vector2Int v2i = new Vector2Int(tileZ, tileX);

            //Debug.Log($"마우스로{tileX},{tileZ}를 클릭");
            // 배열 범위 체크
            Vector3 placePos = new Vector3(tileX, buildingPrefab[buildingPrefabIndex].transform.position.y, tileZ);

            if (tileX >= 0 && tileX < map.rows && tileZ >= 0 && tileZ < map.cols)
            {
                if (map.data[tileZ * map.cols + tileX] == 0) // 바닥이면
                {
                    //Debug.Log("바닥인디요");
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        r.material.color = new Color(0, 1, 0, 0.3f);
                    }
                }
                else
                {
                    //Debug.Log("바닥이 아닌디요");
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach(Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        r.material.color = new Color(1, 0, 0, 0.3f);
                    }
                }

                if (Input.GetMouseButtonDown(0)) //Input.GetMouseButton(0)
                {      // 좌클릭 했을 때
                    //Debug.Log($"{placePos}를 클릭했담");
                    Transform parentTransform; 
                    switch(map.data[tileZ * map.cols + tileX])
                    {
                        case 0: // 바닥
                            if (buildingPrefabIndex <= 1)
                            {
                                parentTransform = GameObject.Find("TileParent").transform;
                                buildings[v2i] = Instantiate(buildingPrefab[buildingPrefabIndex], placePos, buildingPrefab[buildingPrefabIndex].transform.rotation, parentTransform);
                            }
                            else
                            {
                                parentTransform = GameObject.Find("RobotParent").transform;

                                GameObject robot = Instantiate(buildingPrefab[buildingPrefabIndex], placePos, buildingPrefab[buildingPrefabIndex].transform.rotation, parentTransform);
                                int id = resources.getNewRobotID();
                                FindRoute findRoute = robot.GetComponent<FindRoute>();
                                findRoute.enabled = true;
                                findRoute.robotIndex = id;

                                RobotData robotdata = new RobotData();
                                robotdata.id = id;
                                robotdata.x = tileX;
                                robotdata.z = tileZ;
                                robotdata.type = buildingPrefabIndex;
                                robots[id] = robotdata;
                            }
                            map.data[tileZ * map.cols + tileX] = buildingPrefabIndex;
                            //Debug.Log($"벽 생성: ({tileX}, {tileZ})");
                            break;
                        case 1: // 벽
                            Destroy(buildings[v2i]);
                            buildings.Remove(v2i);
                            map.data[tileZ * map.cols + tileX] = 0;
                            break;
                        default:    // 로봇
                            break;
                    }
                }
            }
            else
            {// 타일의 범위를 넘어섰다면 preview disable
                previewInstance.SetActive(false);
            }
        }
    }

    void KeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            buildingPrefabIndex = 1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            buildingPrefabIndex = 2;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            buildingPrefabIndex = 3;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            buildingPrefabIndex = 4;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            buildingPrefabIndex = 5;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            buildingPrefabIndex = 6;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            buildingPrefabIndex = 7;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            buildingPrefabIndex = 8;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            buildingPrefabIndex = 9;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            buildingPrefabIndex = 0;
        }
        SyncPreviewAndBuilding();
    }

    void SyncPreviewAndBuilding()
    {
        if (previewInstance)
        {
            Destroy(previewInstance);
        }
        previewInstance = Instantiate(buildingPrefab[buildingPrefabIndex]);
        foreach(Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
        {
            r.material = wireframeMat;
        }
    }
}
