using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Picking : MonoBehaviour
{
    public GameObject mapParent;
    TilemapGenerator gen;
    private MapJson map;
    public GameObject[] buildingPrefab;
    private int buildingPrefabIndex;
    private GameObject previewInstance;
    private Dictionary<Vector2Int, GameObject> buildings;
    public Material wireframeMat;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (mapParent == null)
        {
            Debug.LogError("mapParent is null!");
        }
        else
        {
            gen = mapParent.GetComponent<TilemapGenerator>();
        }
        if (gen == null)
        {
            Debug.LogError("TilemapGenerator component not found!");
        }
        else
        {
            map = gen.mapRef;
            buildings = gen.buildingsRef;
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
        KeyboardInput();
        MousePicking();
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
            Vector3 placePos = new Vector3(tileX, -previewInstance.GetComponent<Renderer>().bounds.min.y, tileZ);

            Debug.Log(placePos.y);
            //y값이 0.5인 이유는 wall의 pivot이 0.5위에 있어서인데 이를 어케 해결할까...
            if (tileX >= 0 && tileX < map.rows && tileZ >= 0 && tileZ < map.cols)
            {
                if (map.data[tileZ * map.cols + tileX] == 0) // 바닥이면
                {
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        r.material.color = new Color(0, 1, 0, 0.3f);
                    }
                }
                else
                {
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach(Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        r.material.color = new Color(1, 0, 0, 0.3f);
                    }
                }

                if (Input.GetMouseButton(0))
                {      // 좌클릭 했을 때
                    //Debug.Log($"{placePos}를 클릭했담");
                    Transform parentTransform = mapParent.transform.Find("TileParent");
                    if (map.data[tileZ * map.cols + tileX] == 0) // 바닥이면
                    {
                        buildings[v2i] = Instantiate(buildingPrefab[buildingPrefabIndex], placePos, Quaternion.identity, parentTransform);
                        Debug.Log($"벽 생성: ({tileX}, {tileZ})");
                        map.data[tileZ * map.cols + tileX] = 1;
                    }
                    else if (map.data[tileZ * map.cols + tileX] == 1)
                    {
                        Debug.Log($"벽 제거: ({tileX}, {tileZ})");
                        Destroy(buildings[v2i]);
                        buildings.Remove(v2i);
                        map.data[tileZ * map.cols + tileX] = 0;
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
            buildingPrefabIndex = 0;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            buildingPrefabIndex = 1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            buildingPrefabIndex = 2;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            buildingPrefabIndex = 3;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            buildingPrefabIndex = 4;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            buildingPrefabIndex = 5;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            buildingPrefabIndex = 6;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            buildingPrefabIndex = 7;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            buildingPrefabIndex = 8;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            buildingPrefabIndex = 9;
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
