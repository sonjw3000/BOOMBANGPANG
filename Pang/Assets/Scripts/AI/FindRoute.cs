using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

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
	private PathResultBuffer pathResultBuffer = null;
	private Vector3 targetPos = Vector3.zero;
	private bool isNextNodeReserved = false;

	private HashSet<FindRoute> blockingRoutes = new();

	public float GetMovementSpeed() => GameContext.Instance.WMSys.WorkPolicyService.GetMoveSpeed(worker);
	public float GetRotationSpeed() => GetMovementSpeed() * 2.5f;

	public IReadOnlyCollection<FindRoute> BlockingRoutes => blockingRoutes;
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
			Vector3 direction = pathResultBuffer.CurrentNode.Direction.ForwardDirection().ToVector3().normalized;

			if (Vector3.zero.Equals(direction))
			{
				// 
				Debug.LogError("Same Rotation but tried to rotate");
				worker.SetDirection(pathResultBuffer.CurrentNode.Direction);
				return;
			}

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
		var moveResult = GridService.TryMove(this, worker.GridPosition, pathResultBuffer.CurrentNode.Position);
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

		worker.SetPosition(pathResultBuffer.CurrentNode.Position);

		if (worker.GridPosition.Equals(previousPos) == false)
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

		// 이동중이지 않은 worker에 닿았을 때
		if (blockedBy.pathResultBuffer == null)
		{
			blockingRoutes.Add(blockedBy);

			// find leaf sub path
			PathResultBuffer parent = pathResultBuffer;
			PathResultBuffer leafBuffer = pathResultBuffer;
			PathResultBuffer child = leafBuffer.SubPathResult;
			while (child != null)
			{
				parent = leafBuffer;
				leafBuffer = child;
				child = child.SubPathResult;
			}

			// leaf buffer의 path 목적지가 한 개 남았을 때
			if (leafBuffer.NextNode == null)
			{
				// 최종 목적지가 저거이기 때문에 기다려야한다
				if (leafBuffer == pathResultBuffer)
				{
					Debug.Log("[FindRoute] Something is Blocking the goal position!!");
					enabled = false;
					return;
				}

				// leaf buffer를 삭제하고 현 위치 기준으로 새로운 루트를 개척해야함
				Debug.Log("[FindRoute] SubPath Goal Blocked! go around!!");

				parent.SubPathResult = null;
			}
			
			// 다다음 목적지를 새로운 경로로 설정하고 기존 경로 앞에 붙인다
			RequestSubPath(pathResultBuffer.NextNode.Position, blockedBy);
			pathResultBuffer.MoveToNextNode();

			return;
		}

		// 이동중인 worker와 닿았을 때
		var otherToNode = blockedBy.pathResultBuffer.CurrentNode;
		var otherNextNode = blockedBy.pathResultBuffer.NextNode;
		if (otherNextNode == null)
		{
			// 상대방이 endpoint에 도착하기 직전일 때
			var otherCurNode = blockedBy.pathResultBuffer.CurrentNode;
			GridCell targetCell = GridService.GetCell(otherCurNode.Position);
			if (targetCell != null)
			{
				Debug.Log("Waiting until the blocking route finishes.");
				targetCell.OnGridUnReserved += OnCanReserve;
				enabled = false;
			}
		}
		else if (otherToNode.Position.Equals(worker.GridPosition))
		{
			// 교착상태일 때
			Debug.Log("Deadlock!!!!!");
		}
		else if (otherNextNode.Position.Equals(worker.GridPosition))
		{
			Debug.Log("Deadlock?????");
			// 경로가 교착되었을 때
			// 저거임
			// Deadlock case: the other route plans to enter our current tile next.
			// A yield policy will be needed here.
		}
		else
		{
			// 상대방의 경로가 나와 완전히 다를 때
			GridCell targetCell = GridService.GetCell(otherNextNode.Position);
			if (targetCell != null)
			{
				Debug.Log("왜일까 한번 체크해보자" + 
					$"other cur: {blockedBy.worker.GridPosition} to: {otherToNode.Position}, next: {otherNextNode?.Position}, " +
					$"mine Cur: {worker.GridPosition}, next: {pathResultBuffer.CurrentNode.Position}");
				targetCell.OnGridUnReserved += OnCanReserve;
				enabled = false;
			}
		}
	}

	private bool RequestSubPath(in int3 goalPos, FindRoute avoidTarget)
	{
		PathRequest request = new(this, worker.GridPosition, goalPos, worker.Direction, avoidTarget);
		PathFinding.RequestRoute(request);

		Debug.Log($"SubPath Req: from: {worker.GridPosition} to: {goalPos}");

		worker.enabled = false;

		return true;
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

	public bool SetGoalPosition(in int3 goalPos)
	{
		PathRequest request = new(this, worker.GridPosition, goalPos, worker.Direction);
		PathFinding.RequestRoute(request);

		movementState = MovementState.PathPending;

		worker.enabled = false;

		return true;
	}

	public void SetAIMaster(AIWorker worker)
	{
		this.worker = worker;

		Debug.Log($"Init Grid Position!, GridPos: {worker.GridPosition}");
		// Reserve the current tile from initialization time.
		if (GridService.TryReserve(this, worker.GridPosition) == false)
		{
			Debug.LogError("Failed to reserve initial position for AIWorker.");
		}
	}

	public void OnPathFound(PathResultBuffer pathBuffer)
	{
		if (pathBuffer.Path.Count <= 0)
		{
			movementState = MovementState.Failed;
			Debug.Log(transform.name + " could not find a route to the goal.");
			return;
		}

		if (pathResultBuffer != null)
		{
			// its sub path, so we need to append it to the existing path buffer
			var curNode = pathResultBuffer.CurrentLinkedListNode;

			//Debug.Log($"SubPath Res: " +
			//	$"from: {pathBuffer.Path.First.Value.Position}" +
			//	$" to: {pathBuffer.Path.Last.Value.Position}," +
			//	$" next: {curNode.Next.Value.Position}");

			pathResultBuffer.SubPathResult = pathBuffer;
		}
		else
			pathResultBuffer = pathBuffer;

		movementState = MovementState.Moving;

		pathResultBuffer.MoveToNextNode();

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

	public void RemoveBlocked(FindRoute route)
	{
		if (blockingRoutes.Contains(route))
		{
			blockingRoutes.Remove(route);
			Debug.Log("Removed blocking route: " + route.name);
		}
	}

	public float GetPathPercent()
	{
		if (pathResultBuffer == null || pathResultBuffer.Path.Count == 0)
			return 0.0f;

		return (float)pathResultBuffer.CurrentIndex / pathResultBuffer.Path.Count;
	}
}
