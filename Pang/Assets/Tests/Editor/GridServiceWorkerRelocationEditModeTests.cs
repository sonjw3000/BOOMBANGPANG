using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class GridServiceWorkerRelocationEditModeTests
{
	private static readonly int3 Source = new(10, 0, 10);
	private static readonly int3 Destination = new(11, 0, 10);

	private GameObject gridObject;
	private GameObject workerObject;
	private GameObject blockerObject;
	private GridFootprint workerFootprint;
	private PlaceableDefinition workerDefinition;
	private GridService grid;
	private HumanWorker worker;
	private FindRoute route;

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
		if (blockerObject != null)
			Object.DestroyImmediate(blockerObject);
		if (workerObject != null)
			Object.DestroyImmediate(workerObject);
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
				grid.GetCell(Destination).OccupancyObjectOnGrid == workerObject;
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
		Assert.That(grid.GetCell(Destination).OccupancyObjectOnGrid, Is.SameAs(workerObject));
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
		Assert.That(grid.TryReserve(blocker, Source), Is.True);

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
		Assert.That(grid.GetCell(Source).OccupancyObjectOnGrid, Is.SameAs(workerObject));
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
		Assert.That(grid.GetCell(Source).OccupancyObjectOnGrid, Is.SameAs(workerObject));
		Assert.That(grid.GetCell(Destination).OccupancyObjectOnGrid, Is.Null);
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
}
