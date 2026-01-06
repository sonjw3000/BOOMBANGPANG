using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
	[SerializeField] private Material wireframeMat;
	[SerializeField] private GameObject rightClickMenu;
	[SerializeField] private GameObject mainCamera;
	[SerializeField] private GameObject SelectedStatus;

	// 선택된 오브젝트를 하이라이트
	[SerializeField] private GameObject floorhighLight;
	[SerializeField] private GameObject goalPositionHighlight;

	private GridMap MapManager => GameContext.Instance.GridMap;
	private GridCell[,,] map => MapManager.Map;
	private int3 mapSize => MapManager.MapSize;

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
		Select,
		Insert,
		Remove
	}
	private PickingType pickingType = PickingType.Select;

	// MousePicking에서 쓰는 변수
	private Plane groundPlane;
	private Vector3 placePos;

	// 풀링해서 사용하자
	private List<GameObject> floorhighLightPool = new();
	private List<GameObject > goalPositionPool = new();



	public GameObject SelectedObject => selectedObject;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		//resources = GameObject.Find("Resources").GetComponent<Resources>();
		//activate = GameObject.Find("ESC").GetComponent<UIOnOff>();
		//floorhighLight = transform.Find("Highlight").gameObject;
		//goalPositionHighlight = transform.Find("GoalPosition").gameObject;


		//if (map == null)
		//{
		//	Debug.LogError("mapRef is null!");
		//}
		//SyncPreviewAndBuilding();
		//previewInstance.SetActive(false);
		//buildingPrefabIndex = 0;
		//syncPrefabIndex = 0;

		//if (rightClickMenu)
		//{
		//	rightClickMenuAnimator = rightClickMenu.GetComponent<Animator>();
		//	rightClickMenuAnimator.enabled = false;
		//	rightClickMenu.SetActive(false);
		//	removeButton = rightClickMenu.transform.GetChild(1).gameObject;
		//	//Debug.Log(removeButton.gameObject.name);
		//}
		//selectedCoord = new int3();
		//ChangeSelectedObject(null);
		//head = 0;
		//pickingType = PickingType.SELECT;
		//groundPlane = new Plane(Vector3.up, Vector3.zero);
		//placePos = new Vector3();
		//if (mainCamera != null)
		//{
		//	mOrbitCamera = mainCamera.GetComponent<OrbitCamera>();
		//}
	}

	// Update is called once per frame
	private void Update()
	{
		if (!activate.activateRef)
		{
			KeyboardAction();
			MouseAction();
		}
		//Debug.Log($"Preview activeSelf={previewInstance.activeSelf}, buildingPrefabIndex={buildingPrefabIndex}");

	}

	private void MouseAction()
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
			case PickingType.Select:
				break;
			case PickingType.Insert:
				break;
			case PickingType.Remove:
				break;
		}
	}

	private void KeyboardAction()
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

}
