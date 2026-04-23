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
	private bool isNextNodeReserved = false;

	public float GetMovementSpeed() => GameContext.Instance.WMSys.WorkPolicyService.GetMoveSpeed(worker);
	public float GetRotationSpeed() => GetMovementSpeed() * 2.5f;

	public bool IsGoal => movementState == MovementState.Arrived;
	public MovementState CurrentMovementState => movementState;

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
		if (pathResultBuffer.IsGoalReached)
		{
			OnArrived();
			return;
		}

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

			return;
		}

		if (isNextNodeReserved == false)
		{
			if (TryReserveNextTile())
			{
				isNextNodeReserved = true;
				movementState = MovementState.Moving;
			}
			else
			{
				HandleBlocked();
				movementState = MovementState.Blocked;
				return;
			}
		}

		transform.position = Vector3.MoveTowards(transform.position, targetPos, GetMovementSpeed() * Time.deltaTime);

		float distance = Vector3.Distance(transform.position, targetPos);
		if (Mathf.Approximately(distance, 0.0f) == false)
			return;

		transform.position = targetPos;

		int3 previousPos = worker.GridPosition;
		var moveResult = GridService.TryMove(worker, worker.GridPosition, pathResultBuffer.CurrentNode.Position);
		if (moveResult != PlacementResult.Success)
		{
			movementState = MovementState.Blocked;
			worker.enabled = true;
			enabled = false;
			Debug.Log(
				transform.name + " movement was blocked, " +
				"reason: " + moveResult + ", " +
				"current position: " + worker.GridPosition + ", " +
				"target position: " + pathResultBuffer.CurrentNode.Position + ", " +
				"task type: " + worker.TaskType + ", " +
				"target type: " + worker.WorkerState.Target
			);
			return;
		}

		GridService.TryUnreserve(this, previousPos);
		pathResultBuffer.MoveToNextNode();
		isNextNodeReserved = false;
		SyncTargetPositionToCurrentNode();
	}

	private bool TryReserveNextTile()
	{
		if (pathResultBuffer == null || pathResultBuffer.IsGoalReached)
		{
			Debug.LogError("PathResultBuffer is null or goal is already reached. Cannot reserve next tile.");
			return false;
		}

		var nodeToReserve = pathResultBuffer.CurrentNode;
		return GridService.TryReserve(this, nodeToReserve.Position);
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

		// Reserve the current tile from initialization time.
		if (GridService.TryReserve(this, worker.GridPosition) == false)
		{
			Debug.LogError("Failed to reserve initial position for AIWorker.");
		}
	}

	public void OnPathFound(PathResultBuffer pathResultBuffer)
	{
		this.pathResultBuffer = pathResultBuffer;
		if (pathResultBuffer.Path.Count > 0)
		{
			// The start node is the current tile, so move to the first actual step.
			pathResultBuffer.MoveToNextNode();
			movementState = MovementState.Moving;

			// If the start and goal are the same, the path contains only one node.
			if (pathResultBuffer.IsGoalReached)
			{
				OnArrived();
				return;
			}

			SyncTargetPositionToCurrentNode();
			enabled = true;

#if UNITY_EDITOR
			//Debug.Log(transform.name + " started moving to the goal. Path length: " + pathResultBuffer.Path.Count);
			//for (int i = 0; i < pathResultBuffer.Path.Count; i++)
			//{
			//	var node = pathResultBuffer.Path.ElementAt(i);
			//	Debug.Log($"Path {i}: position({node.Position.x}, {node.Position.y}, {node.Position.z}), direction: {node.Direction}");
			//}
#endif
		}
		else
		{
			movementState = MovementState.Failed;
			Debug.Log(transform.name + " could not find a route to the goal.");
		}
	}

	public float GetPathPercent()
	{
		if (pathResultBuffer == null || pathResultBuffer.Path.Count == 0)
			return 0.0f;

		return (float)pathResultBuffer.CurrentIndex / pathResultBuffer.Path.Count;
	}

	private void OnArrived()
	{
		pathResultBuffer.Clear();
		pathResultBuffer = null;

		movementState = MovementState.Arrived;
		worker.enabled = true;
		enabled = false;
	}

	private void HandleBlocked()
	{
		FindRoute blockedBy = GridService.GetReservedFindRoute(pathResultBuffer.CurrentNode.Position);

		if (blockedBy == null)
		{
			Debug.LogError("Reserve failed, but no blocking FindRoute was found.");
			return;
		}

		if (blockedBy.pathResultBuffer == null)
		{
			Debug.LogWarning("The blocking route has no path buffer.");
			return;
		}

		var otherNextNode = blockedBy.pathResultBuffer.NextNode;

		if (otherNextNode == null)
		{
			var otherCurNode = blockedBy.pathResultBuffer.CurrentNode;
			GridCell targetCell = GridService.GetCell(otherCurNode.Position);
			if (targetCell != null)
			{
				Debug.Log("Waiting until the blocking route finishes.");
				targetCell.OnGridUnReserved += OnCanReserve;
				enabled = false;
			}
		}
		else if (otherNextNode.Position.Equals(worker.GridPosition))
		{
			// Deadlock case: the other route plans to enter our current tile next.
			// A yield policy will be needed here.
		}
		else
		{
			GridCell targetCell = GridService.GetCell(otherNextNode.Position);
			if (targetCell != null)
			{
				Debug.Log("Waiting because the target tile is still reserved by another route.");
				targetCell.OnGridUnReserved += OnCanReserve;
				enabled = false;
			}
		}
	}

	private void OnCanReserve(GridCell target)
	{
		target.OnGridUnReserved -= OnCanReserve;
		Debug.Log("Wait released.");
		enabled = true;
	}

	private void SyncTargetPositionToCurrentNode()
	{
		if (pathResultBuffer == null || pathResultBuffer.IsGoalReached)
			return;

		var curNode = pathResultBuffer.CurrentNode;
		targetPos.x = curNode.Position.x;
		targetPos.y = curNode.Position.y;
		targetPos.z = curNode.Position.z;
	}
}
