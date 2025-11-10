using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Picking : MonoBehaviour
{
    private Resources resources;
    private Cell[,,] map;
    private int3 mapSize;
    [HideInInspector]
    public int buildingPrefabIndex;
    private int syncPrefabIndex;
    private GameObject previewInstance;
    public Material wireframeMat;
    private UIOnOff activate;

    public GameObject RightClickMenu;
    private GameObject RemoveButton;
    private Animator RightClickMenuAnimator;
    private int3 m_i3SelectedCoord;
    private GameObject m_goSelectedObject;

    private int head;
    public enum PickingType
    {
        SELECT,
        INSERT,
        REMOVE
    }
    [HideInInspector]
    public PickingType m_PickingType;

    // MousePicking에서 쓰는 변수
    private Plane groundPlane;
    private Vector3 placePos;

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
            RemoveButton = RightClickMenu.transform.GetChild(1).gameObject;
            Debug.Log(RemoveButton.gameObject.name);
        }
        m_i3SelectedCoord = new int3();
        m_goSelectedObject = null;
        head = 0;
        m_PickingType = PickingType.SELECT;
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        placePos = new Vector3();
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
        if (Input.GetMouseButtonDown(1))
        {
            //ReturnSelectedObjectMat();

            Vector3 mousePos = Input.mousePosition;
            RightClickMenu.SetActive(false);
            if (m_goSelectedObject != null) { 
                RemoveButton.SetActive(true);
            }
            else
            {
                RemoveButton.SetActive(false);
            }
            RightClickMenu.SetActive(true);
            // 껏다키는 이유는 자식들도 껏다키기위함
            RightClickMenu.transform.position = mousePos;
            RightClickMenuAnimator.enabled = true;
        }

        switch (m_PickingType)
        {
            case PickingType.SELECT:
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        if (!IsPointerOverUI())
                        {
                            RightClickMenu.SetActive(false);
                            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            float distance;
                            if (groundPlane.Raycast(ray, out distance))
                            {
                                ReturnSelectedObjectMat();
                                Vector3 worldPos = ray.GetPoint(distance);
                                int tileX = Mathf.FloorToInt(worldPos.x + 0.5f);
                                int tileZ = Mathf.FloorToInt(worldPos.z + 0.5f);
                                if (map[tileX, 0, tileZ].obj != null)
                                {
                                    m_i3SelectedCoord.x = tileX; m_i3SelectedCoord.y = 0; m_i3SelectedCoord.z = tileZ;
                                    m_goSelectedObject = map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].obj;
                                    SaveSelectedObjectMat();

                                    // 해당 오브젝트의 속성 출력 예정
                                    Status st = m_goSelectedObject.gameObject.GetComponent<Status>();
                                    if(st != null)
                                    {
                                        st.OnClick();
                                    }
                                    Debug.Log(m_goSelectedObject.name + "이 선택 되었습니다.");
                                }
                                else
                                {   // 다른 뭔가가 좌클릭 되면 선택 해제
                                    m_goSelectedObject = null;
                                }
                            }
                            else
                            {   // 다른 뭔가가 좌클릭 되면 선택 해제
                                m_goSelectedObject = null;
                            }
                        }
                    }
                }
                break;
            case PickingType.INSERT:
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float distance;
                    if (groundPlane.Raycast(ray, out distance))
                    {
                        Vector3 worldPos = ray.GetPoint(distance);
                        int tileX = Mathf.FloorToInt(worldPos.x + 0.5f);
                        int tileZ = Mathf.FloorToInt(worldPos.z + 0.5f);
                        placePos.Set(tileX, resources.Prefabs[buildingPrefabIndex].transform.position.y, tileZ);

                        RenderPreview();

                        if (Input.GetMouseButtonDown(0))
                        {
                            if (!IsPointerOverUI()) // ui를 안건드렸으면
                            {
                                ReturnSelectedObjectMat();

                                RightClickMenu.SetActive(false);
                                Transform parentTransform;
                                if (map[tileX,0,tileZ].type == 0)   // 바닥에 아무것도 없으면
                                {
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

                                        Status st = map[tileX,0,tileZ].obj.GetComponent<Status>();
                                        if(st != null)
                                        {
                                            st.SetID(buildingPrefabIndex);
                                        }
                                    }

                                    map[tileX, 0, tileZ].type = buildingPrefabIndex;
                                    m_PickingType = PickingType.SELECT;
                                    previewInstance.SetActive(false);
                                }
                            }
                        }
                    }
                    else
                    {// 타일의 범위를 넘어섰다면 preview disable
                        previewInstance.SetActive(false);
                    }
                }
                break;
            case PickingType.REMOVE:
                {

                }
                break;
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

            //Component[] components = previewInstance.GetComponents<Component>();
            //foreach (var comp in components)
            //{
            //    switch (comp)
            //    {
            //        case Transform:
            //            continue;
            //        case FindRoute:
            //            //((FindRoute)comp).type = buildingPrefabIndex;
            //            ((FindRoute)comp).enabled = false;
            //            break;
            //        case Human:
            //            ((Human)comp).enabled = false;
            //            break;
            //        default:
            //            Debug.LogError("프리뷰 컴포넌트 끄는 중에 등록되지 않은 컴포넌트 발견!" + comp);
            //            break;
            //    }
            //}
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
        if (m_goSelectedObject != null)
        {
            FindRoute fr = m_goSelectedObject.GetComponent<FindRoute>();
            if (fr != null)
            {//움직이는 로봇들
                fr.RemoveThisObjectOnMap();
            }
            else
            {//가만히 배치돼있는 애들
                map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].Reset();
            }
            Destroy(m_goSelectedObject);
            m_goSelectedObject = null;
        }
        RightClickMenu.gameObject.SetActive(false);
    }

    void RenderPreview()
    {
        if (placePos.x >= 0 && placePos.x < mapSize.x && placePos.z >= 0 && placePos.z < mapSize.z)
        {
            if (buildingPrefabIndex > 1)    // 배치 될 프리팹 보여주기
            {
                SyncPreviewAndBuilding();
                if (map[(int)placePos.x, 0, (int)placePos.z].type == 0) // 바닥이면
                {
                    //Debug.Log("바닥인디요");
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = placePos;
                    foreach (Renderer r in previewInstance.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.materials;
                        for (int i = 0; i < mats.Length; ++i)
                        {
                            Color c = mats[i].color;
                            c.r = 0;
                            c.g = 1;
                            c.b = 0;
                            c.a = 0.3f;
                            mats[i].color = c;
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
                            Color c = mats[i].color;
                            c.r = 1;
                            c.g = 0;
                            c.b = 0;
                            c.a = 0.3f;
                            mats[i].color = c;
                        }
                    }
                }
            }
        }
    }

    public void InsertClicked()
    {

    }

    void LegacyPicking()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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
            placePos.Set(tileX, resources.Prefabs[buildingPrefabIndex].transform.position.y, tileZ);

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
                                Color c = mats[i].color;
                                c.r = 0;
                                c.g = 1;
                                c.b = 0;
                                c.a = 0.3f;
                                mats[i].color = c;
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
                                Color c = mats[i].color;
                                c.r = 1;
                                c.g = 0;
                                c.b = 0;
                                c.a = 0.3f;
                                mats[i].color = c;
                            }
                        }
                    }
                }

                if (Input.GetMouseButtonDown(0))
                {      // 좌클릭 했을 때
                    if (!IsPointerOverUI()) // ui를 안건드렸으면
                    {
                        if (m_goSelectedObject != null)
                        {
                            Renderer[] renderers = m_goSelectedObject.GetComponentsInChildren<Renderer>();
                            var originalMats = map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats;
                            if (originalMats != null && originalMats.Count == renderers.Length)
                            {
                                for (int i = 0; i < renderers.Length; ++i)
                                {
                                    renderers[i].sharedMaterials = originalMats[i];
                                }
                            }
                            map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats = null;
                            m_goSelectedObject = null;
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
                    if (m_goSelectedObject != null) // 이전에 선택된 애가 있다면 메터리얼 원상복귀
                    {
                        Renderer[] renderers = m_goSelectedObject.GetComponentsInChildren<Renderer>();
                        var originalMats = map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats;
                        if (originalMats != null && originalMats.Count == renderers.Length)
                        {
                            for (int i = 0; i < renderers.Length; ++i)
                            {
                                renderers[i].sharedMaterials = originalMats[i];
                            }
                        }
                        map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats = null;
                        m_goSelectedObject = null;
                    }

                    m_i3SelectedCoord.x = tileX; m_i3SelectedCoord.y = 0; m_i3SelectedCoord.z = tileZ;

                    // 기존 메테리얼들 저장, 와이어프레임으로 변경
                    m_goSelectedObject = map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].obj;
                    if (m_goSelectedObject != null)
                    {
                        //Debug.Log(m_goSelectedObject.name + "선택완료");
                        Renderer[] renderers = m_goSelectedObject.GetComponentsInChildren<Renderer>();
                        map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats = new List<Material[]>();
                        foreach (Renderer renderer in renderers)
                        {
                            //기존 메테리얼들 저장
                            map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats.Add(renderer.sharedMaterials);

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

    void ReturnSelectedObjectMat()
    {
        if (m_goSelectedObject != null)
        {
            Renderer[] renderers = m_goSelectedObject.GetComponentsInChildren<Renderer>();
            var originalMats = map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats;
            if (originalMats != null && originalMats.Count == renderers.Length)
            {
                for (int i = 0; i < renderers.Length; ++i)
                {
                    renderers[i].sharedMaterials = originalMats[i];
                }
            }
            map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats = null;
            m_goSelectedObject = null;
        }
    }

    void SaveSelectedObjectMat()
    {
        if (m_goSelectedObject != null)
        {
            Renderer[] renderers = m_goSelectedObject.GetComponentsInChildren<Renderer>();
            if (map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats == null)
            {
                map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats = new List<Material[]>();   // 최초 1회만 new 할당;
            }
            else
            {
                map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats.Clear();
            }
            foreach (Renderer renderer in renderers)
            {
                //기존 메테리얼들 저장
                map[m_i3SelectedCoord.x, m_i3SelectedCoord.y, m_i3SelectedCoord.z].originalMats.Add(renderer.sharedMaterials);

                //메테리얼 교체
                Material[] newMats = new Material[renderer.materials.Length];
                for (int i = 0; i < newMats.Length; ++i)
                {
                    newMats[i] = wireframeMat;
                }
                renderer.materials = newMats;
            }
        }
    }
}
