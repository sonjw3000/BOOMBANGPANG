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

	private GameObject removeButton;
	private Animator rightClickMenuAnimator;
	private GameObject selectedObject;


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
		if (Input.GetKeyDown(KeyCode.B))
			InteractionCtx.ToggleSelectionDomain();
		if (Input.GetKeyDown(KeyCode.R))
			InteractionCtx.RotatePlacement();
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
