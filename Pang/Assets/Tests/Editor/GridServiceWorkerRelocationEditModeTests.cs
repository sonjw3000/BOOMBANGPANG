using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

public sealed class GridServiceWorkerRelocationEditModeTests
{
	private static readonly int3 Source = new(10, 0, 10);
	private static readonly int3 Destination = new(11, 0, 10);
	private static readonly int3 Exit = new(12, 0, 10);

	private GameObject gridObject;
	private GameObject workerObject;
	private GameObject blockerObject;
	private GameObject secondaryWorkerObject;
	private GameObject facilityObject;
	private GameObject contextObject;
	private GridFootprint workerFootprint;
	private GridFootprint overrideFootprint;
	private PlaceableDefinition workerDefinition;
	private PlaceableDefinition overrideDefinition;
	private GridService grid;
	private HumanWorker worker;
	private FindRoute route;
	private HumanWorker secondaryWorker;
	private FindRoute secondaryRoute;
	private GameContext previousGameContext;

	[SetUp]
	public void SetUp()
	{
		gridObject = new GameObject("Worker Relocation Test Grid");
		grid = gridObject.AddComponent<GridService>();
		grid.BuildDefaultMap();

		workerObject = new GameObject("Worker Relocation Test Human");
		route = workerObject.AddComponent<FindRoute>();
		worker = workerObject.AddComponent<HumanWorker>();
		SetPrivateField(typeof(AIWorker), worker, "routeFinder", route);
		SetPrivateField(typeof(FindRoute), route, "worker", worker);

		workerFootprint = ScriptableObject.CreateInstance<GridFootprint>();
		workerFootprint.width = 1;
		workerFootprint.height = 1;
		SetPrivateField(
			typeof(GridFootprint),
			workerFootprint,
			"footprintCells",
			new[]
			{
				new FootprintCell
				{
					flags = GridFlags.DynamicObstacle,
					occupancyCategory = GridOccupancyCategory.Worker,
				},
			});

		workerDefinition = ScriptableObject.CreateInstance<PlaceableDefinition>();
		workerDefinition.gridFootprint = workerFootprint;
		worker.OnPositionSet(Source, FacingDirection.North);

		PlacementContext context = new(
			Source,
			FacingDirection.North,
			workerDefinition,
			PlacementEvent.WorkerSpawn,
			workerObject);
		Dictionary<GameObject, PlacementContext> placedObjects =
			(Dictionary<GameObject, PlacementContext>)GetPrivateField(typeof(GridService), grid, "placedObjects");
		placedObjects.Add(workerObject, context);
		grid.GetCell(Source).Set(workerFootprint.Get(0, 0), workerObject);
		Assert.That(grid.TryReserve(route, Source), Is.True, "test worker must own its source reservation");
	}

	[TearDown]
	public void TearDown()
	{
		if (contextObject != null)
		{
			SetPrivateStaticField(typeof(GameContext), "instance", previousGameContext);
			Object.DestroyImmediate(contextObject);
		}
		if (facilityObject != null)
			Object.DestroyImmediate(facilityObject);
		if (secondaryWorkerObject != null)
			Object.DestroyImmediate(secondaryWorkerObject);
		if (blockerObject != null)
			Object.DestroyImmediate(blockerObject);
		if (workerObject != null)
			Object.DestroyImmediate(workerObject);
		if (overrideDefinition != null)
			Object.DestroyImmediate(overrideDefinition);
		if (overrideFootprint != null)
			Object.DestroyImmediate(overrideFootprint);
		if (workerDefinition != null)
			Object.DestroyImmediate(workerDefinition);
		if (workerFootprint != null)
			Object.DestroyImmediate(workerFootprint);
		if (gridObject != null)
			Object.DestroyImmediate(gridObject);
	}

