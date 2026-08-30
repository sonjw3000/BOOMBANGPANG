using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class RecoveryFacilityServiceEditModeTests
{
	private const uint OtherBuildingId = 1;
	private const uint PrimaryBuildingId = 2;

	private static readonly int3 WorkerPosition = new(10, 0, 10);

	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private GameContext context;
	private GridService gridService;
	private FacilityManager facilityManager;
	private RestFacilityService restFacilityService;
	private ChargingFacilityService chargingFacilityService;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		gridService = CreateComponent<GridService>("Recovery Facility Test Grid");
		gridService.BuildDefaultMap();
		facilityManager = CreateComponent<FacilityManager>("Recovery Facility Test Facility Manager");

		GameObject serviceObject = CreateGameObject("Recovery Facility Test Rest Service", active: false);
		restFacilityService = serviceObject.AddComponent<RestFacilityService>();
		GameObject chargingServiceObject = CreateGameObject("Recovery Facility Test Charging Service", active: false);
		chargingFacilityService = chargingServiceObject.AddComponent<ChargingFacilityService>();

		GameObject contextObject = CreateGameObject("Recovery Facility Test Context", active: false);
		context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "gridService", gridService);
		SetPrivateField(typeof(GameContext), context, "facilityManager", facilityManager);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
		SetPrivateField(typeof(GameContext), context, "chargingFacilityService", chargingFacilityService);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		serviceObject.SetActive(true);
		chargingServiceObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<RestFacility>),
			restFacilityService,
			"TryBindFacilityManager");
		InvokeNonPublic(
			typeof(FacilityService<ChargingFacility>),
			chargingFacilityService,
			"TryBindFacilityManager");
	}

	[TearDown]
	public void TearDown()
	{
		SetPrivateStaticField(typeof(GameContext), "instance", null);
		for (int i = createdObjects.Count - 1; i >= 0; --i)
		{
			if (createdObjects[i] != null)
				UnityEngine.Object.DestroyImmediate(createdObjects[i]);
		}

		createdObjects.Clear();
		SetPrivateStaticField(typeof(GameContext), "instance", previousContext);
	}

	[Test]
	public void TryReserveDestination_PrimaryBuildingHasAvailableFacility_PrefersItOverCloserOtherBuilding()
	{
		HumanWorker worker = CreateWorker("Primary Building Worker", PrimaryBuildingId, WorkerPosition);
		RestFacility closerOtherFacility = CreateFacility(
			"Closer Other Building Rest Facility",
			OtherBuildingId,
			new int3(11, 0, 10));
		RestFacility fartherPrimaryFacility = CreateFacility(
			"Farther Primary Building Rest Facility",
			PrimaryBuildingId,
			new int3(30, 0, 10));

		bool reserved = restFacilityService.TryReserveDestination(
			worker,
			out RestFacility selectedFacility,
			out int3 selectedPoint);

		Assert.That(reserved, Is.True);
		Assert.That(selectedFacility, Is.SameAs(fartherPrimaryFacility));
		Assert.That(selectedFacility, Is.Not.SameAs(closerOtherFacility));
		Assert.That(selectedPoint, Is.EqualTo(new int3(30, 0, 10)));
	}

	[Test]
	public void TryReserveDestination_PrimaryBuildingFacilityIsFull_DoesNotUseOtherBuilding()
	{
		HumanWorker worker = CreateWorker("Primary Building Worker", PrimaryBuildingId, WorkerPosition);
		HumanWorker occupyingWorker = CreateWorker(
			"Occupying Worker",
			PrimaryBuildingId,
			new int3(29, 0, 10));
		RestFacility fullPrimaryFacility = CreateFacility(
			"Full Primary Building Rest Facility",
			PrimaryBuildingId,
			new int3(30, 0, 10));
		RestFacility otherFacility = CreateFacility(
			"Other Building Rest Facility",
			OtherBuildingId,
			new int3(12, 0, 10));
		Assert.That(
			fullPrimaryFacility.TryReserveSlot(occupyingWorker, occupyingWorker.GridPosition, out _),
			Is.True,
			"The primary-building facility must be full before the fallback query.");

		bool reserved = restFacilityService.TryReserveDestination(
			worker,
			out RestFacility selectedFacility,
			out int3 selectedPoint);

		Assert.That(reserved, Is.False);
		Assert.That(selectedFacility, Is.Null);
		Assert.That(selectedPoint, Is.EqualTo(default(int3)));
		Assert.That(otherFacility.IsReservedBy(worker), Is.False);
	}

	[Test]
	public void TryReserveDestination_UnassignedOutdoorWorker_UsesClosestSharedRestFacility()
	{
		HumanWorker worker = CreateWorker("Outdoor Worker", 0, WorkerPosition);
		RestFacility fartherFacility = CreateFacility(
			"Farther Shared Rest Facility",
			PrimaryBuildingId,
			new int3(30, 0, 10));
		RestFacility closerFacility = CreateFacility(
			"Closer Shared Rest Facility",
			OtherBuildingId,
			new int3(12, 0, 10));

		bool reserved = restFacilityService.TryReserveDestination(
			worker,
			out RestFacility selectedFacility,
			out int3 selectedPoint);

		Assert.That(reserved, Is.True);
		Assert.That(selectedFacility, Is.SameAs(closerFacility));
		Assert.That(selectedFacility, Is.Not.SameAs(fartherFacility));
		Assert.That(selectedPoint, Is.EqualTo(new int3(12, 0, 10)));
	}

	[Test]
	public void TryReserveDestination_UnassignedOutdoorRobot_DoesNotUseSharedChargingFacility()
	{
		RobotWorker worker = CreateRobotWorker("Outdoor Robot", 0, WorkerPosition);
		ChargingFacility facility = CreateChargingFacility(
			"Shared Charging Facility",
			OtherBuildingId,
			new int3(12, 0, 10));

		bool reserved = chargingFacilityService.TryReserveDestination(
			worker,
			out ChargingFacility selectedFacility,
			out int3 selectedPoint);

		Assert.That(reserved, Is.False);
		Assert.That(selectedFacility, Is.Null);
		Assert.That(selectedPoint, Is.EqualTo(default(int3)));
		Assert.That(facility.IsReservedBy(worker), Is.False);
	}

	[Test]
	public void TryReserveDestination_ExistingReservation_RemainsStable()
	{
		HumanWorker worker = CreateWorker("Reserved Worker", PrimaryBuildingId, WorkerPosition);
		RestFacility originalFacility = CreateFacility(
			"Original Rest Facility",
			PrimaryBuildingId,
			new int3(25, 0, 10));
		Assert.That(
			restFacilityService.TryReserveDestination(worker, out RestFacility firstFacility, out int3 firstPoint),
			Is.True);
		Assert.That(firstFacility, Is.SameAs(originalFacility));

		CreateFacility(
			"New Closer Rest Facility",
			PrimaryBuildingId,
			new int3(11, 0, 10));

		bool reserved = restFacilityService.TryReserveDestination(
			worker,
			out RestFacility selectedFacility,
			out int3 selectedPoint);

		Assert.That(reserved, Is.True);
		Assert.That(selectedFacility, Is.SameAs(originalFacility));
		Assert.That(selectedPoint, Is.EqualTo(firstPoint));
	}

	private HumanWorker CreateWorker(string objectName, uint primaryBuildingId, in int3 position)
	{
		GameObject workerObject = CreateGameObject(objectName);
		workerObject.AddComponent<FindRoute>();
		HumanWorker worker = workerObject.AddComponent<HumanWorker>();
		worker.OnPositionSet(position, FacingDirection.North);
		worker.SetPrimaryBuildingId(primaryBuildingId);
		return worker;
	}

	private RobotWorker CreateRobotWorker(string objectName, uint primaryBuildingId, in int3 position)
	{
		GameObject workerObject = CreateGameObject(objectName);
		workerObject.AddComponent<FindRoute>();
		RobotWorker worker = workerObject.AddComponent<RobotWorker>();
		worker.OnPositionSet(position, FacingDirection.North);
		worker.SetPrimaryBuildingId(primaryBuildingId);
		return worker;
	}

	private RestFacility CreateFacility(string objectName, uint buildingId, in int3 interactionPoint)
	{
		RestFacility facility = CreateComponent<RestFacility>(objectName);
		facility.OnPositionSet(interactionPoint, FacingDirection.North);
		facility.AddInteractionPoint(InteractionKind.Rest, interactionPoint);
		facilityManager.RegisterFacility(buildingId, facility);
		Assert.That(
			facilityManager.TryGetBuildingId(facility, out uint registeredBuildingId),
			Is.True);
		Assert.That(registeredBuildingId, Is.EqualTo(buildingId));
		return facility;
	}

	private ChargingFacility CreateChargingFacility(string objectName, uint buildingId, in int3 interactionPoint)
	{
		ChargingFacility facility = CreateComponent<ChargingFacility>(objectName);
		facility.OnPositionSet(interactionPoint, FacingDirection.North);
		facility.AddInteractionPoint(InteractionKind.Charge, interactionPoint);
		facilityManager.RegisterFacility(buildingId, facility);
		Assert.That(
			facilityManager.TryGetBuildingId(facility, out uint registeredBuildingId),
			Is.True);
		Assert.That(registeredBuildingId, Is.EqualTo(buildingId));
		return facility;
	}

	private T CreateComponent<T>(string objectName) where T : Component
	{
		return CreateGameObject(objectName).AddComponent<T>();
	}

	private GameObject CreateGameObject(string objectName, bool active = true)
	{
		GameObject gameObject = new(objectName);
		if (active == false)
			gameObject.SetActive(false);
		createdObjects.Add(gameObject);
		return gameObject;
	}

	private static object GetPrivateStaticField(Type ownerType, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		return field.GetValue(null);
	}

	private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(target, value);
	}

	private static void SetPrivateStaticField(Type ownerType, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(null, value);
	}

	private static void InvokeNonPublic(Type ownerType, object target, string methodName)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		method.Invoke(target, null);
	}
}
