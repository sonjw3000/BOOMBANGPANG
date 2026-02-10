using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;


public class MousePicking : MonoBehaviour
{
	[SerializeField] private Material wireframeMat;
	[SerializeField] private GameObject rightClickMenu;
	[SerializeField] private GameObject mainCamera;
	[SerializeField] private GameObject SelectedStatus;

	public GameObject SelectedObject => selectedObject;

	// 선택된 오브젝트를 하이라이트
	[SerializeField] private GameObject floorhighLight;
	[SerializeField] private GameObject goalPositionHighlight;

	private GameObject removeButton;
	private Animator rightClickMenuAnimator;
	private GameObject selectedObject;


	// MousePicking에서 쓰는 변수
	private int currentFloor = 0;
	private int3 currentTargetPoint = new(0);
	private Plane groundPlane;

	private InteractionContext InteractionCtx => GameContext.Instance.InteractionCtx;

	public event System.Action<int3> OnMouseMoved;
	public bool IsPointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		OnMouseMoved += InteractionCtx.OnMouseMove;

		groundPlane = new Plane(Vector3.up, currentFloor);
	}

	// Update is called once per frame
	private void Update()
	{
		if (IsPointerOverUI)
			return;

		CalculateMousePos();

		if (Input.GetMouseButtonDown(0))
			InteractionCtx.OnLeftClick(currentTargetPoint);
		if (Input.GetMouseButtonDown(1))
			InteractionCtx.OnRightClick(currentTargetPoint);
	}

	private void CalculateMousePos()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

		groundPlane.distance = currentFloor;
		if (groundPlane.Raycast(ray, out var dist) == false)
		{
			return;
		}

		Vector3 point = ray.GetPoint(dist);

		int3 befPos = currentTargetPoint;

		currentTargetPoint.x = Mathf.FloorToInt(point.x);
		currentTargetPoint.y = currentFloor;
		currentTargetPoint.z = Mathf.FloorToInt(point.z);

		// 마우스 위치가 이동했다
		if (math.all(befPos == currentTargetPoint) == false)
		{
			OnMouseMoved?.Invoke(currentTargetPoint);
		}
	}

}
