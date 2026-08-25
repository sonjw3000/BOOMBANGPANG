using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class PickingSharedOutputEditModeTests
{
	private const uint TestItemId = 97001;
	private readonly List<UnityEngine.Object> createdObjects = new();
	private GameContext previousContext;
	private FacilityManager facilityManager;
	private FacilityRuleManager ruleManager;
	private BuildingManager buildingManager;
	private CapsuleBufferService bufferService;
	private OutboundWorkflowService outboundWorkflow;
	private TaskManager taskManager;
	private Building building;
	private uint nextBoxId;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		ItemDatabase itemDatabase = CreateComponent<ItemDatabase>("Shared Picking Item Database", false);
		ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
		createdObjects.Add(itemDefinition);
		SetPrivateField(typeof(ItemDefinition), itemDefinition, "itemID", TestItemId);
		SetPrivateField(typeof(ItemDefinition), itemDefinition, "size", 1.0f);
		SetPrivateField(
			typeof(ItemDatabase),
			itemDatabase,
			"itemIDMap",
			new Dictionary<uint, ItemDefinition> { [TestItemId] = itemDefinition });

		facilityManager = CreateComponent<FacilityManager>("Shared Picking Facility Manager", false);
		ruleManager = CreateComponent<FacilityRuleManager>("Shared Picking Rule Manager", false);
		buildingManager = CreateComponent<BuildingManager>("Shared Picking Building Manager", false);
		bufferService = CreateComponent<CapsuleBufferService>("Shared Picking Buffer Service", false);
		outboundWorkflow = CreateComponent<OutboundWorkflowService>("Shared Picking Outbound", false);
		taskManager = CreateComponent<TaskManager>("Shared Picking Task Manager", false);

		GameContext context = CreateComponent<GameContext>("Shared Picking Context", false);
		SetPrivateField(typeof(GameContext), context, "itemDB", itemDatabase);
		SetPrivateField(typeof(GameContext), context, "facilityManager", facilityManager);
		SetPrivateField(typeof(GameContext), context, "facilityRuleManager", ruleManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "capsuleBufferService", bufferService);
		SetPrivateField(typeof(GameContext), context, "outboundWorkflowService", outboundWorkflow);
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");
		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");
		ruleManager.gameObject.SetActive(true);
		bufferService.gameObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<CapsuleBuffer>),
			bufferService,
			"TryBindFacilityManager");

		building = new Building("Shared Picking Building", new List<GridCell>(), ItemProcessStage.Picked);
		buildingManager.Register(building);
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
	public void PickingOutput_AllowsMultipleTasksToRetainCompatibleInsideCapsule()
	{
		CapsuleBuffer buffer = CreatePickedBuffer(currentQuantity: 2);
		ItemTransferTask first = CreatePickingTask();
		ItemTransferTask second = CreatePickingTask();
		AddReadyTask(first);
		AddReadyTask(second);
		Retain(first, buffer, quantity: 2);

		AssertSharedCandidatePrerequisites(buffer);
		Assert.That(IsPickingOutputCandidate(second, buffer), Is.True);
		Retain(second, buffer, quantity: 2);
		Assert.That(HasPickingDependency(buffer), Is.True);
		Assert.That(HasConflictingDependency(buffer), Is.False);
	}

	[Test]
	public void PickingOutput_ThresholdBlocksLateTaskUntilExistingRetainsRelease()
	{
		CapsuleBuffer buffer = CreatePickedBuffer(currentQuantity: 7);
		ItemTransferTask first = CreatePickingTask();
		ItemTransferTask second = CreatePickingTask();
		ItemTransferTask late = CreatePickingTask();
		AddReadyTask(first);
		AddReadyTask(second);
		AddReadyTask(late);
		Retain(first, buffer, quantity: 1);
		Retain(second, buffer, quantity: 1);

		Assert.That(IsPickingOutputCandidate(late, buffer), Is.True);
		ToteBox source = CreateBox<ToteBox>("Threshold Crossing Source", BoxType.Personal, 10.0f);
		Assert.That(source.AddItem(TestItemId, 1), Is.EqualTo(1));
		source.Stacks[0].SetStatus(ItemStatus.Labeled);
		OrderLine crossingLine = new(null, TestItemId, 1, null);
		Assert.That(
			outboundWorkflow.GetPickingManifest(source).AddPicked(crossingLine, TestItemId, 1),
			Is.EqualTo(1));
		Assert.That(
			ItemTransferUtility.MoveItem(new ItemTransferPayload(source, buffer, TestItemId, 1)).Kind,
			Is.EqualTo(TransferResultKind.Complete));
		Assert.That(
			outboundWorkflow.GetPickingManifest(buffer.DockedCapsule).AddPicked(
				crossingLine,
				TestItemId,
				1),
			Is.EqualTo(1));

		Assert.That(IsPickingOutputCandidate(late, buffer), Is.False);
		Assert.That(HasPickingDependency(buffer), Is.True);

		CapsuleRelocateCoordinator coordinator = new(
			dockService: null,
			taskManager: taskManager,
			buildingManager: buildingManager,
			facilityManager: facilityManager);
		ReleaseRetain(first, buffer);
		InvokeNonPublic(
			typeof(CapsuleRelocateCoordinator),
			coordinator,
			"NormalizeCapsuleState",
			buffer,
			buffer.DockedCapsule,
			building);
		Assert.That(buffer.DockedCapsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Inside));
		Assert.That(HasPickingDependency(buffer), Is.True);
		ReleaseRetain(second, buffer);
		InvokeNonPublic(
			typeof(CapsuleRelocateCoordinator),
			coordinator,
			"NormalizeCapsuleState",
			buffer,
			buffer.DockedCapsule,
			building);
		Assert.That(buffer.DockedCapsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.OB));
		Assert.That(HasPickingDependency(buffer), Is.False);
	}

	[Test]
	public void PickingOutput_PartialCapacityMovesAvailableQuantityOnly()
	{
		CapsuleBuffer buffer = CreatePickedBuffer(currentQuantity: 9);
		ToteBox source = CreateBox<ToteBox>("Shared Picking Source", BoxType.Personal, 10.0f);
		Assert.That(source.AddItem(TestItemId, 3), Is.EqualTo(3));

		Assert.That(
			ItemTransferUtility.GetMovableQuantity(source, buffer, TestItemId, 3),
			Is.EqualTo(1));
		ItemTransferResult result = ItemTransferUtility.MoveItem(
			new ItemTransferPayload(source, buffer, TestItemId, 3));
		Assert.That(result.Kind, Is.EqualTo(TransferResultKind.Partial));
		Assert.That(result.Moved, Is.EqualTo(1));
		Assert.That(source.GetQuantity(TestItemId), Is.EqualTo(2));
	}

	private CapsuleBuffer CreatePickedBuffer(int currentQuantity)
	{
		CapsuleBuffer buffer = CreateComponent<CapsuleBuffer>("Shared Picked Buffer", false);
		buffer.OnPositionSet(new int3(1, 0, 1), FacingDirection.North);
		facilityManager.RegisterFacility(building.RuntimeBuildingId, buffer);

		CargoCapsule capsule = CreateBox<CargoCapsule>("Shared Picked Capsule", BoxType.Capsule, 10.0f);
		Assert.That(capsule.AddItem(TestItemId, currentQuantity), Is.EqualTo(currentQuantity));
		for (int i = 0; i < capsule.Stacks.Count; ++i)
			capsule.Stacks[i].SetStatus(ItemStatus.Labeled);
		OrderLine existingLine = new(null, TestItemId, currentQuantity, null);
		Assert.That(
			outboundWorkflow.GetPickingManifest(capsule).AddPicked(existingLine, TestItemId, currentQuantity),
			Is.EqualTo(currentQuantity));
		capsule.SetLogisticsState(CapsuleLogisticsState.Inside);
		Assert.That(buffer.TryDockCapsule(capsule), Is.True);

		FacilityRule rule = new();
		rule.SetRequiredContentState(FacilityContentState.HasItems);
		rule.SetRequiredItemProcessStage(ItemProcessStage.Picked);
		FacilityRulePreset preset = ruleManager.CreatePreset("Shared Picked Rule", rule);
		Assert.That(ruleManager.ApplyPreset(buffer, preset.Id), Is.True);
		return buffer;
	}

	private ItemTransferTask CreatePickingTask()
	{
		return new ItemTransferTask(
			WorkerTask.TaskType.Picking,
			new ItemTransferJob(
				planner: null,
				TransferObjectType.Item,
				TransferObjectType.Item,
				building.RuntimeBuildingId));
	}

	private void AddReadyTask(ItemTransferTask task)
	{
		taskManager.TaskQueue[WorkerTask.TaskType.Picking].AddLast(task);
	}

	private bool IsPickingOutputCandidate(ItemTransferTask task, CapsuleBuffer buffer)
	{
		FacilityFilter filter = CreateProjectedPickedFilter();
		return InvokePrivateStatic<bool>(
			typeof(PickingPlanner),
			"IsPickingOutputBufferCandidate",
			task,
			buffer,
			building.RuntimeBuildingId,
			building,
			filter,
			false);
	}

	private void AssertSharedCandidatePrerequisites(CapsuleBuffer buffer)
	{
		FacilityFilter filter = CreateProjectedPickedFilter();
		Assert.That(buffer.DockedCapsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Inside));
		Assert.That(buffer.IsCapsuleEmpty(), Is.False);
		Assert.That(
			bufferService.IsExplicitRuleMatchedBuffer(
				buffer,
				filter,
				FacilityContentState.HasItems,
				ItemProcessStage.Picked),
			Is.True,
			"projected rule");
		Assert.That(
			bufferService.IsRuleMatchedBuffer(buffer, buffer.DockedCapsule, evaluateLaunchReadiness: false),
			Is.True,
			"current capsule rule");
		Assert.That(bufferService.TryGetRegisteredBuildingId(buffer, out uint ownerId), Is.True);
		Assert.That(ownerId, Is.EqualTo(building.RuntimeBuildingId));
		Assert.That(HasConflictingDependency(buffer), Is.False, "task conflict");
		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(Building),
				building,
				"CanDispatchOutboundBuffer",
				buffer),
			Is.False,
			"outbound threshold");
		Assert.That(facilityManager.IsInvalidating(buffer), Is.False, "invalidation");
	}

	private static FacilityFilter CreateProjectedPickedFilter()
	{
		return new FacilityFilter(
			manifestFilter: FacilityManifestFilter.FromOrderLine(new OrderLine(null, TestItemId, 1, null)),
			itemProcessStage: ItemProcessStage.Picked,
			contentState: FacilityContentState.HasItems);
	}

	private static void Retain(ItemTransferTask task, CapsuleBuffer buffer, int quantity)
	{
		InvokeNonPublic(
			typeof(ItemTransferTask),
			task,
			"RetainPickingOutput",
			new WorkLine(WorkLineAction.Put, buffer, buffer, TestItemId, quantity));
	}

	private static void ReleaseRetain(ItemTransferTask task, CapsuleBuffer buffer)
	{
		InvokeNonPublic(typeof(ItemTransferTask), task, "ReleaseRetainedPickingOutput", buffer);
	}

	private bool HasPickingDependency(CapsuleBuffer buffer)
	{
		return InvokePrivateInstance<bool>(
			typeof(TaskManager),
			taskManager,
			"HasManagedPickingOutputDependency",
			buffer);
	}

	private bool HasConflictingDependency(CapsuleBuffer buffer)
	{
		return InvokePrivateInstance<bool>(
			typeof(TaskManager),
			taskManager,
			"HasConflictingPickingOutputDependency",
			buffer);
	}

	private T CreateBox<T>(string name, BoxType boxType, float capacity) where T : BoxBase
	{
		T box = CreateComponent<T>(name, false);
		SetPrivateField(typeof(BoxBase), box, "boxType", boxType);
		SetPrivateField(typeof(BoxBase), box, "capacity", capacity);
		box.SetBoxId(nextBoxId++);
		return box;
	}

	private T CreateComponent<T>(string name, bool active) where T : Component
	{
		GameObject gameObject = new(name);
		gameObject.SetActive(active);
		createdObjects.Add(gameObject);
		return gameObject.AddComponent<T>();
	}

	private static object GetPrivateStaticField(Type ownerType, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null);
		return field.GetValue(null);
	}

	private static void SetPrivateStaticField(Type ownerType, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null);
		field.SetValue(null, value);
	}

	private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null);
		field.SetValue(target, value);
	}

	private static void InvokeNonPublic(Type ownerType, object target, string methodName, params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null);
		method.Invoke(target, arguments);
	}

	private static T InvokePrivateStatic<T>(Type ownerType, string methodName, params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null);
		return (T)method.Invoke(null, arguments);
	}

	private static T InvokePrivateInstance<T>(Type ownerType, object target, string methodName, params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null);
		return (T)method.Invoke(target, arguments);
	}
}
