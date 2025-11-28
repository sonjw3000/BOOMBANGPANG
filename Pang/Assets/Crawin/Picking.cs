using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Picking : MonoBehaviour
{
	[SerializeField] private Material wireframeMat;
	[SerializeField] private GameObject rightClickMenu;
	[SerializeField] private GameObject mainCamera;
	[SerializeField] private GameObject statusCanvas;

	private Resources resources;

	private Cell[,,] map;
	private int3 mapSize;
	[HideInInspector]
	private int buildingPrefabIndex;
	private int syncPrefabIndex;
	private GameObject previewInstance;
	private UIOnOff activate;

	private GameObject removeButton;
	private Animator rightClickMenuAnimator;
	private int3 selectedCoord;
	private GameObject selectedObject;
	private int head;
	public enum PickingType
	{
		SELECT,
		INSERT,
		REMOVE
	}
	private PickingType pickingType;

	// MousePicking에서 쓰는 변수
	private Plane groundPlane;
	private Vector3 placePos;

	private OrbitCamera mOrbitCamera;

	// 선택된 오브젝트를 하이라이트
	private GameObject floorhighLight;
	private GameObject goalPositionHighlight;

	public GameObject SelectedObject => selectedObject;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		resources = GameObject.Find("Resources").GetComponent<Resources>();
		activate = GameObject.Find("ESC").GetComponent<UIOnOff>();
		floorhighLight = transform.Find("Highlight").gameObject;
		goalPositionHighlight = transform.Find("GoalPosition").gameObject;

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

		if (rightClickMenu)
		{
			rightClickMenuAnimator = rightClickMenu.GetComponent<Animator>();
			rightClickMenuAnimator.enabled = false;
			rightClickMenu.SetActive(false);
			removeButton = rightClickMenu.transform.GetChild(1).gameObject;
			//Debug.Log(removeButton.gameObject.name);
		}
		selectedCoord = new int3();
		ChangeSelectedObject(null);
		head = 0;
		pickingType = PickingType.SELECT;
		groundPlane = new Plane(Vector3.up, Vector3.zero);
		placePos = new Vector3();
		if (mainCamera != null)
		{
			mOrbitCamera = mainCamera.GetComponent<OrbitCamera>();
		}
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
			rightClickMenu.SetActive(false);
		}
		if (Input.GetMouseButtonUp(1))
		{
			Vector3 mousePos = Input.mousePosition;

			if (selectedObject != null)
			{
				removeButton.SetActive(true);
			}
			else
			{
				removeButton.SetActive(false);
			}
			rightClickMenu.SetActive(true);
			rightClickMenu.transform.position = mousePos;
			rightClickMenuAnimator.enabled = true;
		}

		switch (pickingType)
		{
			case PickingType.SELECT:
				if (Input.GetMouseButtonDown(0))
					OnGameScreenLeftClicked();
				break;
			case PickingType.INSERT:
				{
					ReturnSelectedObjectMat();
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

								rightClickMenu.SetActive(false);
								Transform parentTransform;
								if (map[tileX, 0, tileZ].type == 0)   // 바닥에 아무것도 없으면
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
										Shelf shelf = map[tileX, 0, tileZ].obj.GetComponent<Shelf>();
										if (shelf)
										{
											int3 PickPosition = shelf.PickingPosition;
											map[PickPosition.x, PickPosition.y, PickPosition.z].type = -1;
											map[PickPosition.x, PickPosition.y, PickPosition.z].previousType = -1;
										}
									}

									Status st = map[tileX, 0, tileZ].obj.GetComponent<Status>();
									if (st != null)
									{
										st.SetInit(map[tileX, 0, tileZ].obj.name, buildingPrefabIndex);
									}

									map[tileX, 0, tileZ].type = buildingPrefabIndex;
									pickingType = PickingType.SELECT;
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
		GraphicRaycaster gr = rightClickMenu.GetComponentInParent<GraphicRaycaster>();
		gr.Raycast(pointerData, results);
		return results.Count > 0;
	}

	public void RemoveObject()
	{
		if (selectedObject != null)
		{
			FindRoute fr = selectedObject.GetComponent<FindRoute>();
			if (fr != null)
			{//움직이는 로봇들
				fr.RemoveThisObjectOnMap();
			}
			else
			{//가만히 배치돼있는 애들
				map[selectedCoord.x, selectedCoord.y, selectedCoord.z].Reset();
			}
			Destroy(selectedObject);
			ChangeSelectedObject(null);
		}
		rightClickMenu.gameObject.SetActive(false);
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

	private void OnGameScreenLeftClicked()
	{
		// ui를 클릭했다면 패스
		if (IsPointerOverUI())
			return;
		
		rightClickMenu.SetActive(false);
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

		if (groundPlane.Raycast(ray, out float distance))
		{
			ReturnSelectedObjectMat();
			Vector3 worldPos = ray.GetPoint(distance);
			int tileX = Mathf.FloorToInt(worldPos.x + 0.5f);
			int tileZ = Mathf.FloorToInt(worldPos.z + 0.5f);
			if (0 <= tileX && tileX < mapSize.x && 
				0 <= tileZ && tileZ < mapSize.z)
			{
				if (map[tileX, 0, tileZ].obj != null)
				{
					selectedCoord.x = tileX; selectedCoord.y = 0; selectedCoord.z = tileZ;
					ChangeSelectedObject(map[selectedCoord.x, selectedCoord.y, selectedCoord.z].obj);
					
				}
				else
				{	// 다른 뭔가가 좌클릭 되면 선택 해제
					ChangeSelectedObject(null);
				}
			}
		}
		else
		{	// 다른 뭔가가 좌클릭 되면 선택 해제
			ChangeSelectedObject(null);
		}
		

	}

	void ReturnSelectedObjectMat()
	{
		if (selectedObject != null)
		{
			Renderer[] renderers = selectedObject.GetComponentsInChildren<Renderer>();
			var originalMats = map[selectedCoord.x, selectedCoord.y, selectedCoord.z].originalMats;
			if (originalMats != null && originalMats.Count == renderers.Length)
			{
				for (int i = 0; i < renderers.Length; ++i)
				{
					renderers[i].sharedMaterials = originalMats[i];
				}
			}
			map[selectedCoord.x, selectedCoord.y, selectedCoord.z].originalMats = null;
			ChangeSelectedObject(null);
		}
		mOrbitCamera.LockObject(null);
	}

	void SaveSelectedObjectMat()
	{
		if (selectedObject != null)
		{
			Renderer[] renderers = selectedObject.GetComponentsInChildren<Renderer>();
			if (map[selectedCoord.x, selectedCoord.y, selectedCoord.z].originalMats == null)
			{
				map[selectedCoord.x, selectedCoord.y, selectedCoord.z].originalMats = new List<Material[]>();   // 최초 1회만 new 할당;
			}
			else
			{
				map[selectedCoord.x, selectedCoord.y, selectedCoord.z].originalMats.Clear();
			}
			foreach (Renderer renderer in renderers)
			{
				//기존 메테리얼들 저장
				map[selectedCoord.x, selectedCoord.y, selectedCoord.z].originalMats.Add(renderer.sharedMaterials);

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

	private void ChangeSelectedObject(GameObject obj)
	{
		selectedObject = obj;

		if (selectedObject == null)
		{
			floorhighLight.SetActive(false);
			goalPositionHighlight.SetActive(false);
			return;
		}
		// save material
		SaveSelectedObjectMat();
		statusCanvas.SetActive(true);

		// 해당 오브젝트의 속성 출력 예정
		Status st = selectedObject.gameObject.GetComponent<Status>();
		if (st != null)
		{
			st.OnClick();
		}
		mOrbitCamera.LockObject(selectedObject);
		//Debug.Log(selectedObject.name + "이 선택 되었습니다.");

		// 하이라이트 켜놓기
		floorhighLight.transform.position = selectedObject.transform.position;
		floorhighLight.SetActive(true);

		var shelf = selectedObject.GetComponent<ShelfBase>();
		if (shelf != null)
		{
			int3 pos = shelf.PickingPosition;
			goalPositionHighlight.transform.position = new Vector3(pos.x, pos.y, pos.z);
			goalPositionHighlight.SetActive(true);
		}
	}

	public void SetBuildingID(int id)
	{
		buildingPrefabIndex = id;
		pickingType = Picking.PickingType.INSERT;
	}
}
