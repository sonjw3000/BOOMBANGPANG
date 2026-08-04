using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;


public class MousePicking : MonoBehaviour
{
	[SerializeField] private Material wireframeMat;
	[SerializeField] private GameObject rightClickMenu;
	[SerializeField] private GameObject mainCamera;
	[SerializeField] private GameObject SelectedStatus;

	public GameObject SelectedObject => selectedObject;

	[SerializeField] private GameObject floorhighLight;
	[SerializeField] private GameObject goalPositionHighlight;
	[SerializeField, Min(0.0f)] private float rightClickDragThreshold = 6.0f;

	private GameObject removeButton;
	private Animator rightClickMenuAnimator;
	private GameObject selectedObject;
	private bool isRightPointerDown;
	private bool hasRightPointerDragged;
	private Vector2 rightPointerDownPosition;


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
		bool isPointerOverUI = IsPointerOverUI;
		if (isPointerOverUI == false)
			CalculateMousePos();

		HandleRightPointer(isPointerOverUI);
		if (isPointerOverUI)
			return;

		if (Input.GetMouseButtonDown(0))
			InteractionCtx.OnLeftClick(currentTargetPoint);
		if (Input.GetKeyDown(KeyCode.B))
			InteractionCtx.ToggleSelectionDomain();
		if (Input.GetKeyDown(KeyCode.R))
			InteractionCtx.RotatePlacement();
	}

	private void HandleRightPointer(bool isPointerOverUI)
	{
		if (Input.GetMouseButtonDown(1))
		{
			isRightPointerDown = isPointerOverUI == false;
			hasRightPointerDragged = false;
			rightPointerDownPosition = Input.mousePosition;
		}

		if (isRightPointerDown && Input.GetMouseButton(1))
		{
			float threshold = Mathf.Max(0.0f, rightClickDragThreshold);
			Vector2 pointerDelta = (Vector2)Input.mousePosition - rightPointerDownPosition;
			if (pointerDelta.sqrMagnitude > threshold * threshold)
				hasRightPointerDragged = true;
		}

		if (Input.GetMouseButtonUp(1) == false)
			return;

		bool shouldHandleClick = isRightPointerDown &&
			hasRightPointerDragged == false &&
			isPointerOverUI == false;
		isRightPointerDown = false;
		hasRightPointerDragged = false;

		if (shouldHandleClick)
			InteractionCtx.OnRightClick(currentTargetPoint);
	}

	private void CalculateMousePos()
	{
		if (TryGetGridPosition(Input.mousePosition, out int3 position) == false)
			return;

		int3 befPos = currentTargetPoint;
		currentTargetPoint = position;

		// ���콺 ��ġ�� �̵��ߴ�
		if (math.all(befPos == currentTargetPoint) == false)
		{
			OnMouseMoved?.Invoke(currentTargetPoint);
		}
	}

	public bool TryGetGridPosition(Vector2 screenPosition, out int3 position)
	{
		position = default;
		Camera camera = Camera.main;
		if (camera == null)
			return false;

		Ray ray = camera.ScreenPointToRay(screenPosition);
		groundPlane.SetNormalAndPosition(Vector3.up, new Vector3(0f, currentFloor, 0f));
		if (groundPlane.Raycast(ray, out float distance) == false)
			return false;

		Vector3 point = ray.GetPoint(distance);
		position = new int3(
			Mathf.FloorToInt(point.x + 0.5f),
			currentFloor,
			Mathf.FloorToInt(point.z + 0.5f));
		return true;
	}

}
