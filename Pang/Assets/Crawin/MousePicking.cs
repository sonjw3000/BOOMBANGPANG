using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;


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
	public enum MouseMode
	{
		Select,
		Insert,
	}
	private MouseMode mouseMode = MouseMode.Select;

	// MousePicking에서 쓰는 변수
	private int currentFloor = 0;
	private int3 currentTargetPoint = new int3(0);
	private Plane groundPlane;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		groundPlane = new Plane(Vector3.up, currentFloor);
	}

	// Update is called once per frame
	private void Update()
	{
		CalculateMousePos();

		MouseAction();
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

		currentTargetPoint.x = Mathf.FloorToInt(point.x);
		currentTargetPoint.y = currentFloor;
		currentTargetPoint.z = Mathf.FloorToInt(point.z);
	}

	private void MouseAction()
	{


		// remove 모드가 필요할까?
		// remove는 ui상에서 제어하자

		if (Input.GetMouseButtonDown(0))
			OnMouseLeftClick();
		if (Input.GetMouseButtonDown(1))
			OnMouseRightClick();
	}

	private void OnMouseLeftClick()
	{
		// select 모드 일 때 << 기본임
		// 좌클릭 << select

		// insert 모드일 때
		// 좌클릭 << 설치

		switch (mouseMode)
		{
			case MouseMode.Select:
				
				break;

			case MouseMode.Insert:
				break;
		}


	}

	private void OnMouseRightClick()
	{
		// select 모드 일 때 << 기본임
		// 우클릭 << 우클릭 메뉴 생성? 우클릭 어쨋든 함

		// insert 모드일 때
		// 우클릭 << 설치 취소

		switch (mouseMode)
		{
			case MouseMode.Select:
				break;

			case MouseMode.Insert:
				break;
		}
	}




}
