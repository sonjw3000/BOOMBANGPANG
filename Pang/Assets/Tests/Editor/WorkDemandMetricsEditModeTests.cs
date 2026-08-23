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
		StorageBuilding building = CreateStorageBuilding("Picking Demand Storage");
		OrderLine line = new(null, 101, 7, null);
		InvokeNonPublic(typeof(OrderManager), orderManager, "RegisterOrderLineForPicking", line);
		Assert.That(line.TryAllocatePicking(4), Is.EqualTo(4));

		Shelf source = CreateComponent<Shelf>("Picking Demand Shelf", active: false);
		Assert.That(
			building.PickingPlanner.AddReservedPickingRequest(line, source, 4, out PickingRequest request),
			Is.True);
		Assert.That(request.ReportAllocated(1), Is.EqualTo(1));

		WorkDemandSnapshot snapshot = metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.Picking);

		Assert.That(snapshot.SourceCount, Is.EqualTo(2));
		Assert.That(snapshot.ItemQuantity, Is.EqualTo(6));
		Assert.That(snapshot.HasDemand, Is.True);
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
		WorkDemandSnapshot snapshot = metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.Storing);

		Assert.That(firstSourceCount, Is.EqualTo(2));
		Assert.That(firstItemQuantity, Is.EqualTo(7));
		Assert.That(snapshot.SourceCount, Is.EqualTo(3));
		Assert.That(snapshot.ItemQuantity, Is.EqualTo(13));
	}

	[Test]
	public void PackingDemand_SeparatesInputWaitingAndOutputPhysicalSources()
	{
		PackingBuilding building = new("Packing Demand Building", new List<GridCell>());
		buildingManager.Register(building);

		CapsuleBuffer inputBuffer = CreateInboundBuffer(
			"Packing Input Demand Buffer",
			(301u, 5, ItemStatus.Labeled, ItemQuality.None),
			(302u, 3, ItemStatus.None, ItemQuality.None));
		PickingManifest manifest = outboundWorkflow.GetPickingManifest(inputBuffer.DockedCapsule);
		Assert.That(manifest, Is.Not.Null);
		manifest.AddPicked(new OrderLine(null, 301, 4, null), 301, 4);
		manifest.AddPicked(new OrderLine(null, 302, 10, null), 302, 10);
		RegisterCapsuleBuffer(building, inputBuffer);

		PackingStation station = CreateComponent<PackingStation>("Packing Demand Station", active: false);
		ToteBox waitingBox = CreateBox<ToteBox>(
			"Packing Waiting Box",
			BoxType.Personal,
			(303u, 8, ItemStatus.None, ItemQuality.None));
		ToteBox outputBox = CreateBox<ToteBox>(
			"Packing Output Box",
			BoxType.Personal,
			(304u, 6, ItemStatus.None, ItemQuality.None));
		SetPrivateField(
			typeof(PackingStation),
			station,
			"waitStackBox",
			new BoxWithOrder(waitingBox, new WorkJob(1, new List<WorkLine>(), WorkOp.Packing)));
		SetPrivateField(
			typeof(PackingStation),
			station,
			"endPackingBox",
			new BoxWithOrder(outputBox, new WorkJob(2, new List<WorkLine>(), WorkOp.Packing)));
		InvokeNonPublic(typeof(PackingStationService), packingStationService, "OnRegisterFacility", building.RuntimeBuildingId, station);
		InvokeNonPublic(typeof(PackingBuilding), building, "MarkPackingOutputDirty", station);

		WorkDemandSnapshot input = metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.PackingInput);
		WorkDemandSnapshot packing = metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.Packing);
		WorkDemandSnapshot output = metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.PackingOutput);

		Assert.That(input.SourceCount, Is.EqualTo(1));
		Assert.That(input.ItemQuantity, Is.EqualTo(7));
		Assert.That(packing.SourceCount, Is.EqualTo(1));
		Assert.That(packing.ItemQuantity, Is.EqualTo(8));
		Assert.That(output.SourceCount, Is.EqualTo(1));
		Assert.That(output.ItemQuantity, Is.EqualTo(6));
	}

	[Test]
	public void CapsuleRelocateDemand_FiltersPendingEntriesThatAreNoLongerActionable()
	{
		CapsuleBuffer source = CreateInboundBuffer(
			"Capsule Relocate Demand Source",
			(401u, 1, ItemStatus.None, ItemQuality.None));
		CapsuleBuffer target = CreateComponent<CapsuleBuffer>("Capsule Relocate Demand Target", active: false);
		target.SetDockState(CapsuleDockState.Empty);

		Assert.That(
			capsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
				source,
				CapsuleDockState.IB,
				CapsuleLogisticsState.IB,
				CapsuleDockState.Empty,
				CapsuleRelocateScope.GlobalAllowed,
				1)),
			Is.False);
		Assert.That(
			capsuleRelocateCoordinator.RequestDemand(new CapsuleRelocateDemand(
				target,
				CapsuleDockState.Empty,
				CapsuleDockState.IB,
				CapsuleLogisticsState.IB,
				CapsuleRelocateScope.GlobalAllowed,
				2)),
			Is.False);

		CapsuleRelocateDemandSnapshot initial = capsuleRelocateCoordinator.GetDemandSnapshot();
		Assert.That(initial.PendingSends, Is.EqualTo(1));
		Assert.That(initial.PendingDemands, Is.EqualTo(1));
		Assert.That(metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.CapsuleRelocate).SourceCount, Is.EqualTo(2));

		source.SetDockState(CapsuleDockState.Empty);

		CapsuleRelocateDemandSnapshot filtered = capsuleRelocateCoordinator.GetDemandSnapshot();
		Assert.That(capsuleRelocateCoordinator.PendingSendCount, Is.EqualTo(1));
		Assert.That(filtered.PendingSends, Is.Zero);
		Assert.That(filtered.PendingDemands, Is.EqualTo(1));
		Assert.That(metrics.GetWorkDemandSnapshot(LogisticsWorkCategory.CapsuleRelocate).SourceCount, Is.EqualTo(1));
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
