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

	private enum TileReservationResult
	{
		Success,
		GridBlocked,
		NavigationBlocked,
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
	private bool hasCurrentGoal = false;
	private int pathRequestVersion;
	internal int PathRequestVersion => pathRequestVersion;
	private bool isYieldMove = false;
	private int3 currentGoalPos;
	private int3 pendingGoalPos;
	private GridCell waitingCell = null;
	private int travelledCellsSinceLastConsume;
	private NavigationTransitionReservation navigationReservation;
	private int reservedNavigationCoverageVersion = -1;
	private System.Func<int3, bool> navigationTraversalPredicate;
	public int TravelledCellsSinceLastConsume => Mathf.Max(0, travelledCellsSinceLastConsume);

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
	public int3 CurrentGoalPosition => currentGoalPos;

	public int RemainingDistance => pathResultBuffer != null ? pathResultBuffer.Path.Count - pathResultBuffer.CurrentIndex : int.MaxValue;

	public bool TryGetCurrentGoalCell(out int3 cell)
	{
		if (hasCurrentGoal == false)
		{
			cell = default;
			return false;
		}

		cell = currentGoalPos;
		return true;
	}

	public int3 TrafficFromCell => worker.GridPosition;
	internal bool CanStartTrafficClearing => worker != null && worker.IsOperational &&
		worker.IsPlayerOverride == false && worker.IsWaitingForNavigation == false &&
		isNextNodeReserved == false && isYieldMove == false &&
		movementState == MovementState.Blocked && hasCurrentGoal;
	internal bool CanPassTrafficClearing => worker != null && worker.IsOperational &&
		worker.IsWaitingForNavigation == false && isNextNodeReserved == false && isYieldMove == false &&
		movementState == MovementState.Blocked && hasCurrentGoal;
	internal bool CanStartIdleTrafficClearing => worker != null && worker.IsOperational &&
		worker.IsPlayerOverride == false && worker.IsWaitingForNavigation == false &&
		worker.CurrentTask == null && worker.IsRecovering == false &&
		worker.EffectiveStatusAction == WorkerStatusAction.Idle &&
		isNextNodeReserved == false && isYieldMove == false &&
		pathResultBuffer == null && hasPendingGoal == false &&
		(movementState == MovementState.Idle || movementState == MovementState.Arrived);
	internal bool CanTraverseTrafficCell(int3 cell) => CanTraverseNavigationCell(cell);
	internal bool IsTrafficStepReserved => isNextNodeReserved;
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

	public int CollectUpcomingTrafficCells(ICollection<int3> cells, int maxCount)
	{
		if (cells == null)
			throw new System.ArgumentNullException(nameof(cells));

		return pathResultBuffer != null
			? pathResultBuffer.CollectUpcomingPositions(cells, maxCount)
			: 0;
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
			if (TryRefreshStaleNavigationPath())
				return;

			TileReservationResult reservationResult = TryReserveNextTile();
			if (reservationResult == TileReservationResult.Success)
			{
				isNextNodeReserved = true;
				movementState = MovementState.Moving;
			}
			else
			{
				if (reservationResult == TileReservationResult.NavigationBlocked)
					return;

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
		bool transitionNeedsReconcile = ValidateNavigationTransition(out RobotNavigationWaitReason transitionFailure) == false;
		if (transitionNeedsReconcile)
		{
			// The robot was already between cells when the network changed.
			// Finish this physical step, then derive allocation from the actual arrival cell.
			CancelNavigationTransition();
		}

		int3 previousPos = worker.GridPosition;
		var moveResult = GridService.TryMove(this, worker.GridPosition, pathResultBuffer.CurrentNode.Position);
		if (moveResult != PlacementResult.Success)
		{
			ReleaseReservedNextTile();
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
		bool navigationCommitted = CommitOrReconcileNavigationTransition(
			pathResultBuffer.CurrentNode.Position,
			transitionNeedsReconcile,
			ref transitionFailure);
		reservedNavigationCoverageVersion = -1;
		if (worker.CarryingAbility?.CarryingBox != null && travelledCellsSinceLastConsume < int.MaxValue)
			++travelledCellsSinceLastConsume;

		if (worker.GridPosition.Equals(previousPos) == false)
		{
			bool unreserveRes = GridService.TryUnreserve(this, previousPos);
			//Debug.Log($"[FindRoute] {transform.name} Unreserved {previousPos}. Result: {unreserveRes}");
		}

		isNextNodeReserved = false;
		if (navigationCommitted == false)
		{
			RobotNavigationWaitReason reason = transitionFailure;
			if (reason == RobotNavigationWaitReason.None)
			{
				reason = RobotNavigationWaitReason.Coverage;
				if (worker is RobotWorker robot && GameContext.HasInstance)
					GameContext.Instance.RobotNavigationSvc?.CanRunAutomatic(robot, out reason);
			}
			worker.BeginNavigationWait(reason);
			return;
		}

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
		CancelNavigationTransition();
		reservedNavigationCoverageVersion = -1;
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
		hasCurrentGoal = false;
		isYieldMove = false;
		ResetCurrentPathPlan(true);
		movementState = MovementState.Arrived;
		worker.enabled = true;
		enabled = false;
	}

	private TileReservationResult TryReserveNextTile()
	{
		if (pathResultBuffer == null || pathResultBuffer.IsGoalReached)
		{
			Debug.LogError("PathResultBuffer is null or goal is already reached. Cannot reserve next tile.");
			return TileReservationResult.GridBlocked;
		}

		var nodeToReserve = pathResultBuffer.CurrentNode;
		if (TrafficCoordinator != null && TrafficCoordinator.CanReserveClearingCell(this, nodeToReserve.Position) == false)
			return TileReservationResult.GridBlocked;

		if (worker is RobotWorker robot && robot.IsPlayerOverride == false && GameContext.HasInstance)
		{
			RobotNavigationService navigation = GameContext.Instance.RobotNavigationSvc;
			reservedNavigationCoverageVersion = navigation?.CoverageVersion ?? -1;
			if (navigation != null && navigation.TryReserveTransition(
				robot,
				nodeToReserve.Position,
				out navigationReservation,
				out RobotNavigationWaitReason reason) == false)
			{
				reservedNavigationCoverageVersion = -1;
				worker.BeginNavigationWait(reason);
				return TileReservationResult.NavigationBlocked;
			}
		}

		if (GridService.TryReserve(this, nodeToReserve.Position))
			return TileReservationResult.Success;

		CancelNavigationTransition();
		reservedNavigationCoverageVersion = -1;
		return TileReservationResult.GridBlocked;
	}

	private void OnArrived()
	{
		ClearPlannedPathRegistration();
		ClearPathBuffer();

		if (isYieldMove)
		{
			isYieldMove = false;
			movementState = MovementState.Blocked;
			worker.enabled = false;
			enabled = false;
			TrafficCoordinator.NotifyYieldArrived(this);
			return;
		}

		hasCurrentGoal = false;

		movementState = MovementState.Arrived;
		worker.enabled = true;
		enabled = false;
	}

	private void HandleBlocked()
	{
		TrafficCoordinator.RegisterBlocked(this);
	}

	private PathRequest CreatePathRequest(in int3 start, in int3 goal, FindRoute avoidTarget = null,
		System.Func<int3, bool> traversalPredicate = null)
	{
		++pathRequestVersion;
		int coverageVersion = 0;
		if (worker is RobotWorker robot && robot.IsPlayerOverride == false && robot.RequiresNavigationCoverage && GameContext.HasInstance)
			coverageVersion = GameContext.Instance.RobotNavigationSvc?.CoverageVersion ?? 0;

		navigationTraversalPredicate ??= CanTraverseNavigationCell;
		return new PathRequest(
			this,
			start,
			goal,
			worker.Direction,
			avoidTarget,
			traversalPredicate ?? navigationTraversalPredicate,
			coverageVersion);
	}

	private bool TryRefreshStaleNavigationPath()
	{
		if (pathResultBuffer == null ||
			worker is not RobotWorker robot ||
			robot.IsPlayerOverride ||
			robot.RequiresNavigationCoverage == false ||
			GameContext.HasInstance == false)
		{
			return false;
		}

		RobotNavigationService navigation = GameContext.Instance.RobotNavigationSvc;
		if (navigation == null || pathResultBuffer.NavigationCoverageVersion == navigation.CoverageVersion)
			return false;

		if (navigation.CanRunAutomatic(robot, out RobotNavigationWaitReason reason) == false)
		{
			worker.BeginNavigationWait(reason);
			return true;
		}

		navigationTraversalPredicate ??= CanTraverseNavigationCell;
		if (pathResultBuffer.AreRemainingPositionsValid(navigationTraversalPredicate))
		{
			pathResultBuffer.MarkNavigationCoverageVersion(navigation.CoverageVersion);
			return false;
		}

		RequestFreshRouteToCurrentGoal();
		return true;
	}

	private bool CanTraverseNavigationCell(int3 position)
	{
		if (worker is not RobotWorker robot || robot.IsPlayerOverride || GameContext.HasInstance == false)
			return true;

		RobotNavigationService navigation = GameContext.Instance.RobotNavigationSvc;
		return navigation == null || navigation.CanRobotTraverseCell(robot, position);
	}

	private bool CanBeginAutomaticRoute(in int3 goalPosition)
	{
		if (worker is not RobotWorker robot || robot.IsPlayerOverride || GameContext.HasInstance == false)
			return true;

		RobotNavigationService navigation = GameContext.Instance.RobotNavigationSvc;
		if (navigation == null || navigation.CanBeginAutomaticRoute(robot, goalPosition, out RobotNavigationWaitReason reason))
			return true;

		worker.BeginNavigationWait(reason);
		return false;
	}

	private bool ValidateNavigationTransition(out RobotNavigationWaitReason reason)
	{
		reason = RobotNavigationWaitReason.None;
		if (worker is RobotWorker robot &&
			robot.IsPlayerOverride == false &&
			robot.RequiresNavigationCoverage &&
			reservedNavigationCoverageVersion >= 0 &&
			GameContext.HasInstance)
		{
			RobotNavigationService versionService = GameContext.Instance.RobotNavigationSvc;
			if (versionService != null && versionService.CoverageVersion != reservedNavigationCoverageVersion)
			{
				reason = RobotNavigationWaitReason.Coverage;
				return false;
			}
		}

		if (navigationReservation.RequiresCommit == false || GameContext.HasInstance == false)
			return true;

		RobotNavigationService navigation = GameContext.Instance.RobotNavigationSvc;
		return navigation == null || navigation.ValidateTransition(navigationReservation, out reason);
	}

	private bool CommitNavigationTransition()
	{
		if (navigationReservation.RequiresCommit == false)
			return true;

		NavigationTransitionReservation reservation = navigationReservation;
		navigationReservation = default;
		return GameContext.HasInstance == false ||
			GameContext.Instance.RobotNavigationSvc == null ||
			GameContext.Instance.RobotNavigationSvc.CommitTransition(reservation);
	}

	private bool CommitOrReconcileNavigationTransition(
		in int3 arrivedPosition,
		bool forceReconcile,
		ref RobotNavigationWaitReason reason)
	{
		if (worker is not RobotWorker robot || GameContext.HasInstance == false)
			return true;

		RobotNavigationService navigation = GameContext.Instance.RobotNavigationSvc;
		if (navigation == null)
			return true;

		if (robot.IsPlayerOverride)
		{
			navigation.ReconcileManualMovement(robot, arrivedPosition);
			return true;
		}

		if (forceReconcile == false && CommitNavigationTransition())
			return true;

		CancelNavigationTransition();
		return navigation.ReconcileExternalRelocation(robot, arrivedPosition, out reason);
	}

	private void CancelNavigationTransition()
	{
		if (navigationReservation.RequiresCommit == false)
			return;

		if (GameContext.HasInstance)
			GameContext.Instance.RobotNavigationSvc?.CancelTransition(navigationReservation);
		navigationReservation = default;
	}

	public bool RequestSubPath(in int3 goalPos, FindRoute avoidTarget)
	{
		if (CanBeginAutomaticRoute(goalPos) == false)
			return false;

		if (avoidTarget != null)
		{
			blockingRoutes.Add(avoidTarget);
		}

		PathRequest request = CreatePathRequest(worker.GridPosition, goalPos, avoidTarget);
		PathFinding.RequestRoute(request);

		enabled = false;
		pathResultBuffer.MoveToNextNode();
		RefreshPlannedPathRegistration();

		return true;
	}

	public bool RequestFreshRouteToCurrentGoal()
	{
		if (TryGetCurrentGoalCell(out var goalCell) == false)
			return false;
		if (CanBeginAutomaticRoute(goalCell) == false)
			return false;

		ClearWait();
		isYieldMove = false;
		stopAfterCurrentStep = false;
		hasPendingGoal = false;
		ResetCurrentPathPlan(true);

		PathRequest request = CreatePathRequest(worker.GridPosition, goalCell);
		PathFinding.RequestRoute(request);

		movementState = MovementState.PathPending;
		worker.enabled = false;
		enabled = false;

		return true;
	}

	public bool RequestYieldMove(in int3 yieldCell) => RequestYieldMove(yieldCell, false);

	internal bool RequestClearingStep(in int3 yieldCell) => RequestYieldMove(yieldCell, true);

	private bool RequestYieldMove(in int3 yieldCell, bool singleStep)
	{
		// A bounded clearing step may temporarily move a genuinely idle worker with no work goal.
		if (hasCurrentGoal == false && singleStep == false)
			return false;
		if (CanBeginAutomaticRoute(yieldCell) == false)
			return false;

		ClearWait();
		stopAfterCurrentStep = false;
		hasPendingGoal = false;
		isYieldMove = true;
		ResetCurrentPathPlan(true);

		int3 start = worker.GridPosition;
		int3 target = yieldCell;
		System.Func<int3, bool> stepPredicate = singleStep
			? cell => (cell.Equals(start) || cell.Equals(target)) && CanTraverseNavigationCell(cell)
			: null;
		PathRequest request = CreatePathRequest(start, target, traversalPredicate: stepPredicate);
		PathFinding.RequestRoute(request);

		movementState = MovementState.PathPending;
		worker.enabled = false;
		enabled = false;

		return true;
	}

	public bool RequestIdleYieldMove(in int3 yieldCell)
	{
		if (CanBeginAutomaticRoute(yieldCell) == false)
			return false;

		ClearWait();
		stopAfterCurrentStep = false;
		hasPendingGoal = false;
		isYieldMove = true;
		ResetCurrentPathPlan(true);

		PathRequest request = CreatePathRequest(worker.GridPosition, yieldCell);
		PathFinding.RequestRoute(request);

		movementState = MovementState.PathPending;
		worker.enabled = false;
		enabled = false;

		return true;
	}

	public void SuspendForTraffic(WorkerStatusAction blockAction = WorkerStatusAction.TrafficBlock)
	{
		ClearWait();
		movementState = MovementState.Blocked;
		worker?.BeginTrafficBlock(blockAction);
		enabled = false;
	}

	public void ResumeFromTraffic()
	{
		ClearWait();
		worker?.EndTrafficBlock();
		enabled = true;
	}

	public void ClearTrafficBlockState()
	{
		worker?.EndTrafficBlock();
	}

	public void SuspendForNavigation()
	{
		if (GameContext.HasInstance)
			TrafficCoordinator.CancelRoute(this);

		ClearWait();
		stopAfterCurrentStep = false;
		hasPendingGoal = false;
		isYieldMove = false;
		ResetCurrentPathPlan(true);
		movementState = MovementState.Blocked;
		worker?.EndTrafficBlock();
		enabled = false;
	}

	public bool ResumeFromNavigation()
	{
		if (hasCurrentGoal == false)
		{
			movementState = MovementState.Idle;
			return false;
		}

		return RequestFreshRouteToCurrentGoal();
	}

	internal void RestoreNavigationGoal(in int3 goal)
	{
		currentGoalPos = goal;
		hasCurrentGoal = true;
		hasPendingGoal = false;
		stopAfterCurrentStep = false;
		isYieldMove = false;
		movementState = MovementState.Blocked;
	}

	public void CompleteIdleYieldMove()
	{
		++pathRequestVersion;
		ClearWait();
		worker?.EndTrafficBlock();
		hasCurrentGoal = false;
		hasPendingGoal = false;
		isYieldMove = false;
		ResetCurrentPathPlan(true);
		movementState = MovementState.Idle;
		if (worker != null)
			worker.enabled = true;
		enabled = false;
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
		isYieldMove = false;
		stopAfterCurrentStep = false;
		currentGoalPos = goalPos;
		hasCurrentGoal = true;
		if (CanBeginAutomaticRoute(goalPos) == false)
			return false;
		ResetCurrentPathPlan(true);
		PathRequest request = CreatePathRequest(worker.GridPosition, goalPos);
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

	public void PauseForExternalTransit()
	{
		ClearWait();
		stopAfterCurrentStep = false;
		hasPendingGoal = false;
		hasCurrentGoal = false;
		isYieldMove = false;
		ResetCurrentPathPlan(true);
		movementState = MovementState.Idle;
		enabled = false;
	}

	public void CancelCurrentRoute()
	{
		++pathRequestVersion;
		if (travelledCellsSinceLastConsume > 0)
			worker?.ApplyCarriedMovementFatigue(ConsumeTravelledCells());

		if (GameContext.HasInstance)
			TrafficCoordinator.CancelRoute(this);

		ClearWait();
		stopAfterCurrentStep = false;
		hasPendingGoal = false;
		hasCurrentGoal = false;
		isYieldMove = false;
		ResetCurrentPathPlan(true);
		movementState = MovementState.Idle;
		enabled = false;
	}

	public void ConsumeArrivedGoal()
	{
		if (movementState != MovementState.Arrived)
			return;

		hasCurrentGoal = false;
		hasPendingGoal = false;
		movementState = MovementState.Idle;
	}

	public int ConsumeTravelledCells()
	{
		int result = travelledCellsSinceLastConsume;
		travelledCellsSinceLastConsume = 0;
		return result;
	}

	public void RestoreTravelledCells(int travelledCells)
		=> travelledCellsSinceLastConsume = Mathf.Max(0, travelledCells);

	public void SetAIMaster(AIWorker worker)
	{
		this.worker = worker;
		navigationTraversalPredicate = CanTraverseNavigationCell;

		//Debug.Log($"Init Grid Position!, GridPos: {worker.GridPosition}");
		// Reserve the current tile from initialization time.
		if (GridService.TryReserve(this, worker.GridPosition) == false)
		{
			Debug.LogError("Failed to reserve initial position for AIWorker.");
		}
	}

	public void OnPathFound(PathResultBuffer pathBuffer, int? requestVersion = null)
	{
		if (requestVersion.HasValue && requestVersion.Value != pathRequestVersion)
		{
			pathBuffer?.Clear();
			return;
		}

		ReleaseReservedNextTile();
		if (worker != null && worker.IsWaitingForNavigation)
		{
			pathBuffer?.Clear();
			enabled = false;
			return;
		}

		if (pathBuffer == null || pathBuffer.Path?.Count <= 0)
		{
			if (isYieldMove)
			{
				isYieldMove = false;
				movementState = MovementState.Blocked;
				TrafficCoordinator.NotifyYieldMoveFailed(this);
				enabled = false;
				return;
			}

			if (pathBuffer != null && pathBuffer.WasTraversalRejected && worker is RobotWorker robot && robot.IsPlayerOverride == false)
			{
				pathBuffer.Clear();
				worker.BeginNavigationWait(RobotNavigationWaitReason.Coverage);
				return;
			}

			movementState = MovementState.Failed;
			RefreshPlannedPathRegistration();
			Debug.Log($"[FindRoute] {transform.name}, ID: {worker.WorkerID} could not find a route to the goal.");
			worker.enabled = true;
			enabled = false;
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
