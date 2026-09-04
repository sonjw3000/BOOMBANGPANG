using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TrafficCoordinatorIdleYieldEditModeTests
{
	private static readonly int3 Source = new(20, 0, 12);
	private static readonly int3 BlockedCell = new(19, 0, 12);
	private static readonly int3 Destination = new(19, 0, 11);
	private static readonly int3 YieldCell = new(18, 0, 12);
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private GridService grid;
	private PathFindingService pathFinding;
	private TrafficCoordinator coordinator;
	private FindRoute requestedRoute;
	private FindRoute blockerRoute;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)InstanceField.GetValue(null);
		GameObject contextObject = CreateObject("Idle Yield Test Context");
		GameContext context = contextObject.AddComponent<GameContext>();
		grid = CreateObject("Idle Yield Test Grid").AddComponent<GridService>();
		grid.BuildDefaultMap();
		pathFinding = CreateObject("Idle Yield Test Pathfinding").AddComponent<PathFindingService>();
		coordinator = CreateObject("Idle Yield Test Coordinator").AddComponent<TrafficCoordinator>();
		WMSystem warehouse = CreateObject("Traffic Test Warehouse").AddComponent<WMSystem>();
		WorkPolicyService policy = CreateObject("Traffic Test Policy").AddComponent<WorkPolicyService>();
		SetField(typeof(WMSystem), warehouse, "workPolicyService", policy);
		SetField(typeof(GameContext), context, "warehouseManagement", warehouse);
		SetField(typeof(GameContext), context, "gridService", grid);
		SetField(typeof(GameContext), context, "pathFindingService", pathFinding);
		SetField(typeof(GameContext), context, "trafficCoordinator", coordinator);
		InstanceField.SetValue(null, context);
		Invoke(typeof(PathFindingService), pathFinding, "Start");
		requestedRoute = CreateWorker("Requested Worker", Source);
		blockerRoute = CreateWorker("Idle Blocker", BlockedCell);
	}

	[TearDown]
	public void TearDown()
	{
		InstanceField.SetValue(null, null);
		try
		{
			for (int i = createdObjects.Count - 1; i >= 0; --i)
			{
				if (createdObjects[i] != null)
					Object.DestroyImmediate(createdObjects[i]);
			}
			createdObjects.Clear();
		}
		finally
		{
			InstanceField.SetValue(null, previousContext);
		}
	}

	[Test]
	public void ResolveRequest_IdleIntermediateBlocker_YieldsBeforeRequestingDetour()
	{
		SetRequestedPath(Source, BlockedCell, Destination);

		ResolveRequest();

		AssertYieldStarted();
		Assert.That(requestedRoute.BlockingRoutes, Is.Empty, "The requester must not start a detour.");
		Assert.That(requestedRoute.TryGetTrafficToCell(out int3 next), Is.True);
		Assert.That(next, Is.EqualTo(BlockedCell), "Waiting must preserve the original path cursor.");
	}

	[Test]
	public void ResolveRequest_IdleDestinationBlocker_StillYields()
	{
		SetRequestedPath(Source, BlockedCell);

		ResolveRequest();

		AssertYieldStarted();
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ResolveRequest_IdleYieldCancellation_ReturnsBlockerToIdle(bool cancelYieldingRoute)
	{
		SetRequestedPath(Source, BlockedCell, Destination);
		ResolveRequest();
		AssertYieldStarted();

		(cancelYieldingRoute ? blockerRoute : requestedRoute).CancelCurrentRoute();

		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.False);
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Idle));
		Assert.That(blockerRoute.TryGetCurrentGoalCell(out _), Is.False);
		Assert.That(blockerRoute.Worker.WorkerState.Action, Is.EqualTo(WorkerStatusAction.Idle));
	}

	[Test]
	public void ResolveRequest_IdleIntermediateBlockerWithoutYieldSpace_FallsBackToDetour()
	{
		SetRequestedPath(Source, BlockedCell, Destination);
		BlockYieldCells();

		ResolveRequest();

		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.False);
		Assert.That(coordinator.IsWaitingForTraffic(requestedRoute), Is.False);
		Assert.That(requestedRoute.BlockingRoutes, Does.Contain(blockerRoute));
		PathRequest request = GetOnlyPathRequest();
		Assert.That(request.target, Is.SameAs(requestedRoute));
		Assert.That(request.IsSubPathRequest, Is.True);
		Assert.That(request.AvoidTarget, Is.SameAs(blockerRoute));
		Assert.That(request.endPosition, Is.EqualTo(Destination));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ResolveRequest_IdleBlockerWithOccupiedYieldPath_StartsClearingChainBeforeDetour(bool playerControlledRequester)
	{
		FindRoute tailRoute = PrepareIdleClearingChain();
		if (playerControlledRequester)
			SetField(typeof(AIWorker), requestedRoute.Worker, "controlMode", WorkerControlMode.PlayerOverride);

		ResolveRequest();

		Assert.That(GetOnlyPathRequest().target, Is.SameAs(tailRoute), "The rear worker must move before the idle blocker.");
		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.True);
		Assert.That(coordinator.IsYieldHeld(tailRoute), Is.True);
		Assert.That(coordinator.IsWaitingForTraffic(requestedRoute), Is.True);
		Assert.That(requestedRoute.BlockingRoutes, Is.Empty, "The requester must not detour while a complete clearing plan exists.");
	}

	[Test]
	public void ClearingPlan_IdleParticipant_ReturnsToIdleAfterPassage()
	{
		FindRoute tailRoute = PrepareIdleClearingChain();
		ResolveRequest();
		CompleteClearingSteps();

		Assert.That(blockerRoute.TryGetCurrentGoalCell(out _), Is.False);
		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.True);
		MoveWorker(requestedRoute, new int3(18, 0, 11));
		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");

		AssertClearingOwnershipCleared();
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Idle));
		Assert.That(blockerRoute.TryGetCurrentGoalCell(out _), Is.False);
		Assert.That(blockerRoute.Worker.WorkerState.Action, Is.EqualTo(WorkerStatusAction.Idle));
		Assert.That(blockerRoute.Worker.enabled, Is.True);
		Assert.That(tailRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
	}

	[Test]
	public void ClearingPlan_IdleParticipant_ReturnsToIdleWhenPlanAborts()
	{
		PrepareIdleClearingChain();
		ResolveRequest();
		SetField(typeof(TrafficCoordinator), coordinator, "clearingPlanTimeoutSeconds", -1f);

		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");

		AssertClearingOwnershipCleared();
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Idle));
		Assert.That(blockerRoute.TryGetCurrentGoalCell(out _), Is.False);
		Assert.That(blockerRoute.Worker.WorkerState.Action, Is.EqualTo(WorkerStatusAction.Idle));
		Assert.That(blockerRoute.Worker.enabled, Is.True);
	}

	[Test]
	public void ClearingPlan_IdleParticipantActiveRequestIsIgnoredAfterAbort()
	{
		PrepareIdleClearingChain();
		ResolveRequest();
		for (int step = 0; step < 10 && GetOnlyPathRequest().target != blockerRoute; ++step)
			CompleteCurrentClearingStep();
		Assert.That(GetOnlyPathRequest().target, Is.SameAs(blockerRoute));
		int staleVersion = (int)GetField(typeof(FindRoute), blockerRoute, "pathRequestVersion");
		SetField(typeof(TrafficCoordinator), coordinator, "clearingPlanTimeoutSeconds", -1f);

		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");
		blockerRoute.OnPathFound(null, staleVersion);

		AssertClearingOwnershipCleared();
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Idle));
		Assert.That(blockerRoute.TryGetCurrentGoalCell(out _), Is.False);
	}

	[Test]
	public void ResolveRequest_IdleDestinationBlockerWithoutYieldSpace_KeepsWaiting()
	{
		SetRequestedPath(Source, BlockedCell);
		BlockYieldCells();

		ResolveRequest();

		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.False);
		Assert.That(coordinator.IsWaitingForTraffic(requestedRoute), Is.True);
		Assert.That(ActiveJobs, Is.Empty);
		Assert.That(requestedRoute.BlockingRoutes, Is.Empty);
	}

	[Test]
	public void ClearingPlan_OccupiedYieldPath_StartsWithTailWorker()
	{
		int3 releaseCell = new(18, 0, 11);
		FindRoute tailRoute = PrepareClearingChain();
		bool started = StartClearingPlan();

		Assert.That(started, Is.True);
		PathRequest request = GetOnlyPathRequest();
		Assert.That(request.target, Is.SameAs(tailRoute), "The farthest blocker must clear before the first blocker moves.");
		Assert.That(request.endPosition, Is.Not.EqualTo(BlockedCell));
		Assert.That(request.endPosition, Is.Not.EqualTo(YieldCell));
		Assert.That(request.endPosition, Is.Not.EqualTo(releaseCell));
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Blocked));
		Assert.That(tailRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		AssertClearingPlanEndsOutsideProtectedCells();
	}

	[Test]
	public void ClearingPlan_WhenBoundedSearchCannotFinish_DoesNotMoveAnyWorker()
	{
		FindRoute tailRoute = PrepareClearingChain();
		SetField(typeof(TrafficCoordinator), coordinator, "maxClearingMoves", 2);
		bool started = StartClearingPlan();

		Assert.That(started, Is.False);
		Assert.That(ActiveJobs, Is.Empty);
		Assert.That(requestedRoute.TrafficFromCell, Is.EqualTo(Source));
		Assert.That(blockerRoute.TrafficFromCell, Is.EqualTo(BlockedCell));
		Assert.That(tailRoute.TrafficFromCell, Is.EqualTo(YieldCell));
		AssertClearingOwnershipCleared();
	}

	[Test]
	public void ClearingPlan_StaleQueueEntry_DoesNotSuspendActiveStep()
	{
		FindRoute tail = PrepareClearingChain();
		Assert.That(StartClearingPlan(), Is.True);
		Invoke(typeof(TrafficCoordinator), coordinator, "Update");
		Assert.That(tail.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		Assert.That(coordinator.IsWaitingForTraffic(requestedRoute), Is.True);
		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.True);
	}

	[Test]
	public void ClearingPlan_LogicalReservation_BlocksUnrelatedMovementIntoEmptyCell()
	{
		PrepareClearingChain();
		Assert.That(StartClearingPlan(), Is.True);
		int3 reservedCell = GetOnlyPathRequest().endPosition;
		FindRoute outsider = CreateWorker("Unrelated Worker", new int3(16, 0, 12));
		SetRoutePath(outsider, outsider.TrafficFromCell, reservedCell);
		Assert.That(grid.GetBlockingFindRoute(reservedCell), Is.Null);
		Assert.That(coordinator.CanReserveClearingCell(outsider, reservedCell), Is.False);
		Assert.That(InvokeResult(typeof(FindRoute), outsider, "TryReserveNextTile").ToString(), Is.EqualTo("GridBlocked"));
		Invoke(typeof(TrafficCoordinator), coordinator, "ResolveRequest", outsider);
		Assert.That(coordinator.IsWaitingForTraffic(outsider), Is.True);
		Assert.That(grid.GetReservedFindRoute(reservedCell), Is.Null);
	}

	[Test]
	public void ClearingPlan_HeadOnInheritedPriority_ReleasesOnPassingRouteNotOwner()
	{
		FindRoute tail = PrepareClearingChain();
		FindRoute owner = CreateWorker("Priority Owner", new int3(21, 0, 12));
		SetRoutePath(owner, owner.TrafficFromCell, Source);
		owner.Worker.SetWorkerID(23);
		requestedRoute.Worker.SetWorkerID(4);
		blockerRoute.Worker.SetWorkerID(24);
		tail.Worker.SetWorkerID(22);
		Invoke(typeof(TrafficCoordinator), coordinator, "RegisterClearingRoute", requestedRoute, owner);

		Invoke(typeof(TrafficCoordinator), coordinator, "ResolveRequest", requestedRoute);
		Assert.That(GetOnlyPathRequest().target, Is.SameAs(tail));
		Assert.That(InvokeResult(typeof(TrafficCoordinator), coordinator, "GetEffectivePriorityRoute", tail), Is.SameAs(owner));
		CompleteClearingSteps();
		Assert.That(coordinator.IsYieldHeld(tail), Is.True);
		Assert.That(coordinator.CanReserveClearingCell(requestedRoute, BlockedCell), Is.True);

		MoveWorker(requestedRoute, BlockedCell);
		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");
		Assert.That(coordinator.IsYieldHeld(tail), Is.True);
		MoveWorker(requestedRoute, YieldCell);
		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");
		Assert.That(coordinator.IsYieldHeld(tail), Is.True);
		MoveWorker(requestedRoute, new int3(18, 0, 11));
		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");

		AssertClearingOwnershipCleared();
		Assert.That(owner.TrafficFromCell, Is.EqualTo(new int3(21, 0, 12)), "The inherited owner never moved.");
		Assert.That(InvokeResult(typeof(TrafficCoordinator), coordinator, "GetEffectivePriorityRoute", requestedRoute), Is.SameAs(owner));
		Assert.That(blockerRoute.CurrentGoalPosition, Is.EqualTo(Source));
		Assert.That(tail.CurrentGoalPosition, Is.EqualTo(Source));
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		Assert.That(tail.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		Assert.That(blockerRoute.Worker.WorkerState.Action, Is.Not.EqualTo(WorkerStatusAction.TrafficBlock));
	}

	[TestCase("cancel")]
	[TestCase("failure")]
	[TestCase("timeout")]
	[TestCase("path-change")]
	public void ClearingPlan_Interrupted_ReleasesOwnershipAndResumesOtherWorkers(string interruption)
	{
		FindRoute tail = PrepareClearingChain();
		Assert.That(StartClearingPlan(), Is.True);
		int staleVersion = (int)GetField(typeof(FindRoute), tail, "pathRequestVersion");
		if (interruption == "cancel")
		{
			blockerRoute.CancelCurrentRoute();
			Assert.That(blockerRoute.Worker.WorkerState.Action, Is.Not.EqualTo(WorkerStatusAction.TrafficBlock));
		}
		else if (interruption == "failure")
			coordinator.NotifyYieldMoveFailed(tail);
		else if (interruption == "timeout")
		{
			SetField(typeof(TrafficCoordinator), coordinator, "clearingPlanTimeoutSeconds", -1f);
			Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");
		}
		else
		{
			requestedRoute.RequestFreshRouteToCurrentGoal();
			Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");
		}

		AssertClearingOwnershipCleared();
		Assert.That(coordinator.IsYieldHeld(tail), Is.False);
		Assert.That(tail.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		Assert.That(tail.Worker.WorkerState.Action, Is.Not.EqualTo(WorkerStatusAction.TrafficBlock));
		tail.OnPathFound(null, staleVersion);
		Assert.That(tail.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending), "A stale clearing result must not resume the cancelled plan.");
	}

	[TestCase("moving")]
	[TestCase("reserved-step")]
	[TestCase("other-plan")]
	public void ClearingPlan_IneligibleTail_DoesNotStart(string state)
	{
		FindRoute tail = PrepareClearingChain();
		if (state == "moving")
			SetField(typeof(FindRoute), tail, "movementState", FindRoute.MovementState.Moving);
		else if (state == "reserved-step")
			SetField(typeof(FindRoute), tail, "isNextNodeReserved", true);
		else
			Invoke(typeof(TrafficCoordinator), coordinator, "RegisterClearingRoute", tail, requestedRoute);
		Assert.That(StartClearingPlan(), Is.False);
		Assert.That(ActiveJobs, Is.Empty);
		AssertClearingOwnershipCleared();
	}

	[Test]
	public void ClearingPlan_TimeoutDuringReservedStep_FinishesStepBeforeReleasingCells()
	{
		FindRoute tail = PrepareClearingChain();
		Assert.That(StartClearingPlan(), Is.True);
		int3 target = GetOnlyPathRequest().endPosition;
		Invoke(typeof(PathFindingService), pathFinding, "Update");
		Assert.That(ActiveJobs, Is.Empty);
		Assert.That(InvokeResult(typeof(FindRoute), tail, "TryReserveNextTile").ToString(), Is.EqualTo("Success"));
		SetField(typeof(FindRoute), tail, "isNextNodeReserved", true);
		SetField(typeof(TrafficCoordinator), coordinator, "clearingPlanTimeoutSeconds", -1f);
		Invoke(typeof(TrafficCoordinator), coordinator, "ProcessClearingPlans");
		Assert.That(coordinator.IsYieldHeld(tail), Is.True);
		Assert.That(grid.GetReservedFindRoute(target), Is.SameAs(tail));
		MoveWorker(tail, target);
		SetField(typeof(FindRoute), tail, "isNextNodeReserved", false);
		Invoke(typeof(FindRoute), tail, "OnArrived");
		AssertClearingOwnershipCleared();
	}

	[Test]
	public void ClearingPlan_FourWaitingBlockers_CanClearWithinDefaultBounds()
	{
		List<FindRoute> blockers = new() { blockerRoute };
		for (int x = 18; x >= 16; --x)
			blockers.Add(CreateWorker($"Chain {x}", new int3(x, 0, 12)));
		SetRoutePath(requestedRoute, Source, BlockedCell, YieldCell, new int3(17, 0, 12), new int3(16, 0, 12), new int3(16, 0, 11));
		coordinator.RegisterBlocked(requestedRoute);
		foreach (FindRoute route in blockers)
		{
			SetRoutePath(route, route.TrafficFromCell, route.TrafficFromCell + new int3(1, 0, 0));
			coordinator.RegisterBlocked(route);
		}
		Assert.That(StartClearingPlan(), Is.True);
		var protectedCells = GetActiveClearingProtectedCells();
		CompleteClearingSteps();
		foreach (FindRoute route in blockers)
		{
			Assert.That(coordinator.IsYieldHeld(route), Is.True);
			Assert.That(protectedCells.Contains(route.TrafficFromCell), Is.False);
		}
	}

	private HashSet<int3> GetActiveClearingProtectedCells()
	{
		object plan = null;
		foreach (object candidate in (System.Collections.IEnumerable)GetField(typeof(TrafficCoordinator), coordinator, "clearingPlans"))
			plan = candidate;
		Assert.That(plan, Is.Not.Null);
		object definition = GetAnyField(plan.GetType(), plan, "Definition");
		return new HashSet<int3>((HashSet<int3>)GetAnyField(definition.GetType(), definition, "ProtectedCells"));
	}

	private FindRoute PrepareClearingChain()
	{
		FindRoute tail = CreateWorker("Clearing Tail", YieldCell);
		SetRoutePath(requestedRoute, Source, BlockedCell, YieldCell, new int3(18, 0, 11));
		SetRoutePath(blockerRoute, BlockedCell, Source);
		SetRoutePath(tail, YieldCell, BlockedCell, Source);
		BlockStaticCell(new int3(19, 0, 11));
		BlockStaticCell(new int3(19, 0, 13));
		coordinator.RegisterBlocked(requestedRoute);
		coordinator.RegisterBlocked(blockerRoute);
		coordinator.RegisterBlocked(tail);
		return tail;
	}

	private FindRoute PrepareIdleClearingChain()
	{
		FindRoute tail = CreateWorker("Idle Clearing Tail", YieldCell);
		SetRoutePath(requestedRoute, Source, BlockedCell, YieldCell, new int3(18, 0, 11));
		SetRoutePath(tail, YieldCell, BlockedCell, Source);
		BlockStaticCell(new int3(19, 0, 11));
		BlockStaticCell(new int3(19, 0, 13));
		coordinator.RegisterBlocked(tail);
		return tail;
	}

	private bool StartClearingPlan() => (bool)InvokeResult(typeof(TrafficCoordinator), coordinator,
		"TryStartClearingPlan", requestedRoute, blockerRoute, requestedRoute);

	private void CompleteClearingSteps()
	{
		for (int step = 0; step < 10 && ActiveJobs.Count > 0; ++step)
			CompleteCurrentClearingStep();
		Assert.That(ActiveJobs, Is.Empty);
	}

	private void CompleteCurrentClearingStep()
	{
		PathRequest request = GetOnlyPathRequest();
		FindRoute route = request.target;
		int3 target = request.endPosition;
		for (int tick = 0; tick < 5 && ActiveJobs.Count > 0; ++tick)
			Invoke(typeof(PathFindingService), pathFinding, "Update");
		Assert.That(ActiveJobs, Is.Empty, "A planned adjacent step must finish its path search.");
		Assert.That(route.TryGetTrafficToCell(out int3 toCell), Is.True);
		Assert.That(toCell, Is.EqualTo(target));
		Assert.That(route.TryGetFutureToCell(out _), Is.False, "Clearing paths must not detour outside the planned step.");
		Assert.That(coordinator.CanReserveClearingCell(requestedRoute, BlockedCell), Is.False);
		Assert.That(InvokeResult(typeof(FindRoute), route, "TryReserveNextTile").ToString(), Is.EqualTo("Success"));
		MoveWorker(route, target);
		Invoke(typeof(FindRoute), route, "OnArrived");
	}

	// Exercise real path search/reservation/arrival callbacks; advance occupancy explicitly in EditMode.
	private void MoveWorker(FindRoute route, in int3 target)
	{
		int3 from = route.TrafficFromCell;
		FootprintCell footprint = new() { flags = GridFlags.DynamicObstacle, occupancyCategory = GridOccupancyCategory.Worker };
		Assert.That(grid.TryReserve(route, target), Is.True);
		grid.GetCell(from).Remove(footprint, route.gameObject);
		grid.GetCell(target).Set(footprint, route.gameObject);
		route.Worker.OnPositionSet(target, FacingDirection.West);
		Assert.That(grid.TryUnreserve(route, from), Is.True);
	}

	private void AssertClearingOwnershipCleared()
	{
		Assert.That((System.Collections.IEnumerable)GetField(typeof(TrafficCoordinator), coordinator, "clearingPlans"), Is.Empty);
		Assert.That((System.Collections.IEnumerable)GetField(typeof(TrafficCoordinator), coordinator, "clearingPlansByRoute"), Is.Empty);
		Assert.That((System.Collections.IEnumerable)GetField(typeof(TrafficCoordinator), coordinator, "clearingPlansByCell"), Is.Empty);
		Assert.That((System.Collections.IEnumerable)GetField(typeof(TrafficCoordinator), coordinator, "reservedYieldCells"), Is.Empty);
	}

	private void AssertYieldStarted()
	{
		Assert.That(coordinator.IsYieldHeld(blockerRoute), Is.True);
		Assert.That(coordinator.IsWaitingForTraffic(requestedRoute), Is.True);
		Assert.That(blockerRoute.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		Assert.That(grid.GetReservedFindRoute(Source), Is.SameAs(requestedRoute));
		Assert.That(grid.GetReservedFindRoute(BlockedCell), Is.SameAs(blockerRoute));
		PathRequest request = GetOnlyPathRequest();
		Assert.That(request.target, Is.SameAs(blockerRoute));
		Assert.That(request.endPosition, Is.EqualTo(YieldCell));
		Assert.That(request.IsSubPathRequest, Is.False);
	}

	private FindRoute CreateWorker(string name, in int3 position)
	{
		GameObject workerObject = CreateObject(name);
		FindRoute route = workerObject.AddComponent<FindRoute>();
		HumanWorker worker = workerObject.AddComponent<HumanWorker>();
		SetField(typeof(AIWorker), worker, "routeFinder", route);
		SetField(typeof(FindRoute), route, "worker", worker);
		worker.OnPositionSet(position, FacingDirection.West);
		worker.SetWorkerAction(WorkerStatusAction.Idle);
		FootprintCell footprint = new()
		{
			flags = GridFlags.DynamicObstacle,
			occupancyCategory = GridOccupancyCategory.Worker,
		};
		grid.GetCell(position).Set(footprint, workerObject);
		Assert.That(grid.TryReserve(route, position), Is.True);
		return route;
	}

	private void SetRequestedPath(params int3[] points)
	{
		SetRoutePath(requestedRoute, points);
	}

	private static void SetRoutePath(FindRoute route, params int3[] points)
	{
		LinkedList<PathNode> nodes = new();
		foreach (int3 point in points)
			nodes.AddLast(PathResultBuffer.GetNewNode(point, FacingDirection.West));
		PathResultBuffer path = new(nodes, route);
		path.MoveToNextNode();
		SetField(typeof(FindRoute), route, "pathResultBuffer", path);
		SetField(typeof(FindRoute), route, "hasCurrentGoal", true);
		SetField(typeof(FindRoute), route, "currentGoalPos", points[points.Length - 1]);
		SetField(typeof(FindRoute), route, "movementState", FindRoute.MovementState.Moving);
	}

	private void BlockYieldCells()
	{
		foreach (int3 point in new[] { YieldCell, Destination, new int3(19, 0, 13) })
		{
			FootprintCell footprint = new() { flags = GridFlags.BlockMovement };
			grid.GetCell(point).Set(footprint, CreateObject($"Yield Obstacle {point}"));
		}
	}

	private void BlockStaticCell(in int3 point)
	{
		FootprintCell footprint = new() { flags = GridFlags.BlockMovement };
		grid.GetCell(point).Set(footprint, CreateObject($"Clearing Obstacle {point}"));
	}

	private void ResolveRequest()
	{
		coordinator.RegisterBlocked(requestedRoute);
		Invoke(typeof(TrafficCoordinator), coordinator, "ResolveRequest", requestedRoute);
	}

	private List<PathSearchJob> ActiveJobs =>
		(List<PathSearchJob>)GetField(typeof(PathFindingService), pathFinding, "activeJobs");

	private PathRequest GetOnlyPathRequest()
	{
		Assert.That(ActiveJobs, Has.Count.EqualTo(1));
		return (PathRequest)GetField(typeof(PathSearchJob), ActiveJobs[0], "request");
	}

	private void AssertClearingPlanEndsOutsideProtectedCells()
	{
		object plan = null;
		foreach (object candidate in (System.Collections.IEnumerable)GetField(typeof(TrafficCoordinator), coordinator, "clearingPlans"))
		{
			Assert.That(plan, Is.Null, "Only one clearing plan should be active in this fixture.");
			plan = candidate;
		}
		Assert.That(plan, Is.Not.Null);

		object definition = GetAnyField(plan.GetType(), plan, "Definition");
		var participants = (List<FindRoute>)GetAnyField(definition.GetType(), definition, "Participants");
		var protectedCells = (HashSet<int3>)GetAnyField(definition.GetType(), definition, "ProtectedCells");
		var finalPositions = new Dictionary<FindRoute, int3>();
		for (int i = 0; i < participants.Count; ++i)
			finalPositions[participants[i]] = participants[i].TrafficFromCell;

		int moveCount = 0;
		foreach (object move in (System.Collections.IEnumerable)GetAnyField(definition.GetType(), definition, "Moves"))
		{
			FindRoute route = (FindRoute)GetAnyField(move.GetType(), move, "Route");
			int3 toCell = (int3)GetAnyField(move.GetType(), move, "ToCell");
			finalPositions[route] = toCell;
			++moveCount;
		}

		Assert.That(moveCount, Is.GreaterThanOrEqualTo(3));
		foreach (var pair in finalPositions)
			Assert.That(protectedCells.Contains(pair.Value), Is.False, $"{pair.Key.Worker.Name} must finish outside the passing route.");
	}

	private GameObject CreateObject(string name)
	{
		GameObject obj = new(name);
		obj.SetActive(false);
		createdObjects.Add(obj);
		return obj;
	}

	private static FieldInfo InstanceField => typeof(GameContext).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
	private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
	private static object GetField(Type type, object target, string name) => Field(type, name).GetValue(target);
	private static object GetAnyField(Type type, object target, string name) =>
		type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
	private static void SetField(Type type, object target, string name, object value) => Field(type, name).SetValue(target, value);
	private static void Invoke(Type type, object target, string name, params object[] args) =>
		type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
	private static object InvokeResult(Type type, object target, string name, params object[] args) =>
		type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
