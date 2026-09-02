using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class RobotHumanCollisionEditModeTests
{
	private static readonly int3 RobotCell = new(10, 0, 10);
	private static readonly int3 HumanCell = new(11, 0, 10);
	private static readonly int3 RelocatedHumanCell = new(12, 0, 10);

	private readonly List<GameObject> createdObjects = new();
	private GridFootprint workerFootprint;
	private PlaceableDefinition workerDefinition;
	private GridService grid;
	private RobotWorker robot;
	private HumanWorker human;
	private FindRoute robotRoute;
	private FindRoute humanRoute;
	private WorkplaceIncidentService incidents;
	private RobotHumanCollisionService collisionService;

	[SetUp]
	public void SetUp()
	{
		GameObject gridObject = CreateGameObject("Robot Collision Test Grid");
		grid = gridObject.AddComponent<GridService>();
		grid.BuildDefaultMap();

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

		GameObject robotObject = CreateGameObject("Robot Collision Test Robot");
		robotRoute = robotObject.AddComponent<FindRoute>();
		robot = robotObject.AddComponent<RobotWorker>();
		robot.SetRobotIdentity(RobotType.Transfer);
		SetPrivateField(typeof(AIWorker), robot, "workerID", (uint)1);
		SetPrivateField(typeof(AIWorker), robot, "routeFinder", robotRoute);
		PlaceWorker(robot, robotRoute, RobotCell);

		GameObject humanObject = CreateGameObject("Robot Collision Test Human");
		humanRoute = humanObject.AddComponent<FindRoute>();
		human = humanObject.AddComponent<HumanWorker>();
		human.SetHumanIdentity(HumanType.FullTime);
		SetPrivateField(typeof(AIWorker), human, "workerID", (uint)2);
		SetPrivateField(typeof(AIWorker), human, "routeFinder", humanRoute);
		PlaceWorker(human, humanRoute, HumanCell);

		GameObject incidentObject = CreateGameObject("Robot Collision Test Incidents");
		incidents = incidentObject.AddComponent<WorkplaceIncidentService>();
		GameObject collisionObject = CreateGameObject("Robot Collision Test Service");
		collisionService = collisionObject.AddComponent<RobotHumanCollisionService>();
		collisionService.Initialize(grid, incidents, null, null);
	}

	[TearDown]
	public void TearDown()
	{
		for (int i = createdObjects.Count - 1; i >= 0; --i)
		{
			if (createdObjects[i] != null)
				Object.DestroyImmediate(createdObjects[i]);
		}

		if (workerDefinition != null)
			Object.DestroyImmediate(workerDefinition);
		if (workerFootprint != null)
			Object.DestroyImmediate(workerFootprint);
	}

	[Test]
	public void TryResolve_OpenSafeCell_RelocatesKnocksOutAndRecordsCollision()
	{
		float healthBefore = robot.Health;
		float wearBefore = robot.Wear;
		WorkerIncidentCase publishedIncident = null;
		incidents.OnIncidentCreated += incident => publishedIncident = incident;

		RobotHumanCollisionResult result = collisionService.TryResolve(robot, human, HumanCell);

		Assert.That(result, Is.EqualTo(RobotHumanCollisionResult.HumanRelocated));
		Assert.That(human.OperationalState, Is.EqualTo(WorkerOperationalState.Knockout));
		Assert.That(human.GridPosition, Is.EqualTo(RelocatedHumanCell));
		Assert.That(grid.GetReservedFindRoute(HumanCell), Is.Null);
		Assert.That(grid.GetReservedFindRoute(RelocatedHumanCell), Is.SameAs(humanRoute));
		Assert.That(robot.Health, Is.LessThan(healthBefore));
		Assert.That(robot.Wear, Is.GreaterThan(wearBefore));
		Assert.That(incidents.Incidents, Has.Count.EqualTo(1));
		Assert.That(publishedIncident, Is.Not.Null);
		AssertCollisionContext(publishedIncident);
	}

	[Test]
	public void TryResolve_PlayerOverrideRobot_StillUsesHumanCollisionRules()
	{
		SetPrivateField(
			typeof(AIWorker),
			robot,
			"controlMode",
			WorkerControlMode.PlayerOverride);

		RobotHumanCollisionResult result = collisionService.TryResolve(robot, human, HumanCell);

		Assert.That(result, Is.EqualTo(RobotHumanCollisionResult.HumanRelocated));
		Assert.That(human.OperationalState, Is.EqualTo(WorkerOperationalState.Knockout));
		Assert.That(incidents.Incidents, Has.Count.EqualTo(1));
	}

	[Test]
	public void TrafficCoordinator_PlayerOverrideRobot_StillEntersHumanCollisionGate()
	{
		SetPrivateField(
			typeof(AIWorker),
			robot,
			"controlMode",
			WorkerControlMode.PlayerOverride);
		SetPrivateField(
			typeof(AIWorker),
			human,
			"operationalState",
			WorkerOperationalState.Knockout);

		GameObject contextObject = CreateGameObject("Player Override Collision Context");
		contextObject.SetActive(false);
		GameContext context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "gridService", grid);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		TrafficCoordinator coordinator = CreateGameObject("Player Override Traffic Coordinator")
			.AddComponent<TrafficCoordinator>();
		MethodInfo resolveCollision = typeof(TrafficCoordinator).GetMethod(
			"TryResolveRobotHumanCollision",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(resolveCollision, Is.Not.Null);

		bool handled = (bool)resolveCollision.Invoke(
			coordinator,
			new object[] { robotRoute, humanRoute, HumanCell });

		Assert.That(handled, Is.True);
		Assert.That(coordinator.IsWaitingForTraffic(robotRoute), Is.True);
	}

	[Test]
	public void TryResolve_NoSafeCell_LeavesCasualtyReservationAndDoesNotRepeatDamage()
	{
		BlockCell(new int3(12, 0, 10));
		BlockCell(new int3(11, 0, 11));
		BlockCell(new int3(11, 0, 9));

		RobotHumanCollisionResult firstResult = collisionService.TryResolve(robot, human, HumanCell);
		float healthAfterFirst = robot.Health;
		float wearAfterFirst = robot.Wear;
		RobotHumanCollisionResult secondResult = collisionService.TryResolve(robot, human, HumanCell);

		Assert.That(firstResult, Is.EqualTo(RobotHumanCollisionResult.BlockedByCasualty));
		Assert.That(secondResult, Is.EqualTo(RobotHumanCollisionResult.BlockedByCasualty));
		Assert.That(human.OperationalState, Is.EqualTo(WorkerOperationalState.Knockout));
		Assert.That(human.GridPosition, Is.EqualTo(HumanCell));
		Assert.That(grid.GetReservedFindRoute(HumanCell), Is.SameAs(humanRoute));
		Assert.That(robot.Health, Is.EqualTo(healthAfterFirst));
		Assert.That(robot.Wear, Is.EqualTo(wearAfterFirst));
		Assert.That(incidents.Incidents, Has.Count.EqualTo(1));
	}

	[Test]
	public void IncidentSave_RoundTripsRobotCollisionContext()
	{
		Assert.That(
			collisionService.TryResolve(robot, human, HumanCell),
			Is.EqualTo(RobotHumanCollisionResult.HumanRelocated));
		WorkplaceIncidentSaveData saveData = incidents.CaptureState();

		GameObject restoredObject = CreateGameObject("Restored Robot Collision Incidents");
		WorkplaceIncidentService restored = restoredObject.AddComponent<WorkplaceIncidentService>();
		restored.RestoreState(saveData);

		Assert.That(restored.Incidents, Has.Count.EqualTo(1));
		AssertCollisionContext(restored.Incidents[0]);
	}

	[Test]
	public void IncidentRestore_ResolvedIncidentIsHistoryOnly()
	{
		WorkplaceIncidentSaveData saveData = CreateIncidentSaveData(WorkerIncidentCaseState.Resolved);
		GameObject restoredObject = CreateGameObject("Resolved Incident Restore");
		WorkplaceIncidentService restored = restoredObject.AddComponent<WorkplaceIncidentService>();

		restored.RestoreState(saveData);

		Assert.That(restored.Incidents, Has.Count.EqualTo(1));
		Assert.That(GetCurrentIncidentCount(restored), Is.Zero);
	}

	[Test]
	public void ResolveIncident_RemovesWorkerFromCurrentIncidentIndex()
	{
		WorkplaceIncidentSaveData saveData = CreateIncidentSaveData(WorkerIncidentCaseState.HandedOver);
		GameObject restoredObject = CreateGameObject("Incident Resolution Index");
		WorkplaceIncidentService restored = restoredObject.AddComponent<WorkplaceIncidentService>();
		restored.RestoreState(saveData);
		Assert.That(GetCurrentIncidentCount(restored), Is.EqualTo(1));

		MethodInfo resolveIncident = typeof(WorkplaceIncidentService).GetMethod(
			"ResolveIncident",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(resolveIncident, Is.Not.Null);
		resolveIncident.Invoke(restored, new object[] { restored.Incidents[0] });

		Assert.That(restored.Incidents[0].State, Is.EqualTo(WorkerIncidentCaseState.Resolved));
		Assert.That(GetCurrentIncidentCount(restored), Is.Zero);
	}

	[Test]
	public void TryEvacuateWorker_IncapacitatedHuman_RemovesWorkerFromGridAndManager()
	{
		GameObject managerObject = CreateGameObject("Medical Evacuation Worker Manager");
		WorkerManager workerManager = managerObject.AddComponent<WorkerManager>();
		MethodInfo workerManagerAwake = typeof(WorkerManager).GetMethod(
			"Awake",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(workerManagerAwake, Is.Not.Null);
		workerManagerAwake.Invoke(workerManager, null);

		GameObject contextObject = CreateGameObject("Medical Evacuation Context");
		contextObject.SetActive(false);
		GameContext context = contextObject.AddComponent<GameContext>();
		TrafficCoordinator trafficCoordinator = contextObject.AddComponent<TrafficCoordinator>();
		RestFacilityService restFacilityService = contextObject.AddComponent<RestFacilityService>();
		SetPrivateField(typeof(GameContext), context, "gridService", grid);
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "trafficCoordinator", trafficCoordinator);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		workerManager.RegisterWorker(human);
		SetPrivateField(
			typeof(AIWorker),
			human,
			"operationalState",
			WorkerOperationalState.Knockout);

		bool removed = workerManager.TryEvacuateWorker(human);

		Assert.That(removed, Is.True);
		Assert.That(workerManager.Workers, Is.Empty);
		Assert.That(grid.IsPlacedObject(human.gameObject), Is.False);
		Assert.That(grid.GetCell(HumanCell).OccupancyWorker, Is.Null);
		Assert.That(grid.GetReservedFindRoute(HumanCell), Is.Null);
	}

	private void PlaceWorker(AIWorker worker, FindRoute route, in int3 position)
	{
		SetPrivateField(typeof(FindRoute), route, "worker", worker);
		worker.OnPositionSet(position, FacingDirection.North);
		PlacementContext context = new(
			position,
			FacingDirection.North,
			workerDefinition,
			PlacementEvent.WorkerSpawn,
			worker.gameObject);
		Dictionary<GameObject, PlacementContext> placedObjects =
			(Dictionary<GameObject, PlacementContext>)GetPrivateField(typeof(GridService), grid, "placedObjects");
		placedObjects.Add(worker.gameObject, context);
		grid.GetCell(position).Set(workerFootprint.Get(0, 0), worker.gameObject);
		Assert.That(grid.TryReserve(route, position), Is.True);
	}

	private void BlockCell(in int3 position)
	{
		GameObject obstacle = CreateGameObject($"Collision Obstacle {position}");
		FootprintCell footprint = new()
		{
			flags = GridFlags.BlockMovement,
			occupancyCategory = GridOccupancyCategory.Other,
		};
		grid.GetCell(position).Set(footprint, obstacle);
	}

	private void AssertCollisionContext(WorkerIncidentCase incident)
	{
		Assert.That(incident.Cause, Is.EqualTo(WorkerIncidentCause.RobotCollision));
		Assert.That(incident.WorkerId, Is.EqualTo(human.WorkerID));
		Assert.That(incident.InstigatorWorkerId, Is.EqualTo(robot.WorkerID));
		Assert.That(incident.VictimWorkerId, Is.EqualTo(human.WorkerID));
		Assert.That(new int3(incident.PositionX, incident.PositionY, incident.PositionZ), Is.EqualTo(HumanCell));
	}

	private WorkplaceIncidentSaveData CreateIncidentSaveData(WorkerIncidentCaseState state)
	{
		WorkplaceIncidentSaveData saveData = new()
		{
			NextIncidentId = 2,
			IsAccidentFree = false,
		};
		saveData.Incidents.Add(new WorkerIncidentCase
		{
			IncidentId = 1,
			WorkerId = human.WorkerID,
			WorkerKind = WorkerKind.Human,
			OperationalState = WorkerOperationalState.Knockout,
			ResponseKind = WorkerIncidentResponseKind.Medical,
			State = state,
		});
		return saveData;
	}

	private static int GetCurrentIncidentCount(WorkplaceIncidentService service)
	{
		object currentIncidents = GetPrivateField(
			typeof(WorkplaceIncidentService),
			service,
			"currentIncidentByWorker");
		return ((System.Collections.IDictionary)currentIncidents).Count;
	}

	private GameObject CreateGameObject(string objectName)
	{
		GameObject gameObject = new(objectName);
		createdObjects.Add(gameObject);
		return gameObject;
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

	private static void SetPrivateStaticField(System.Type ownerType, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(null, value);
	}
}