	[Test]
	public void TryRelocateWorker_Success_CommitsDestinationBeforeReleasingSource()
	{
		bool sourceReleasedAfterCommit = false;
		grid.GetCell(Source).OnGridUnReserved += _ =>
		{
			sourceReleasedAfterCommit =
				worker.GridPosition.Equals(Destination) &&
				grid.GetReservedFindRoute(Destination) == route &&
				grid.GetCell(Destination).OccupancyWorker == worker;
		};

		bool relocated = grid.TryRelocateWorker(
			worker,
			Destination,
			FacingDirection.East,
			out WorkerRelocationFailureReason reason);

		Assert.That(relocated, Is.True);
		Assert.That(reason, Is.EqualTo(WorkerRelocationFailureReason.None));
		Assert.That(sourceReleasedAfterCommit, Is.True, "source release must observe a fully committed destination");
		Assert.That(grid.GetReservedFindRoute(Source), Is.Null);
		Assert.That(grid.GetReservedFindRoute(Destination), Is.SameAs(route));
		Assert.That(grid.GetCell(Source).OccupancyObjectOnGrid, Is.Null);
		Assert.That(grid.GetCell(Source).OccupancyWorker, Is.Null);
		Assert.That(grid.GetCell(Destination).OccupancyObjectOnGrid, Is.Null);
		Assert.That(grid.GetCell(Destination).OccupancyWorker, Is.SameAs(worker));
		Assert.That(worker.GridPosition, Is.EqualTo(Destination));
		Assert.That(worker.Direction, Is.EqualTo(FacingDirection.East));
	}

	[Test]
	public void TryRelocateWorker_ReservedDestination_PreservesSourceState()
	{
		FindRoute blocker = CreateBlockerRoute();
		Assert.That(grid.TryReserve(blocker, Destination), Is.True);

		bool relocated = grid.TryRelocateWorker(
			worker,
			Destination,
			FacingDirection.East,
			out WorkerRelocationFailureReason reason);

		Assert.That(relocated, Is.False);
		Assert.That(reason, Is.EqualTo(WorkerRelocationFailureReason.DestinationReserved));
		AssertSourceStatePreserved();
		Assert.That(grid.GetReservedFindRoute(Destination), Is.SameAs(blocker));
	}

	[Test]
	public void TryRelocateWorker_SourceReservationMismatch_DoesNotClaimDestination()
	{
		FindRoute blocker = CreateBlockerRoute();
		Assert.That(grid.TryUnreserve(route, Source), Is.True);
		grid.GetCell(Source).Remove(workerFootprint.Get(0, 0), workerObject);
		Assert.That(grid.TryReserve(blocker, Source), Is.True);
		grid.GetCell(Source).Set(workerFootprint.Get(0, 0), workerObject);

		bool relocated = grid.TryRelocateWorker(
			worker,
			Destination,
			FacingDirection.East,
			out WorkerRelocationFailureReason reason);

		Assert.That(relocated, Is.False);
		Assert.That(reason, Is.EqualTo(WorkerRelocationFailureReason.SourceReservationMismatch));
		Assert.That(grid.GetReservedFindRoute(Source), Is.SameAs(blocker));
		Assert.That(grid.GetReservedFindRoute(Destination), Is.Null);
		Assert.That(worker.GridPosition, Is.EqualTo(Source));
		Assert.That(grid.GetCell(Source).OccupancyObjectOnGrid, Is.Null);
		Assert.That(grid.GetCell(Source).OccupancyWorker, Is.SameAs(worker));
	}

	[Test]
	public void TryMove_ThroughInteractionPoint_PreservesFacilityOccupancyBeforeDuringAndAfter()
	{
		FootprintCell interactionCell = PlaceInteractionOccupant(Destination);
		GridCell interactionGridCell = grid.GetCell(Destination);

		Assert.That(grid.TryReserve(route, Destination), Is.True);
		Assert.That(grid.TryMove(route, Source, Destination), Is.EqualTo(PlacementResult.Success));
		worker.SetPosition(Destination);
		Assert.That(grid.TryUnreserve(route, Source), Is.True);

		Assert.That(interactionGridCell.OccupancyObjectOnGrid, Is.SameAs(facilityObject));
		Assert.That(interactionGridCell.OccupancyWorker, Is.SameAs(worker));
		Assert.That(interactionGridCell.OccupancyCategory, Is.EqualTo(GridOccupancyCategory.Other));
		Assert.That(interactionGridCell.ObjectsOnGrid, Does.Contain(facilityObject));
		Assert.That(interactionGridCell.ObjectsOnGrid, Does.Contain(workerObject));

		Assert.That(grid.TryReserve(route, Exit), Is.True);
		Assert.That(grid.TryMove(route, Destination, Exit), Is.EqualTo(PlacementResult.Success));
		worker.SetPosition(Exit);
		Assert.That(grid.TryUnreserve(route, Destination), Is.True);

		Assert.That(interactionGridCell.OccupancyObjectOnGrid, Is.SameAs(facilityObject));
		Assert.That(interactionGridCell.OccupancyWorker, Is.Null);
		Assert.That(interactionGridCell.OccupancyCategory, Is.EqualTo(GridOccupancyCategory.Other));
		Assert.That(interactionGridCell.ObjectsOnGrid, Does.Contain(facilityObject));
		Assert.That(interactionGridCell.ObjectsOnGrid, Has.No.Member(workerObject));
		Assert.That(interactionGridCell.Flags.HasFlag(GridFlags.Interaction), Is.True);
		Assert.That(interactionGridCell.Flags.HasFlag(GridFlags.DynamicObstacle), Is.False);
		Assert.That(interactionCell.flags.HasFlag(GridFlags.BlockMovement), Is.True, "test footprint must prove Interaction strips only its movement flag");
	}

