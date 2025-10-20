using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Picking : MonoBehaviour
{
    private Resources resources;
    private Cell[,,] map;
    private int3 mapSize;
    private int buildingPrefabIndex;
    public ref int IndexRef => ref buildingPrefabIndex;
    private GameObject previewInstance;
    public Material wireframeMat;
    private UIOnOff activate;

    public GameObject RightClickMenu;
    private Animator RightClickMenuAnimator;
    private int3 RightClickedCoord;
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
            mapSize = resources.mapSize;
        }
        if (map == null)
        {
            Debug.LogError("mapRef is null!");
        }
        SyncPreviewAndBuilding();
        previewInstance.name = "Preview";
        previewInstance.SetActive(false);
        buildingPrefabIndex = 0;

        if (RightClickMenu)
        {
            RightClickMenuAnimator = RightClickMenu.GetComponent<Animator>();
        }
        RightClickedCoord = new int3();
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
            //Debug.Log(worldPos);
            // 월드 좌표 → 타일 인덱스
            int tileX = Mathf.FloorToInt(worldPos.x+0.5f);
            int tileZ = Mathf.FloorToInt(worldPos.z+0.5f);

            //Debug.Log($"마우스로{tileX},{tileZ}를 클릭");
            // 배열 범위 체크
            Vector3 placePos = new Vector3(tileX, resources.Prefabs[buildingPrefabIndex].transform.position.y, tileZ);

            if (tileX >= 0 && tileX < mapSize.x && tileZ >= 0 && tileZ < mapSize.z)
            {
                if (map[tileX,0,tileZ].type == 0) // 바닥이면
                {
                    //Debug.Log("바닥인디요");
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.materials;
                        for(int i = 0; i <mats.Length; ++i)
                        {
                            mats[i].color = new Color(0, 1, 0, 0.3f);
                        }
                        //r.material.color = new Color(0, 1, 0, 0.3f);
                    }
                }
                else
                {
                    //Debug.Log("바닥이 아닌디요");
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach(Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.materials;
                        for (int i = 0; i < mats.Length; ++i)
                        {
                            mats[i].color = new Color(1, 0, 0, 0.3f);
                        }
                        //r.material.color = new Color(1, 0, 0, 0.3f);
                    }
                }

                if (Input.GetMouseButtonDown(0)) //Input.GetMouseButton(0)
                {      // 좌클릭 했을 때
                    RightClickMenu.SetActive(false);
                    //Debug.Log($"{placePos}를 클릭했담");
                    Transform parentTransform; 
                    switch(map[tileX,0,tileZ].type)
                    {
                        case 0: // 바닥
                            if (buildingPrefabIndex <= 1)   // 기둥이거나 타일이면
                            {
                                parentTransform = GameObject.Find("TileParent").transform;
                                map[tileX, 0, tileZ].obj = Instantiate(resources.Prefabs[buildingPrefabIndex], placePos, resources.Prefabs[buildingPrefabIndex].transform.rotation, parentTransform);
                            }
                            else
                            {
                                parentTransform = GameObject.Find("RobotParent").transform;
                                map[tileX, 0, tileZ].obj = Instantiate(resources.Prefabs[buildingPrefabIndex], placePos, resources.Prefabs[buildingPrefabIndex].transform.rotation, parentTransform);

                                FindRoute findroute = map[tileX, 0, tileZ].obj.GetComponent<FindRoute>();
                                if (findroute != null)
                                {
                                    findroute.type = buildingPrefabIndex;
                                    findroute.enabled = true;
                                }
                            }
                            map[tileX, 0, tileZ].type = buildingPrefabIndex;
                            //Debug.Log($"벽 생성: ({tileX}, {tileZ})");
                            break;
                        case 1: // 벽
                            Destroy(map[tileX, 0, tileZ].obj);
                            map[tileX, 0, tileZ].obj = null;
                            map[tileX, 0, tileZ].type = 0;
                            break;
                        default:    // 로봇
                            break;
                    }
                }

                if (Input.GetMouseButtonDown(1))
                {
                    Vector3 mousePos = Input.mousePosition;
                    RightClickMenu.SetActive(true);
                    RightClickMenu.transform.position = mousePos;
                    RightClickMenuAnimator.ResetTrigger("Clicked");
                    RightClickMenuAnimator.SetTrigger("Clicked");
                    RightClickedCoord.x = tileX; RightClickedCoord.y = 0; RightClickedCoord.z = tileZ;
                    Debug.Log("우클릭"+RightClickedCoord.x + "," + RightClickedCoord.z);
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
        previewInstance = Instantiate(resources.Prefabs[buildingPrefabIndex]);
        foreach(Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; ++i)
            {
                mats[i] = wireframeMat;
            }
            r.materials = mats;
        }
    }
}
