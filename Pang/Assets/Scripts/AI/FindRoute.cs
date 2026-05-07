using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

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

	private static GridService GridService => GameContext.Instance.GridService;
	private static PathFindingService PathFinding => GameContext.Instance.PathFinding;
	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;

	private AIWorker worker;
	private MovementState movementState = MovementState.Idle;
	private PathResultBuffer pathResultBuffer = null;
	private Vector3 targetPos = Vector3.zero;
	private bool isNextNodeReserved = false;
	private GridCell waitingCell = null;

	private HashSet<FindRoute> blockingRoutes = new();


#if UNITY_EDITOR

	private Color pathColor = Color.darkSeaGreen;
	private Vector3 cellSize = new(0.8f, 0.05f, 0.8f);

	private void DrawPath(PathResultBuffer buf)
	{
		if (buf == null)
			return;

		var node = buf.CurrentLinkedListNode;

		while (node != null)
		{
			Vector3 world = new(node.Value.Position.x, node.Value.Position.y, node.Value.Position.z);

			Gizmos.color = pathColor;
			Gizmos.DrawCube(world, cellSize);

			node = node.Next;
		}
	}

	private void OnDrawGizmos()
	{
		var buf = pathResultBuffer;

		while (buf != null)
		{
			DrawPath(buf);
			buf = buf.SubPathResult;
		}
	}

