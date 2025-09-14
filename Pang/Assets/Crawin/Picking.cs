using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Picking : MonoBehaviour
{
    public GameObject mapParent;
    TilemapGenerator gen;
    private MapJson map;
    public GameObject buildingPrefab;
    public GameObject previewPrefab;
    private GameObject previewInstance;
    private Dictionary<Vector2Int, GameObject> buildings;
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
        if(previewPrefab == null)
        {
            Debug.LogError("previewPrefab is null!");
        }
        else
        {
            previewInstance = Instantiate(previewPrefab);
            previewInstance.SetActive(false);
        }
        
    }

    // Update is called once per frame
    void Update()
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
            Vector3 placePos = new Vector3(tileX, 0.5f, tileZ);
            if (tileX >= 0 && tileX < map.rows && tileZ >= 0 && tileZ < map.cols)
            {
                if (map.data[tileZ * map.cols + tileX] == 0) // 바닥이면
                {
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    previewInstance.GetComponent<Renderer>().material.color = new Color(0,1,0,0.3f);
                }
                else
                {
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    previewInstance.GetComponent<Renderer>().material.color = new Color(1, 0, 0, 0.3f);
                }

                if (Input.GetMouseButtonDown(0)) {      // 좌클릭 했을 때
                    //Debug.Log($"{placePos}를 클릭했담");
                    if (map.data[tileZ * map.cols + tileX] == 0) // 바닥이면
                    {
                        buildings[v2i] = Instantiate(buildingPrefab, placePos, Quaternion.identity);
                        Debug.Log($"벽 생성: ({tileX}, {tileZ})");
                        map.data[tileZ * map.cols + tileX] = 1;
                    }
                    else if (map.data[tileZ * map.cols + tileX] == 1)
                    {
                        Debug.Log($"벽 제거: ({tileX}, {tileZ})");
                        Destroy(buildings[v2i]);
                        buildings.Remove(v2i);
                        //Vector3 placePos = new Vector3(tileX, 0, tileZ);
                        //Instantiate(buildingPrefab, placePos, Quaternion.identity);
                        //Debug.Log($"벽 제거: ({tileX}, {tileZ})");
                        map.data[tileZ * map.cols + tileX] = 0;
                    }
                }
            }
        }
    }
}
