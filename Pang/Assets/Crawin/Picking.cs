using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Picking : MonoBehaviour
{
    private Resources resources;
    private Cell[,,] map;
    private int3 mapSize;
    private int buildingPrefabIndex;
    private int syncPrefabIndex;
    public ref int IndexRef => ref buildingPrefabIndex;
    private GameObject previewInstance;
    public Material wireframeMat;
    private UIOnOff activate;

    public GameObject RightClickMenu;
    private Animator RightClickMenuAnimator;
    private int3 RightClickedCoord;
    private GameObject RightClickedObject;

    private int head;
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
        previewInstance.SetActive(false);
        buildingPrefabIndex = 0;
        syncPrefabIndex = 0;

        if (RightClickMenu)
        {
            RightClickMenuAnimator = RightClickMenu.GetComponent<Animator>();
            RightClickMenuAnimator.enabled = false;
            RightClickMenu.SetActive(false);
            //Debug.Log("분명 끔");
        }
        RightClickedCoord = new int3();
        RightClickedObject = null;
        head = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (!activate.activateRef)
        {
            KeyboardInput();
            MousePicking();
        }
        //Debug.Log($"Preview activeSelf={previewInstance.activeSelf}, buildingPrefabIndex={buildingPrefabIndex}");

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
            int tileX = Mathf.FloorToInt(worldPos.x + 0.5f);
            int tileZ = Mathf.FloorToInt(worldPos.z + 0.5f);

            //Debug.Log($"마우스로{tileX},{tileZ}를 클릭");
            // 배열 범위 체크
            Vector3 placePos = new Vector3(tileX, resources.Prefabs[buildingPrefabIndex].transform.position.y, tileZ);

            if (tileX >= 0 && tileX < mapSize.x && tileZ >= 0 && tileZ < mapSize.z)
            {
                if (buildingPrefabIndex > 1)    // 배치 될 프리팹 보여주기
                {
                    SyncPreviewAndBuilding();
                    if (map[tileX, 0, tileZ].type == 0) // 바닥이면
                    {
                        //Debug.Log("바닥인디요");
                        previewInstance.SetActive(true);
                        previewInstance.transform.position = placePos;
                        foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                        {
                            var mats = r.materials;
                            for (int i = 0; i < mats.Length; ++i)
                            {
                                mats[i].color = new Color(0, 1, 0, 0.3f);
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log("바닥이 아닌디요");
                        previewInstance.SetActive(true);
                        previewInstance.transform.position = placePos;
                        foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                        {
                            var mats = r.materials;
                            for (int i = 0; i < mats.Length; ++i)
                            {
                                mats[i].color = new Color(1, 0, 0, 0.3f);
                            }
                        }
                    }
                }

                if (Input.GetMouseButtonDown(0))
                {      // 좌클릭 했을 때
                    if (!IsPointerOverUI()) // ui를 안건드렸으면
                    {
                        if (RightClickedObject != null)
                        {
                            Renderer[] renderers = RightClickedObject.GetComponentsInChildren<Renderer>();
                            var originalMats = map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].originalMats;
                            if (originalMats != null && originalMats.Count == renderers.Length)
                            {
                                for (int i = 0; i < renderers.Length; ++i)
                                {
                                    renderers[i].sharedMaterials = originalMats[i];
                                }
                            }
                            map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].originalMats = null;
                            RightClickedObject = null;
                        }

                        RightClickMenu.SetActive(false);
                        //Debug.Log($"{placePos}를 클릭했담");
                        Transform parentTransform;
                        switch (map[tileX, 0, tileZ].type)
                        {
                            case 0: // 바닥
                                quaternion baseRot = resources.Prefabs[buildingPrefabIndex].transform.rotation * Quaternion.Euler(0, 90 * head, 0);
                                if (buildingPrefabIndex <= 1)   // 기둥이거나 타일이면
                                {
                                    parentTransform = GameObject.Find("TileParent").transform;
                                    map[tileX, 0, tileZ].obj = Instantiate(resources.Prefabs[buildingPrefabIndex], placePos, baseRot, parentTransform);
                                }
                                else
                                {
                                    parentTransform = GameObject.Find("RobotParent").transform;
                                    map[tileX, 0, tileZ].obj = Instantiate(resources.Prefabs[buildingPrefabIndex], placePos, baseRot, parentTransform);

                                    FindRoute findroute = map[tileX, 0, tileZ].obj.GetComponent<FindRoute>();
                                    if (findroute != null)
                                    {
                                        findroute.type = buildingPrefabIndex;
                                        findroute.enabled = true;
                                        // type을 입력해준 다음 enabled 를 해야지 벽 뚫는현상 방지
                                    }
                                }
                                map[tileX, 0, tileZ].type = buildingPrefabIndex;
                                //Debug.Log($"벽 생성: ({tileX}, {tileZ})");
                                break;
                            case 1: // 벽
                                //Destroy(map[tileX, 0, tileZ].obj);
                                //map[tileX, 0, tileZ].obj = null;
                                //map[tileX, 0, tileZ].type = 0;
                                break;
                            default:    // 로봇
                                break;
                        }
                    }
                }

                if (Input.GetMouseButtonDown(1))
                {
                    if (RightClickedObject != null) // 이전에 선택된 애가 있다면 메터리얼 원상복귀
                    {
                        Renderer[] renderers = RightClickedObject.GetComponentsInChildren<Renderer>();
                        var originalMats = map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].originalMats;
                        if (originalMats != null && originalMats.Count == renderers.Length)
                        {
                            for (int i = 0; i < renderers.Length; ++i)
                            {
                                renderers[i].sharedMaterials = originalMats[i];
                            }
                        }
                        map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].originalMats = null;
                        RightClickedObject = null;
                    }

                    RightClickedCoord.x = tileX; RightClickedCoord.y = 0; RightClickedCoord.z = tileZ;

                    // 기존 메테리얼들 저장, 와이어프레임으로 변경
                    RightClickedObject = map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].obj;
                    if (RightClickedObject != null)
                    {
                        //Debug.Log(RightClickedObject.name + "선택완료");
                        Renderer[] renderers = RightClickedObject.GetComponentsInChildren<Renderer>();
                        map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].originalMats = new List<Material[]>();
                        foreach (Renderer renderer in renderers)
                        {
                            //기존 메테리얼들 저장
                            map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].originalMats.Add(renderer.sharedMaterials);

                            //메테리얼 교체
                            Material[] newMats = new Material[renderer.materials.Length];
                            for (int i = 0; i < newMats.Length; ++i)
                            {
                                newMats[i] = wireframeMat;
                            }
                            renderer.materials = newMats;
                        }
                    }

                    Vector3 mousePos = Input.mousePosition;
                    RightClickMenu.SetActive(false);
                    RightClickMenu.SetActive(true);
                    // 껏다키는 이유는 자식들도 껏다키기위함
                    RightClickMenuAnimator.enabled = true;
                    RightClickMenu.transform.position = mousePos;
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
        else if (Input.GetKeyDown(KeyCode.Q))   // 반시계 90도 회전
        {
            if (head == 0)
            {
                head = 3;
            }
            else
            {
                head = (head - 1) % 4;
            }
        }
        else if (Input.GetKeyDown(KeyCode.E))   // 시계 90도 회전
        {
            head = (head + 1) % 4;
        }
        //SyncPreviewAndBuilding();
    }

    void SyncPreviewAndBuilding()
    {
        if (syncPrefabIndex != buildingPrefabIndex || previewInstance == null)
        {
            Destroy(previewInstance);
            previewInstance = Instantiate(resources.Prefabs[buildingPrefabIndex]);
            previewInstance.name = "Preview";
            foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; ++i)
                {
                    mats[i] = wireframeMat;
                }
                r.materials = mats;
            }

            Component[] components = previewInstance.GetComponents<Component>();
            foreach (var comp in components)
            {
                switch (comp)
                {
                    case Transform:
                        continue;
                    case FindRoute:
                        ((FindRoute)comp).type = buildingPrefabIndex;
                        ((FindRoute)comp).enabled = false;
                        break;
                    case Human:
                        ((Human)comp).enabled = false;
                        break;
                    default:
                        Debug.LogError("프리뷰 컴포넌트 끄는 중에 등록되지 않은 컴포넌트 발견!" + comp);
                        break;
                }
            }
            syncPrefabIndex = buildingPrefabIndex;
        }
        int prev_head = ((Mathf.RoundToInt(previewInstance.transform.eulerAngles.y / 90f) % 4) + 4) % 4;
        if (prev_head != head)
        {
            previewInstance.transform.Rotate(0, (head - prev_head) * 90, 0);
        }
    }

    bool IsPointerOverUI()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        GraphicRaycaster gr = RightClickMenu.GetComponentInParent<GraphicRaycaster>();
        gr.Raycast(pointerData, results);
        return results.Count > 0;
    }

    public void RemoveObject()
    {
        if (RightClickedObject != null)
        {
            FindRoute fr = RightClickedObject.GetComponent<FindRoute>();
            if (fr != null)
            {//움직이는 로봇들
                fr.RemoveThisObjectOnMap();
            }
            else
            {//가만히 배치돼있는 애들
                map[RightClickedCoord.x, RightClickedCoord.y, RightClickedCoord.z].Reset();
            }
            Destroy(RightClickedObject);
            RightClickedObject = null;
        }
        RightClickMenu.gameObject.SetActive(false);
    }

    public void InsertClicked()
    {

    }
}