#endif
	private void OnDisable()
	{
		ClearWait();
	}

	private void ClearWait()
	{
		if (waitingCell != null)
		{
			waitingCell.OnGridUnReserved -= OnCanReserve;
			waitingCell = null;
		}
		CancelInvoke(nameof(OnWaitTimeout));
	}

	public float GetMovementSpeed() => WorkPolicy.GetMoveSpeed(worker);
	public float GetRotationSpeed() => GetMovementSpeed() * 2.5f;

	public IReadOnlyCollection<FindRoute> BlockingRoutes => blockingRoutes;
	public bool IsGoal => movementState == MovementState.Arrived;
	public bool IsWaiting => waitingCell != null;
	public MovementState CurrentMovementState => movementState;

	public int RemainingDistance => pathResultBuffer != null ? pathResultBuffer.Path.Count - pathResultBuffer.CurrentIndex : int.MaxValue;

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
		{
			bool unreserveRes = GridService.TryUnreserve(this, previousPos);
			//Debug.Log($"[FindRoute] {transform.name} Unreserved {previousPos}. Result: {unreserveRes}");
		}

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
			Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} Reserve failed, but no blocking FindRoute was found. Maybe new placeable");
			RequestSubPath(pathResultBuffer.NextNode.Position, null);
			return;
		}

		// path 목적지가 한 개 남았을 때
		if (pathResultBuffer.NextNode == null)
		{
			Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} " +
				$"detected {blockedBy.name}, ID: {blockedBy.worker.WorkerID} is Blocking the goal position!! " +
				$"Waiting For: {pathResultBuffer.CurrentNode.Position}");
			WaitForTargetCell(pathResultBuffer.CurrentNode.Position);
			return;
		}

		// 이동중이지 않은 worker에 닿았을 때
		// 혹은 마지막 노드인 상대 worker
		var otherCurNode = blockedBy.pathResultBuffer?.CurrentNode;
		var otherNextNode = blockedBy.pathResultBuffer?.NextNode;

		// 상대방이 대기 중(IsWaiting)이라면 정적 장애물로 판단하지 않습니다.
		if (blockedBy.pathResultBuffer == null || (blockedBy.enabled == false && !blockedBy.IsWaiting) || otherNextNode == null)
		{
			Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} " +
				$"detected {blockedBy.name}, ID: {blockedBy.worker.WorkerID} is static or finishing. Requesting SubPath. " +
				$"Target: {pathResultBuffer.NextNode.Position}");
			blockingRoutes.Add(blockedBy);

			RequestSubPath(pathResultBuffer.NextNode.Position, blockedBy);
			return;
		}

		// 상대방이 이동한다
		// 상대방이 내 자리를 노린다면
		if (otherNextNode.Position.Equals(worker.GridPosition))
		{
			Debug.Log($"[FindRoute] DeadLock!! {transform.name}, ID: {worker.WorkerID}, with {blockedBy.name}, ID: {blockedBy.worker.WorkerID} " +
				$"ID: {blockedBy.worker.WorkerID} cur: {blockedBy.worker.GridPosition} " +
				$"cur: {otherCurNode.Position}, " +
				$"next: {otherNextNode?.Position}, " +
				$"ID: {worker.WorkerID} " +
				$"Cur: {worker.GridPosition}, " +
				$"cur: {pathResultBuffer.CurrentNode.Position}," +
				$" next: {pathResultBuffer.NextNode?.Position}, ");

			bool res = WorkPolicy.IsTargetHigherPriority(worker, blockedBy.worker);

			FindRoute high = res ? this : blockedBy;
			FindRoute low = res ? blockedBy : this;

			// high: wait
			// low: req new sub path
			high.WaitForTargetCell(high.pathResultBuffer.CurrentNode.Position);
			low.RequestSubPath(low.pathResultBuffer.NextNode.Position, high);

			return;
		}

		// 상대방이 다른 자리로 이동할 것이다
		WaitForTargetCell(pathResultBuffer.CurrentNode.Position);
	}

	private bool RequestSubPath(in int3 goalPos, FindRoute avoidTarget)
	{
		//Debug.Log($"SubPath Req: from: {worker.GridPosition} to: {goalPos}, avoiding: {avoidTarget.worker.GridPosition}");
		
		PathRequest request = new(this, worker.GridPosition, goalPos, worker.Direction, avoidTarget);
		PathFinding.RequestRoute(request);

		enabled = false;
		pathResultBuffer.MoveToNextNode();

		return true;
	}

	private void WaitForTargetCell(in int3 pos)
	{
		ClearWait();
		//Debug.Log("Waiting until the blocking route finishes.");
		GridCell targetCell = GridService.GetCell(pos);

		if (targetCell == null)
		{
			return;
		}

		waitingCell = targetCell;
		waitingCell.OnGridUnReserved += OnCanReserve;
		enabled = false;

		Invoke(nameof(OnWaitTimeout), 5f);
	}

	private void OnWaitTimeout()
	{
		Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} Wait timeout reached. Waking up to retry.");
		OnCanReserve(null);
	}

	private void OnCanReserve(GridCell target)
	{
		ClearWait();
		Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} Wake up by unreserve");
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
		ClearWait();
		PathRequest request = new(this, worker.GridPosition, goalPos, worker.Direction);
		PathFinding.RequestRoute(request);

		movementState = MovementState.PathPending;

		worker.enabled = false;

		return true;
	}

	public void SetAIMaster(AIWorker worker)
	{
		this.worker = worker;

		//Debug.Log($"Init Grid Position!, GridPos: {worker.GridPosition}");
		// Reserve the current tile from initialization time.
		if (GridService.TryReserve(this, worker.GridPosition) == false)
		{
			Debug.LogError("Failed to reserve initial position for AIWorker.");
		}
	}

	public void OnPathFound(PathResultBuffer pathBuffer)
	{
		if (isNextNodeReserved)
		{
			int3 oldTarget = new((int)targetPos.x, (int)targetPos.y, (int)targetPos.z);
			if (oldTarget.Equals(worker.GridPosition) == false)
				GridService.TryUnreserve(this, oldTarget);

			isNextNodeReserved = false;
		}

		if (pathBuffer == null || pathBuffer.Path?.Count <= 0)
		{
			movementState = MovementState.Failed;
			Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} could not find a route to the goal.");
			//worker.enabled = true;
			enabled = true;
			return;
		}

		if (pathResultBuffer != null)
		{
			pathResultBuffer.SubPathResult = pathBuffer;
			//Debug.Log($"SubPath Set!, To reserve: {pathResultBuffer.CurrentNode.Position}, next: {pathResultBuffer.NextNode?.Position}");
		}
		else
		{
			pathResultBuffer = pathBuffer;
		}

		movementState = MovementState.Moving;

		pathResultBuffer.MoveToNextNode();

		if (pathResultBuffer.IsGoalReached)
		{
			OnArrived();
			return;
		}

		SyncTargetPositionToCurrentNode();
		enabled = true;
	}

	public void RemoveBlocked(FindRoute route)
	{
		if (blockingRoutes.Contains(route))
		{
			blockingRoutes.Remove(route);
			//Debug.Log("Removed blocking route: " + route.name);
		}
	}

	public float GetPathPercent()
	{
		if (pathResultBuffer == null || pathResultBuffer.Path.Count == 0)
			return 0.0f;

		return (float)pathResultBuffer.CurrentIndex / pathResultBuffer.Path.Count;
	}
}