	[Test]
	public void FindRoute_NextCellOccupiedByWorkerWithoutReservation_IsGridBlocked()
	{
		CreateSecondaryWorker(Destination, reserveCell: false);

		Assert.That(grid.GetReservedFindRoute(Destination), Is.Null);
		Assert.That(grid.GetBlockingFindRoute(Destination), Is.SameAs(secondaryRoute));
		Assert.That(InvokeTryReserveNextTile(Destination), Is.EqualTo("GridBlocked"));
		Assert.That(grid.GetReservedFindRoute(Destination), Is.Null);
	}

	[Test]
	public void FindRoute_NextCellBlockedByStaticObstacle_IsGridBlocked()
	{
		blockerObject = new GameObject("FindRoute Static Obstacle");
		FootprintCell obstacleCell = new()
		{
			flags = GridFlags.BlockMovement,
			occupancyCategory = GridOccupancyCategory.Other,
		};
		grid.GetCell(Destination).Set(obstacleCell, blockerObject);

		Assert.That(grid.IsBlocked(Destination), Is.True);
		Assert.That(InvokeTryReserveNextTile(Destination), Is.EqualTo("GridBlocked"));
		Assert.That(grid.GetReservedFindRoute(Destination), Is.Null);
	}

	[Test]
	public void PathSearch_StaticObstacle_IsExcludedFromCompletedPath()
	{
		EnsureGameContext();
		blockerObject = new GameObject("Path Search Static Obstacle");
		FootprintCell obstacleCell = new()
		{
			flags = GridFlags.BlockMovement,
			occupancyCategory = GridOccupancyCategory.Other,
		};
		grid.GetCell(Destination).Set(obstacleCell, blockerObject);

		PathResultBuffer.InitializePool(128);
		PathResultBuffer result = null;
		PathRequest request = new(
			Source,
			Exit,
			FacingDirection.East,
			path => result = path);
		PathSearchJob job = new();
		job.Setup(request, new SearchBuffer(grid.MapSize));

		bool completed = false;
		for (int i = 0; i < 100 && completed == false; ++i)
			completed = job.Execute(1024);

		Assert.That(completed, Is.True, "path search must complete within the bounded test budget");
		job.SetPath();
		try
		{
			Assert.That(result, Is.Not.Null);
			Assert.That(result.Path, Is.Not.Empty);
			foreach (PathNode node in result.Path)
				Assert.That(node.Position, Is.Not.EqualTo(Destination));
		}
		finally
		{
			result?.Clear();
		}
	}

	[Test]
	public void FacilitySelection_PrefersOccupancyWorkerThenFallsBackToStaticObject()
	{
		EnsureGameContext();
		facilityObject = new GameObject("Selection Static Facility");
		FootprintCell facilityCell = new()
		{
			flags = GridFlags.BlockMovement,
			occupancyCategory = GridOccupancyCategory.Other,
		};
		grid.GetCell(Source).Set(facilityCell, facilityObject);
		InteractionContext interaction = new();

		interaction.OnLeftClick(Source);
		Assert.That(interaction.SelectedObject, Is.SameAs(workerObject));

		Assert.That(grid.TryUnreserve(route, Source), Is.True);
		grid.GetCell(Source).Remove(workerFootprint.Get(0, 0), workerObject);
		interaction.OnLeftClick(Source);

		Assert.That(grid.GetCell(Source).OccupancyWorker, Is.Null);
		Assert.That(grid.GetCell(Source).OccupancyObjectOnGrid, Is.SameAs(facilityObject));
		Assert.That(interaction.SelectedObject, Is.SameAs(facilityObject));
	}

