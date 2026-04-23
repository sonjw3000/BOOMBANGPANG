using UnityEngine;
using Unity.Mathematics;

public class FindRoute : MonoBehaviour
{
	//private Resources resources;
	public enum MovementState
	{
		Idle,
		PathPending,
		Moving,
		Arrived,
		Blocked,
		Failed,
	}

	private GridService GridService => GameContext.Instance.GridService;
	private PathFindingService PathFinding => GameContext.Instance.PathFinding;

	private AIWorker worker;
	private MovementState movementState = MovementState.Idle;
	private PathResultBuffer pathResultBuffer;
	private Vector3 targetPos = Vector3.zero;

	public float GetMovementSpeed() => GameContext.Instance.WMSys.WorkPolicyService.GetMoveSpeed(worker);
	public float GetRotationSpeed() => GetMovementSpeed() * 2.5f;

	public bool IsGoal => movementState == MovementState.Arrived;
	public MovementState CurrentMovementState => movementState;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	private void Start()
	{
		enabled = false;
	}

	private void Update()
	{
		if (pathResultBuffer != null)
			MoveOnTile();
	}

	private void MoveOnTile()
	{
		// if fin
		if (pathResultBuffer.IsGoalReached)
		{
			pathResultBuffer.Clear();
			pathResultBuffer = null;

			movementState = MovementState.Arrived;
			worker.enabled = true;
			this.enabled = false;
			
			return;
		}

		// if rotate is needed, rotate first. if not, move forward.
		if (pathResultBuffer.CurrentNode.Direction != worker.Direction)
		{
			Vector3 direction = math.normalize(targetPos - transform.position);

			float rotSpeed = GetRotationSpeed();
			Quaternion targetRotation = Quaternion.LookRotation(direction);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotSpeed);

			float dotProduct = math.dot(transform.forward, direction);
			if (Mathf.Approximately(dotProduct, 1.0f))
			{
				transform.rotation = targetRotation;
				worker.SetDirection(pathResultBuffer.CurrentNode.Direction);
			}
		}
		else
		{
			transform.position = Vector3.MoveTowards(transform.position, targetPos, GetMovementSpeed() * Time.deltaTime);
			
			float distance = Vector3.Distance(transform.position, targetPos);
			if (Mathf.Approximately(distance, 0.0f) == false)
				return;

			transform.position = targetPos;
			if (GridService.TryMove(worker, worker.GridPosition, pathResultBuffer.CurrentNode.Position) == false)
			{
				movementState = MovementState.Blocked;
				worker.enabled = true;
				this.enabled = false;
				 Debug.Log(
					 transform.name + "의 이동이 막혔습니다. 현재 위치: " 
					 + worker.GridPosition + ", 목표 위치: " 
					 + pathResultBuffer.CurrentNode.Position);
				return;
			}

			pathResultBuffer.MoveToNextNode();
			if (pathResultBuffer.IsGoalReached == false)
			{
				var curNode = pathResultBuffer.CurrentNode;
				targetPos.x = curNode.Position.x;
				targetPos.y = curNode.Position.y;
				targetPos.z = curNode.Position.z;
			}
		}
	}

	public bool SetGoalPosition(int3 goalPos)
	{
		PathRequest request = new PathRequest(this, worker.GridPosition, goalPos, worker.Direction);
		PathFinding.RequestRoute(request);

		worker.enabled = false;

		return true;
	}

	public void SetAIMaster(AIWorker worker)
	{
		this.worker = worker;
	}

	public void OnPathFound(PathResultBuffer pathResultBuffer)
	{
		this.pathResultBuffer = pathResultBuffer;
		if (pathResultBuffer.Path.Count > 0)
		{
			// start node는 현재 위치이므로 다음 노드로 이동
			pathResultBuffer.MoveToNextNode();

			movementState = MovementState.Moving;
			var curNode = pathResultBuffer.CurrentNode;
			targetPos.x = curNode.Position.x;
			targetPos.y = curNode.Position.y;
			targetPos.z = curNode.Position.z;

			enabled = true;

# if UNITY_EDITOR
			//Debug.Log(transform.name + "가 목적지로 이동을 시작합니다. 경로 길이: " + pathResultBuffer.Path.Count);
			//for(int i = 0; i < pathResultBuffer.Path.Count; i++)
			//{
			//	var node = pathResultBuffer.Path.ElementAt(i);
			//	Debug.Log($"경로 {i}: 위치({node.Position.x}, {node.Position.y}, {node.Position.z}), 방향: {node.Direction}");
			//}
#endif

		}
		else
		{
			movementState = MovementState.Failed;
			Debug.Log(transform.name + "가 목적지로 갈 수 없습니다.");
		}
	}

	public float GetPathPercent()
	{
		if (pathResultBuffer == null || pathResultBuffer.Path.Count == 0)
			return 0.0f;

		return (float)pathResultBuffer.CurrentIndex / pathResultBuffer.Path.Count;
	}
}
