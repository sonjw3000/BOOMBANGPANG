using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WorkDemandMetricsEditModeTests
{
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private GameContext context;
	private TaskManager taskManager;
	private MetricsService metrics;
	private BuildingManager buildingManager;
	private OrderManager orderManager;
	private InboundWorkflowService inboundWorkflow;
	private OutboundWorkflowService outboundWorkflow;
	private PackingStationService packingStationService;
	private CapsuleBufferService capsuleBufferService;
	private CapsuleDockService capsuleDockService;
	private CapsuleRelocateCoordinator capsuleRelocateCoordinator;
	private uint nextBoxId;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		taskManager = CreateComponent<TaskManager>("Work Demand Test Task Manager", active: false);
		metrics = CreateComponent<MetricsService>("Work Demand Test Metrics", active: false);
		buildingManager = CreateComponent<BuildingManager>("Work Demand Test Building Manager", active: false);
		orderManager = CreateComponent<OrderManager>("Work Demand Test Order Manager", active: false);
		inboundWorkflow = CreateComponent<InboundWorkflowService>("Work Demand Test Inbound Workflow", active: false);
		outboundWorkflow = CreateComponent<OutboundWorkflowService>("Work Demand Test Outbound Workflow", active: false);
		packingStationService = CreateComponent<PackingStationService>("Work Demand Test Packing Station Service", active: false);
		capsuleBufferService = CreateComponent<CapsuleBufferService>("Work Demand Test Capsule Buffer Service", active: false);
		capsuleDockService = CreateComponent<CapsuleDockService>("Work Demand Test Capsule Dock Service", active: false);

		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");

		GameObject contextObject = CreateGameObject("Work Demand Test Context", active: false);
		context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateField(typeof(GameContext), context, "metrics", metrics);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "orderManager", orderManager);
		SetPrivateField(typeof(GameContext), context, "inboundWorkflowService", inboundWorkflow);
		SetPrivateField(typeof(GameContext), context, "outboundWorkflowService", outboundWorkflow);
		SetPrivateField(typeof(GameContext), context, "capsuleBufferService", capsuleBufferService);
		SetPrivateField(typeof(GameContext), context, "capsuleDockService", capsuleDockService);
		SetPrivateField(typeof(OutboundWorkflowService), outboundWorkflow, "packingStationService", packingStationService);

		capsuleRelocateCoordinator = new CapsuleRelocateCoordinator(null);
		SetPrivateField(typeof(GameContext), context, "capsuleRelocateCoordinator", capsuleRelocateCoordinator);
		SetPrivateStaticField(typeof(GameContext), "instance", context);
		SetPrivateField(
			typeof(InboundWorkflowService),
			inboundWorkflow,
			"storingPlanner",
			new StoringPlanner(capsuleBufferService));

		nextBoxId = 1;
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
	public void PickingDemand_CombinesUnallocatedOrdersAndPlannerRequestsWithoutAssignedQuantity()
	{
		StorageBuilding firstBuilding = CreateStorageBuilding("First Picking Demand Storage");
		StorageBuilding secondBuilding = CreateStorageBuilding("Second Picking Demand Storage");
		OrderLine line = new(null, 101, 12, null);
		InvokeNonPublic(typeof(OrderManager), orderManager, "RegisterOrderLineForPicking", line);
		Assert.That(line.TryAllocatePicking(9), Is.EqualTo(9));

		Shelf firstSource = CreateComponent<Shelf>("First Picking Demand Shelf", active: false);
		Assert.That(
			firstBuilding.PickingPlanner.AddReservedPickingRequest(line, firstSource, 4, out PickingRequest firstRequest),
			Is.True);
		Assert.That(firstRequest.ReportAllocated(1), Is.EqualTo(1));

		Shelf secondSource = CreateComponent<Shelf>("Second Picking Demand Shelf", active: false);
		Assert.That(
			secondBuilding.PickingPlanner.AddReservedPickingRequest(line, secondSource, 5, out PickingRequest secondRequest),
			Is.True);
		Assert.That(secondRequest.ReportAllocated(1), Is.EqualTo(1));

		AssertDemand(LogisticsWorkCategory.Picking, firstBuilding.RuntimeBuildingId, 1, 3);
		AssertDemand(LogisticsWorkCategory.Picking, secondBuilding.RuntimeBuildingId, 1, 4);
		AssertDemand(LogisticsWorkCategory.Picking, 0, 1, 3);
		AssertDemand(LogisticsWorkCategory.Picking, uint.MaxValue, 0, 0);
		AssertGlobalPartition(LogisticsWorkCategory.Picking, 3, 10);
	}

	[Test]
	public void StoringDemand_CountsBufferItemSourcesAndExcludesWasteWithinBuildingScope()
	{
		StorageBuilding firstBuilding = CreateStorageBuilding("First Storing Demand Storage");
		StorageBuilding secondBuilding = CreateStorageBuilding("Second Storing Demand Storage");

		CapsuleBuffer firstBuffer = CreateInboundBuffer(
			"First Storing Demand Buffer",
			(201u, 3, ItemStatus.None, ItemQuality.None),
			(201u, 2, ItemStatus.None, ItemQuality.Waste),
			(202u, 4, ItemStatus.Labeled, ItemQuality.None));
		RegisterCapsuleBuffer(firstBuilding, firstBuffer);

		CapsuleBuffer secondBuffer = CreateInboundBuffer(
			"Second Storing Demand Buffer",
			(203u, 6, ItemStatus.None, ItemQuality.None));
		RegisterCapsuleBuffer(secondBuilding, secondBuffer);

		inboundWorkflow.StoringPlanner.GetPendingDemand(
			firstBuilding.RuntimeBuildingId,
			out int firstSourceCount,
			out int firstItemQuantity);
		Assert.That(firstSourceCount, Is.EqualTo(2));
		Assert.That(firstItemQuantity, Is.EqualTo(7));
		AssertDemand(LogisticsWorkCategory.Storing, firstBuilding.RuntimeBuildingId, 2, 7);
		AssertDemand(LogisticsWorkCategory.Storing, secondBuilding.RuntimeBuildingId, 1, 6);
		AssertDemand(LogisticsWorkCategory.Storing, 0, 0, 0);
		AssertGlobalPartition(LogisticsWorkCategory.Storing, 3, 13);
	}

	[Test]
	public void PackingDemand_SeparatesInputWaitingAndOutputPhysicalSources()
	{
		PackingBuilding firstBuilding = new("First Packing Demand Building", new List<GridCell>());
		buildingManager.Register(firstBuilding);

		CapsuleBuffer firstInputBuffer = CreateInboundBuffer(
			"First Packing Input Demand Buffer",
			(301u, 5, ItemStatus.Labeled, ItemQuality.None),
			(302u, 3, ItemStatus.None, ItemQuality.None));
		PickingManifest firstManifest = outboundWorkflow.GetPickingManifest(firstInputBuffer.DockedCapsule);
		Assert.That(firstManifest, Is.Not.Null);
		firstManifest.AddPicked(new OrderLine(null, 301, 4, null), 301, 4);
		firstManifest.AddPicked(new OrderLine(null, 302, 10, null), 302, 10);
		RegisterCapsuleBuffer(firstBuilding, firstInputBuffer);

		PackingStation firstStation = CreateComponent<PackingStation>("First Packing Demand Station", active: false);
		ToteBox firstWaitingBox = CreateBox<ToteBox>(
			"First Packing Waiting Box",
			BoxType.Personal,
			(303u, 8, ItemStatus.None, ItemQuality.None));
		ToteBox firstOutputBox = CreateBox<ToteBox>(
			"First Packing Output Box",
			BoxType.Personal,
			(304u, 6, ItemStatus.None, ItemQuality.None));
		SetPrivateField(
			typeof(PackingStation),
			firstStation,
			"waitStackBox",
			new BoxWithOrder(firstWaitingBox, new WorkJob(1, new List<WorkLine>(), WorkOp.Packing)));
		SetPrivateField(
			typeof(PackingStation),
			firstStation,
			"endPackingBox",
			new BoxWithOrder(firstOutputBox, new WorkJob(2, new List<WorkLine>(), WorkOp.Packing)));
		InvokeNonPublic(
			typeof(PackingStationService),
			packingStationService,
			"OnRegisterFacility",
			firstBuilding.RuntimeBuildingId,
			firstStation);
		InvokeNonPublic(typeof(PackingBuilding), firstBuilding, "MarkPackingOutputDirty", firstStation);

		PackingBuilding secondBuilding = new("Second Packing Demand Building", new List<GridCell>());
		buildingManager.Register(secondBuilding);
		CapsuleBuffer secondInputBuffer = CreateInboundBuffer(
			"Second Packing Input Demand Buffer",
			(305u, 9, ItemStatus.Labeled, ItemQuality.None));
		PickingManifest secondManifest = outboundWorkflow.GetPickingManifest(secondInputBuffer.DockedCapsule);
		Assert.That(secondManifest, Is.Not.Null);
		secondManifest.AddPicked(new OrderLine(null, 305, 5, null), 305, 5);
		RegisterCapsuleBuffer(secondBuilding, secondInputBuffer);

		PackingStation secondStation = CreateComponent<PackingStation>("Second Packing Demand Station", active: false);
		ToteBox secondWaitingBox = CreateBox<ToteBox>(
			"Second Packing Waiting Box",
			BoxType.Personal,
			(306u, 4, ItemStatus.None, ItemQuality.None));
		ToteBox secondOutputBox = CreateBox<ToteBox>(
			"Second Packing Output Box",
			BoxType.Personal,
			(307u, 2, ItemStatus.None, ItemQuality.None));
		SetPrivateField(
			typeof(PackingStation),
			secondStation,
			"waitStackBox",
			new BoxWithOrder(secondWaitingBox, new WorkJob(3, new List<WorkLine>(), WorkOp.Packing)));
		SetPrivateField(
			typeof(PackingStation),
			secondStation,
			"endPackingBox",
			new BoxWithOrder(secondOutputBox, new WorkJob(4, new List<WorkLine>(), WorkOp.Packing)));
		InvokeNonPublic(
			typeof(PackingStationService),
			packingStationService,
			"OnRegisterFacility",
			secondBuilding.RuntimeBuildingId,
			secondStation);
		InvokeNonPublic(typeof(PackingBuilding), secondBuilding, "MarkPackingOutputDirty", secondStation);

		AssertDemand(LogisticsWorkCategory.PackingInput, firstBuilding.RuntimeBuildingId, 1, 7);
		AssertDemand(LogisticsWorkCategory.PackingInput, secondBuilding.RuntimeBuildingId, 1, 5);
		AssertDemand(LogisticsWorkCategory.PackingInput, 0, 0, 0);
		AssertGlobalPartition(LogisticsWorkCategory.PackingInput, 2, 12);

		AssertDemand(LogisticsWorkCategory.Packing, firstBuilding.RuntimeBuildingId, 1, 8);
		AssertDemand(LogisticsWorkCategory.Packing, secondBuilding.RuntimeBuildingId, 1, 4);
		AssertDemand(LogisticsWorkCategory.Packing, 0, 0, 0);
		AssertGlobalPartition(LogisticsWorkCategory.Packing, 2, 12);

		AssertDemand(LogisticsWorkCategory.PackingOutput, firstBuilding.RuntimeBuildingId, 1, 6);
		AssertDemand(LogisticsWorkCategory.PackingOutput, secondBuilding.RuntimeBuildingId, 1, 2);
		AssertDemand(LogisticsWorkCategory.PackingOutput, 0, 0, 0);
		AssertGlobalPartition(LogisticsWorkCategory.PackingOutput, 2, 8);
	}

	[Test]
	public void CapsuleRelocateDemand_FiltersPendingEntriesThatAreNoLongerActionable()
	{
		StorageBuilding sourceBuilding = CreateStorageBuilding("Capsule Relocate Source Building");
		StorageBuilding targetBuilding = CreateStorageBuilding("Capsule Relocate Target Building");
		CapsuleBuffer source = CreateInboundBuffer(
			"Capsule Relocate Demand Source",
			(401u, 1, ItemStatus.None, ItemQuality.None));
		CapsuleBuffer target = CreateComponent<CapsuleBuffer>("Capsule Relocate Demand Target", active: false);
		target.SetDockState(CapsuleDockState.Empty);
		CapsuleBuffer hubTarget = CreateComponent<CapsuleBuffer>("Capsule Relocate Hub Target", active: false);
		hubTarget.SetDockState(CapsuleDockState.Empty);
		CapsuleBuffer orphanTarget = CreateComponent<CapsuleBuffer>("Capsule Relocate Orphan Target", active: false);
		orphanTarget.SetDockState(CapsuleDockState.Empty);

		Assert.That(
			capsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
				source,
				CapsuleDockState.IB,
				CapsuleLogisticsState.IB,
				CapsuleDockState.Empty,
				CapsuleRelocateScope.GlobalAllowed,
				sourceBuilding.RuntimeBuildingId)),
			Is.False);
		Assert.That(
			capsuleRelocateCoordinator.RequestDemand(new CapsuleRelocateDemand(
				target,
				CapsuleDockState.Empty,
				CapsuleDockState.IB,
				CapsuleLogisticsState.IB,
				CapsuleRelocateScope.GlobalAllowed,
				targetBuilding.RuntimeBuildingId)),
			Is.False);
		Assert.That(
			capsuleRelocateCoordinator.RequestDemand(new CapsuleRelocateDemand(
				hubTarget,
				CapsuleDockState.Empty,
				CapsuleDockState.IB,
				CapsuleLogisticsState.IB,
				CapsuleRelocateScope.GlobalAllowed,
				0)),
			Is.False);
		Assert.That(
			capsuleRelocateCoordinator.RequestDemand(new CapsuleRelocateDemand(
				orphanTarget,
				CapsuleDockState.Empty,
				CapsuleDockState.IB,
				CapsuleLogisticsState.IB,
				CapsuleRelocateScope.GlobalAllowed,
				uint.MaxValue)),
			Is.False);

		CapsuleRelocateDemandSnapshot initial = capsuleRelocateCoordinator.GetDemandSnapshot();
		Assert.That(initial.PendingSends, Is.EqualTo(1));
		Assert.That(initial.PendingDemands, Is.EqualTo(3));
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, sourceBuilding.RuntimeBuildingId, 1, 0);
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, targetBuilding.RuntimeBuildingId, 1, 0);
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, 0, 2, 0);
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, uint.MaxValue, 0, 0);
		AssertGlobalPartition(LogisticsWorkCategory.CapsuleRelocate, 4, 0);

		source.SetDockState(CapsuleDockState.Empty);

		CapsuleRelocateDemandSnapshot filtered = capsuleRelocateCoordinator.GetDemandSnapshot();
		Assert.That(capsuleRelocateCoordinator.PendingSendCount, Is.EqualTo(1));
		Assert.That(filtered.PendingSends, Is.Zero);
		Assert.That(filtered.PendingDemands, Is.EqualTo(3));
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, sourceBuilding.RuntimeBuildingId, 0, 0);
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, targetBuilding.RuntimeBuildingId, 1, 0);
		AssertDemand(LogisticsWorkCategory.CapsuleRelocate, 0, 2, 0);
		AssertGlobalPartition(LogisticsWorkCategory.CapsuleRelocate, 3, 0);
	}

	private void AssertDemand(
		LogisticsWorkCategory category,
		uint buildingId,
		int expectedSourceCount,
		int expectedItemQuantity)
	{
		WorkDemandSnapshot snapshot = metrics.GetWorkDemandSnapshot(category, buildingId);
		Assert.That(
			snapshot.SourceCount,
			Is.EqualTo(expectedSourceCount),
			$"{category} source count for building {buildingId}");
		Assert.That(
			snapshot.ItemQuantity,
			Is.EqualTo(expectedItemQuantity),
			$"{category} item quantity for building {buildingId}");
	}

	private void AssertGlobalPartition(
		LogisticsWorkCategory category,
		int expectedSourceCount,
		int expectedItemQuantity)
	{
		WorkDemandSnapshot all = metrics.GetWorkDemandSnapshot(category);
		WorkDemandSnapshot unassigned = metrics.GetWorkDemandSnapshot(category, 0);
		int partitionSourceCount = unassigned.SourceCount;
		int partitionItemQuantity = unassigned.ItemQuantity;

		for (int i = 0; i < buildingManager.RegisteredBuildings.Count; ++i)
		{
			Building building = buildingManager.RegisteredBuildings[i];
			if (building == null)
				continue;

			WorkDemandSnapshot buildingDemand =
				metrics.GetWorkDemandSnapshot(category, building.RuntimeBuildingId);
			partitionSourceCount += buildingDemand.SourceCount;
			partitionItemQuantity += buildingDemand.ItemQuantity;
		}

		Assert.That(all.SourceCount, Is.EqualTo(expectedSourceCount), $"{category} all source count");
		Assert.That(all.ItemQuantity, Is.EqualTo(expectedItemQuantity), $"{category} all item quantity");
		Assert.That(all.SourceCount, Is.EqualTo(partitionSourceCount), $"{category} source partition");
		Assert.That(all.ItemQuantity, Is.EqualTo(partitionItemQuantity), $"{category} item partition");
	}

	private StorageBuilding CreateStorageBuilding(string displayName)
	{
		StorageBuilding building = new(displayName, new List<GridCell>());
		buildingManager.Register(building);
		Assert.That(building.RuntimeBuildingId, Is.Not.Zero);
		return building;
	}

	private CapsuleBuffer CreateInboundBuffer(
		string objectName,
		params (uint ItemId, int Quantity, ItemStatus Status, ItemQuality Quality)[] contents)
	{
		CargoCapsule capsule = CreateBox<CargoCapsule>(
			$"{objectName} Capsule",
			BoxType.Capsule,
			contents);
		capsule.SetLogisticsState(CapsuleLogisticsState.IB);

		CapsuleBuffer buffer = CreateComponent<CapsuleBuffer>(objectName, active: false);
		buffer.SetDockState(CapsuleDockState.IB);
		Assert.That(buffer.TryDockCapsule(capsule), Is.True);
		return buffer;
	}

	private void RegisterCapsuleBuffer(Building building, CapsuleBuffer buffer)
	{
		Assert.That(buildingManager.TryRegisterFacility(building.RuntimeBuildingId, buffer), Is.True);
		InvokeNonPublic(
			typeof(CapsuleBufferService),
			capsuleBufferService,
			"OnRegisterFacility",
			building.RuntimeBuildingId,
			buffer);
	}

	private T CreateBox<T>(
		string objectName,
		BoxType boxType,
		params (uint ItemId, int Quantity, ItemStatus Status, ItemQuality Quality)[] contents)
		where T : BoxBase
	{
		T box = CreateComponent<T>(objectName, active: false);
		SetPrivateField(typeof(BoxBase), box, "boxType", boxType);
		box.SetBoxId(nextBoxId++);
		InvokeNonPublic(typeof(BoxBase), box, "MarkValid");

		for (int i = 0; i < contents.Length; ++i)
			AddStack(box, contents[i].ItemId, contents[i].Quantity, contents[i].Status, contents[i].Quality);

		return box;
	}

	private static void AddStack(
		BoxBase box,
		uint itemId,
		int quantity,
		ItemStatus status,
		ItemQuality quality)
	{
		ItemStack stack = new(itemId, status: status, quality: quality);
		Assert.That(stack.AddItem(quantity), Is.EqualTo(quantity));

		List<ItemStack> stacks = (List<ItemStack>)GetPrivateField(typeof(BoxBase), box, "stacks");
		Dictionary<uint, int> totals = (Dictionary<uint, int>)GetPrivateField(typeof(BoxBase), box, "itemTotals");
		stacks.Add(stack);
		totals[itemId] = totals.GetValueOrDefault(itemId) + quantity;
	}

	private T CreateComponent<T>(string objectName, bool active) where T : Component
	{
		return CreateGameObject(objectName, active).AddComponent<T>();
	}

	private GameObject CreateGameObject(string objectName, bool active)
	{
		GameObject gameObject = new(objectName);
		gameObject.SetActive(active);
		createdObjects.Add(gameObject);
		return gameObject;
	}

	private static object GetPrivateField(Type ownerType, object target, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		return field.GetValue(target);
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

	private static object InvokeNonPublic(
		Type ownerType,
		object target,
		string methodName,
		params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(
			methodName,
			BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		return method.Invoke(target, arguments);
	}
}