	[Test]
	public void WorkerOverride_OnFacilityInteractionPoint_TargetsOnlyWorker()
	{
		PlaceInteractionOccupant(Destination);
		CreateSecondaryWorker(Destination, reserveCell: true);

		overrideFootprint = ScriptableObject.CreateInstance<GridFootprint>();
		overrideFootprint.width = 1;
		overrideFootprint.height = 1;
		SetPrivateField(
			typeof(GridFootprint),
			overrideFootprint,
			"footprintCells",
			new[]
			{
				new FootprintCell
				{
					flags = GridFlags.BlockMovement,
					occupancyCategory = GridOccupancyCategory.Rocket,
					overrideTargets = GridOccupancyCategory.Worker,
				},
			});
		overrideDefinition = ScriptableObject.CreateInstance<PlaceableDefinition>();
		overrideDefinition.gridFootprint = overrideFootprint;
		PlacementContext context = new(Destination, FacingDirection.North, overrideDefinition, PlacementEvent.RocketLanding);
		List<int3> possible = new();
		List<int3> blocked = new();

		Assert.That(grid.OnCheckInstallable(context, possible, blocked), Is.True);
		List<GameObject> targets = new();
		grid.GetOverrideTargets(context, targets);

		Assert.That(targets, Has.Count.EqualTo(1));
		Assert.That(targets[0], Is.SameAs(secondaryWorkerObject));
		Assert.That(targets, Has.No.Member(facilityObject));
		Assert.That(grid.GetCell(Destination).OccupancyObjectOnGrid, Is.SameAs(facilityObject));
		Assert.That(grid.GetCell(Destination).OccupancyWorker, Is.SameAs(secondaryWorker));
	}

