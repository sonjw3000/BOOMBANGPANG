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
	private static TrafficCoordinator TrafficCoordinator => GameContext.Instance.TrafficCoordinator;

	private AIWorker worker;
	private MovementState movementState = MovementState.Idle;
	private PathResultBuffer pathResultBuffer = null;
	private Vector3 targetPos = Vector3.zero;
	private bool isNextNodeReserved = false;
	private bool stopAfterCurrentStep = false;
	private bool hasPendingGoal = false;
	private int3 pendingGoalPos;
	private GridCell waitingCell = null;

	private HashSet<FindRoute> blockingRoutes = new();
	private readonly HashSet<int3> plannedPathCells = new();
	private readonly HashSet<int3> plannedPathScratch = new();


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

	private void OnDestroy()
	{
		ClearWait();
		ClearPlannedPathRegistration();
		ReleaseReservedNextTile();
		ClearPathBuffer();

		if (GameContext.HasInstance && worker != null)
		{
			GridService.TryUnreserve(this, worker.GridPosition);
		}
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
	public bool HasPlannedPath => plannedPathCells.Count > 0;
	public bool IsGoal => movementState == MovementState.Arrived;
	public bool IsWaiting => waitingCell != null;
	public MovementState CurrentMovementState => movementState;
	public AIWorker Worker => worker;
	public bool HasActiveGoal => pathResultBuffer != null || waitingCell != null || hasPendingGoal || movementState == MovementState.PathPending || movementState == MovementState.Moving || movementState == MovementState.Arrived;
	public int3 CurrentGoalPosition => new((int)targetPos.x, (int)targetPos.y, (int)targetPos.z);

	public int RemainingDistance => pathResultBuffer != null ? pathResultBuffer.Path.Count - pathResultBuffer.CurrentIndex : int.MaxValue;

	public int3 TrafficFromCell => worker.GridPosition;
	public bool TryGetTrafficToCell(out int3 cell)
	{
		if (pathResultBuffer == null || pathResultBuffer.IsGoalReached)
		{
			cell = default;
			return false;
		}

		cell = pathResultBuffer.CurrentNode.Position;
		return true;
	}

	public bool TryGetFutureToCell(out int3 cell)
	{
		cell = default;
		if (pathResultBuffer == null || pathResultBuffer.IsGoalReached)
			return false;

		var nextNode = pathResultBuffer.NextNode;

		if (nextNode == null)
			return false;

		cell = nextNode.Position;
		return true;
	}

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

		isNextNodeReserved = false;

		if (hasPendingGoal)
		{
			int3 goalPos = pendingGoalPos;
			hasPendingGoal = false;
			ApplyGoalPosition(goalPos);
			return;
		}

		if (stopAfterCurrentStep)
		{
			stopAfterCurrentStep = false;
			StopCurrentPathAtCurrentTile();
			return;
		}

		pathResultBuffer.MoveToNextNode();
		RefreshPlannedPathRegistration();
		SyncTargetPositionToCurrentNode();
	}

	private void ReleaseReservedNextTile()
	{
		if (isNextNodeReserved == false || worker == null || GameContext.HasInstance == false)
			return;

		int3 reservedPos = new((int)targetPos.x, (int)targetPos.y, (int)targetPos.z);
		if (reservedPos.Equals(worker.GridPosition) == false)
		{
			GridService.TryUnreserve(this, reservedPos);
		}

		isNextNodeReserved = false;
	}

	private void ClearPathBuffer()
	{
		if (pathResultBuffer == null)
			return;

		pathResultBuffer.Clear();
		pathResultBuffer = null;
	}

	private void ClearPlannedPathRegistration()
	{
		if (GameContext.HasInstance)
		{
			foreach (var cellPos in plannedPathCells)
			{
				GridService.UnregisterPlannedPath(this, cellPos);
			}
		}

		plannedPathCells.Clear();
		plannedPathScratch.Clear();
	}

	private void RefreshPlannedPathRegistration()
	{
		plannedPathScratch.Clear();
		pathResultBuffer?.CollectRemainingPositions(plannedPathScratch);

		if (GameContext.HasInstance)
		{
			foreach (var cellPos in plannedPathCells)
			{
				if (plannedPathScratch.Contains(cellPos))
					continue;

				GridService.UnregisterPlannedPath(this, cellPos);
			}

			foreach (var cellPos in plannedPathScratch)
			{
				if (plannedPathCells.Contains(cellPos))
					continue;

				GridService.RegisterPlannedPath(this, cellPos);
			}
		}

		plannedPathCells.Clear();
		foreach (var cellPos in plannedPathScratch)
		{
			plannedPathCells.Add(cellPos);
		}
	}

	private void ResetCurrentPathPlan(bool clearBlockingRoutes)
	{
		ReleaseReservedNextTile();
		ClearPlannedPathRegistration();
		ClearPathBuffer();

		if (clearBlockingRoutes)
		{
			blockingRoutes.Clear();
		}
	}

	private void StopCurrentPathAtCurrentTile()
	{
		ClearWait();
		hasPendingGoal = false;
		ResetCurrentPathPlan(true);
		movementState = MovementState.Arrived;
		worker.enabled = true;
		enabled = false;
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
		ClearPlannedPathRegistration();
		ClearPathBuffer();

		movementState = MovementState.Arrived;
		worker.enabled = true;
		enabled = false;
	}

	private void HandleBlocked()
	{
		TrafficCoordinator.RegisterBlocked(this);
	}

	public bool RequestSubPath(in int3 goalPos, FindRoute avoidTarget)
	{
		if (avoidTarget != null)
		{
			blockingRoutes.Add(avoidTarget);
		}

		PathRequest request = new(this, worker.GridPosition, goalPos, worker.Direction, avoidTarget);
		PathFinding.RequestRoute(request);

		enabled = false;
		pathResultBuffer.MoveToNextNode();
		RefreshPlannedPathRegistration();

		return true;
	}

	public void SuspendForTraffic()
	{
		ClearWait();
		movementState = MovementState.Blocked;
		enabled = false;
	}

	public void ResumeFromTraffic()
	{
		ClearWait();
		enabled = true;
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
		if (isNextNodeReserved)
		{
			pendingGoalPos = goalPos;
			hasPendingGoal = true;
			stopAfterCurrentStep = false;
			worker.enabled = false;
			return true;
		}

		return ApplyGoalPosition(goalPos);
	}

	private bool ApplyGoalPosition(in int3 goalPos)
	{
		ClearWait();
		stopAfterCurrentStep = false;
		ResetCurrentPathPlan(true);
		PathRequest request = new(this, worker.GridPosition, goalPos, worker.Direction);
		PathFinding.RequestRoute(request);

		movementState = MovementState.PathPending;

		worker.enabled = false;

		return true;
	}

	public void StopAfterCurrentStep()
	{
		if (pathResultBuffer == null && waitingCell == null)
			return;

		if (isNextNodeReserved)
		{
			stopAfterCurrentStep = true;
			hasPendingGoal = false;
			return;
		}

		StopCurrentPathAtCurrentTile();
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
		ReleaseReservedNextTile();

		if (pathBuffer == null || pathBuffer.Path?.Count <= 0)
		{
			movementState = MovementState.Failed;
			RefreshPlannedPathRegistration();
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
		RefreshPlannedPathRegistration();
		enabled = true;
	}

	public void RemoveBlocked(FindRoute route)
	{
		if (blockingRoutes.Contains(route))
		{
			blockingRoutes.Remove(route);
			if (GameContext.HasInstance)
			{
				TrafficCoordinator.NotifyAvoidTargetCleared(this, route);
			}
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
