using Unity.VisualScripting;
using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
	private Camera _Camera;
	private Vector3 _CurTargetPos = new Vector3(0, 0, 0);
	public Vector3 _GoalTargetPos = new Vector3(0, 0, 0);

	private float _CurDistance { get; set; } = 5.0f;
	private float _GoalDistance { get; set; } = 5.0f;
	[SerializeField] private float _WheelSpeed = 5.0f;
	[SerializeField] private float _MinDistance = 1.0f;
	[SerializeField] private float _MaxDistance = 30.0f;
	[SerializeField] private float _ZoomSpeed = 5.0f;

	private float _Sensitivity { get; set; } = 2.5f;
	private float _Yaw = 0.0f;

	private Vector3 _MoveAxis = new Vector3(1, 0, 1);
	[SerializeField] private float _MoveSpeed = 25.0f;
	[SerializeField] private float _CameraFollowSpeed = 5.0f;

	private GameObject _LockObject;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		_Yaw = transform.rotation.eulerAngles.y;
		_CurTargetPos = _GoalTargetPos;
		_GoalDistance = _CurDistance = Vector3.Distance(_CurTargetPos, transform.position);
	}

	// Update is called once per frame
	void Update()
	{
		// move by keyboard input
		//transform.right;
		Vector3 move = new Vector3(0, 0, 0);

		if (Input.GetKey(KeyCode.W)) move += transform.forward;
		if (Input.GetKey(KeyCode.S)) move -= transform.forward;
		if (Input.GetKey(KeyCode.A)) move -= transform.right;
		if (Input.GetKey(KeyCode.D)) move += transform.right;

		move = Vector3.Normalize(Vector3.Scale(move, _MoveAxis)) * _MoveSpeed * Time.deltaTime;

		_GoalTargetPos += move;

		if(_LockObject != null)
		{
			_GoalTargetPos = _LockObject.transform.position;
		}

		// move cur target to goal target
		float dist = Vector3.Distance(_CurTargetPos, _GoalTargetPos);

		if (Mathf.Approximately(dist, 0.0f))
		{
			_CurTargetPos = _GoalTargetPos; 
		}
		else
		{
			Vector3 moveMount = _GoalTargetPos - _CurTargetPos;
			_CurTargetPos += moveMount * _CameraFollowSpeed * Time.deltaTime;
		}
	}

	private void LateUpdate()
	{
		// rotation by mouse right click
		if (Input.GetMouseButton(1))
		{
			_Yaw += Input.GetAxis("Mouse X") * _Sensitivity;
		}

		// zoom by mouse wheel
		float wheelMount = Input.GetAxis("Mouse ScrollWheel");
		if (Mathf.Approximately(wheelMount, 0.0f) == false)
		{
			// wheel move
			_GoalDistance -= wheelMount * _WheelSpeed;
			_GoalDistance = Mathf.Clamp(_GoalDistance, _MinDistance, _MaxDistance);
		}
		
		if (Mathf.Approximately(_GoalDistance, _CurDistance))
		{
			_CurDistance = _GoalDistance;
		}
		else
		{
			float diff = _GoalDistance - _CurDistance;
			_CurDistance += diff * _ZoomSpeed * Time.deltaTime;
		}
		

		// set camera
		Quaternion rot = Quaternion.Euler(45.0f, _Yaw, 0);
		Vector3 pos = _CurTargetPos - (rot * Vector3.forward * _CurDistance);

		transform.position = pos;
		transform.LookAt(_CurTargetPos);
	}

	public void LockObject(GameObject lockingObject)
	{
		_LockObject = lockingObject;
	}
}