	[Test]
	public void PlannedPathCongestion_PreservesCostsAndReflectsRouteStateChanges()
	{
		FindRoute other = CreateBlockerRoute();
		HashSet<int3> activeCells = (HashSet<int3>)GetPrivateField(typeof(FindRoute), other, "plannedPathCells");
		activeCells.Add(Destination);
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, route, 7, 3), Is.Zero);
		Assert.That(grid.GetPlannedPathCongestionCost(new int3(-1, 0, -1), route, 7, 3), Is.Zero);
		Assert.That(grid.RegisterPlannedPath(route, Destination), Is.True);
		Assert.That(grid.RegisterPlannedPath(other, Destination), Is.True);
		Assert.That(grid.RegisterPlannedPath(other, Destination), Is.False, "A route is counted only once per cell.");
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, route, 7, 3), Is.EqualTo(7));
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, null, 7, 3), Is.EqualTo(10));

		activeCells.Clear();
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, route, 7, 3), Is.EqualTo(3));
		activeCells.Add(Destination);
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, route, 7, 3), Is.EqualTo(7));
		Assert.That(grid.UnregisterPlannedPath(other, Destination), Is.True);
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, route, 7, 3), Is.Zero);
		Assert.That(grid.RegisterPlannedPath(other, Destination), Is.True);
		activeCells.Clear(); // Leave a stale registration when the Unity object is destroyed.
		Object.DestroyImmediate(blockerObject);
		Assert.That(grid.GetPlannedPathCongestionCost(Destination, route, 7, 3), Is.EqualTo(3),
			"A destroyed registered route retains the existing stale-route cost.");
		Assert.That(grid.GetCell(Destination).PlannedPathCount, Is.EqualTo(2), "Queries must not mutate registrations.");
	}

	[Test]
	public void PlannedPathCongestion_RepeatedQueriesDoNotAllocate()
	{
		FindRoute other = CreateBlockerRoute();
		HashSet<int3> activeCells = (HashSet<int3>)GetPrivateField(typeof(FindRoute), other, "plannedPathCells");
		activeCells.Add(Destination);
		grid.RegisterPlannedPath(route, Destination);
		grid.RegisterPlannedPath(other, Destination);
		for (int i = 0; i < 10; ++i) grid.GetPlannedPathCongestionCost(Destination, route, 7, 3);

		int total = 0;
		// Verify the allocation detector itself: the Mono per-thread byte counter can return constant zero.
		Assert.That(() => System.GC.KeepAlive(new byte[1024]), new AllocatingGCMemoryConstraint());
		Assert.That(() =>
		{
			for (int i = 0; i < 1000; ++i)
				total += grid.GetPlannedPathCongestionCost(Destination, route, 7, 3);
		}, Is.Not.AllocatingGCMemory(), "Repeated congestion queries must not box the HashSet enumerator.");
		Assert.That(total, Is.EqualTo(7000));
	}

	private FindRoute CreateBlockerRoute()
	{
		blockerObject = new GameObject("Worker Relocation Test Blocker");
		return blockerObject.AddComponent<FindRoute>();
	}

	private void AssertSourceStatePreserved()
	{
		Assert.That(grid.GetReservedFindRoute(Source), Is.SameAs(route));
		Assert.That(worker.GridPosition, Is.EqualTo(Source));
		Assert.That(grid.GetCell(Source).OccupancyObjectOnGrid, Is.Null);
		Assert.That(grid.GetCell(Source).OccupancyWorker, Is.SameAs(worker));
		Assert.That(grid.GetCell(Destination).OccupancyObjectOnGrid, Is.Null);
		Assert.That(grid.GetCell(Destination).OccupancyWorker, Is.Null);
	}

	private FootprintCell PlaceInteractionOccupant(in int3 position)
	{
		facilityObject = new GameObject("Rule Target Interaction Facility");
		FootprintCell interactionCell = new()
		{
			flags = GridFlags.Interaction | GridFlags.BlockMovement,
			interactionKind = InteractionKind.Work,
			occupancyCategory = GridOccupancyCategory.Other,
		};
		grid.GetCell(position).Set(interactionCell, facilityObject);
		return interactionCell;
	}

	private void CreateSecondaryWorker(in int3 position, bool reserveCell)
	{
		secondaryWorkerObject = new GameObject("Secondary Occupancy Worker");
		secondaryRoute = secondaryWorkerObject.AddComponent<FindRoute>();
		secondaryWorker = secondaryWorkerObject.AddComponent<HumanWorker>();
		SetPrivateField(typeof(AIWorker), secondaryWorker, "routeFinder", secondaryRoute);
		SetPrivateField(typeof(FindRoute), secondaryRoute, "worker", secondaryWorker);
		secondaryWorker.OnPositionSet(position, FacingDirection.North);
		grid.GetCell(position).Set(workerFootprint.Get(0, 0), secondaryWorkerObject);

		if (reserveCell)
			Assert.That(grid.TryReserve(secondaryRoute, position), Is.True);
	}

	private string InvokeTryReserveNextTile(in int3 position)
	{
		EnsureGameContext();
		PathResultBuffer.InitializePool(8);
		LinkedList<PathNode> path = new();
		path.AddLast(PathResultBuffer.GetNewNode(position, FacingDirection.East));
		PathResultBuffer pathBuffer = new(path, route);
		SetPrivateField(typeof(FindRoute), route, "pathResultBuffer", pathBuffer);

		try
		{
			MethodInfo method = typeof(FindRoute).GetMethod(
				"TryReserveNextTile",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);
			return method.Invoke(route, null)?.ToString();
		}
		finally
		{
			SetPrivateField(typeof(FindRoute), route, "pathResultBuffer", null);
			pathBuffer.Clear();
		}
	}

	private void EnsureGameContext()
	{
		if (contextObject != null)
			return;

		previousGameContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		contextObject = new GameObject("Worker Occupancy Test Context");
		contextObject.SetActive(false);
		GameContext context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "gridService", grid);
		SetPrivateStaticField(typeof(GameContext), "instance", context);
	}

	private static object GetPrivateField(System.Type ownerType, object target, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		return field.GetValue(target);
	}

	private static void SetPrivateField(System.Type ownerType, object target, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(target, value);
	}

	private static object GetPrivateStaticField(System.Type ownerType, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		return field.GetValue(null);
	}

	private static void SetPrivateStaticField(System.Type ownerType, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(null, value);
	}
}
