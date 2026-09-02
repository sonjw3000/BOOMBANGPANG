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
		LinkedList<PathNode> nodes = new();
		foreach (int3 point in points)
			nodes.AddLast(PathResultBuffer.GetNewNode(point, FacingDirection.West));
		PathResultBuffer path = new(nodes, requestedRoute);
		path.MoveToNextNode();
		SetField(typeof(FindRoute), requestedRoute, "pathResultBuffer", path);
		SetField(typeof(FindRoute), requestedRoute, "hasCurrentGoal", true);
		SetField(typeof(FindRoute), requestedRoute, "currentGoalPos", points[points.Length - 1]);
		SetField(typeof(FindRoute), requestedRoute, "movementState", FindRoute.MovementState.Moving);
	}

	private void BlockYieldCells()
	{
		foreach (int3 point in new[] { YieldCell, Destination, new int3(19, 0, 13) })
		{
			FootprintCell footprint = new() { flags = GridFlags.BlockMovement };
			grid.GetCell(point).Set(footprint, CreateObject($"Yield Obstacle {point}"));
		}
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
	private static void SetField(Type type, object target, string name, object value) => Field(type, name).SetValue(target, value);
	private static void Invoke(Type type, object target, string name, params object[] args) =>
		type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
