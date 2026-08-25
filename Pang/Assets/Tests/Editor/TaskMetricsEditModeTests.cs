using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.AI.BT;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class TaskMetricsEditModeTests
{
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private TaskManager taskManager;
	private MetricsService metrics;
	private WorkerManager workerManager;
	private BuildingManager buildingManager;
	private FacilityManager facilityManager;
	private OutboundWorkflowService outboundWorkflow;
	private PackingStationService packingStationService;
	private ProcessStatsCollector processStats;
	private RestFacilityService restFacilityService;
	private int nextWorkerPosition;
	private int nextTaskObjectId;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		taskManager = CreateComponent<TaskManager>("Task Metrics Test Task Manager");
		metrics = CreateComponent<MetricsService>("Task Metrics Test Metrics Service");
		workerManager = CreateComponent<WorkerManager>("Task Metrics Test Worker Manager");
		buildingManager = CreateComponent<BuildingManager>("Task Metrics Test Building Manager");
		facilityManager = CreateComponent<FacilityManager>("Task Metrics Test Facility Manager");
		outboundWorkflow = CreateGameObject("Task Metrics Test Outbound Workflow", active: false)
			.AddComponent<OutboundWorkflowService>();
		packingStationService = CreateGameObject("Task Metrics Test Packing Station Service", active: false)
			.AddComponent<PackingStationService>();
		processStats = CreateComponent<ProcessStatsCollector>("Task Metrics Test Process Stats");
		restFacilityService = CreateComponent<RestFacilityService>("Task Metrics Test Rest Facility Service");

		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");
		InvokeNonPublic(typeof(WorkerManager), workerManager, "Awake");
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");
		InvokeNonPublic(typeof(ProcessStatsCollector), processStats, "Awake");

		GameObject contextObject = CreateGameObject("Task Metrics Test Context", active: false);
		GameContext context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateField(typeof(GameContext), context, "metrics", metrics);
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "facilityManager", facilityManager);
		SetPrivateField(typeof(GameContext), context, "outboundWorkflowService", outboundWorkflow);
		SetPrivateField(typeof(GameContext), context, "processStats", processStats);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
		SetPrivateField(
			typeof(OutboundWorkflowService),
			outboundWorkflow,
			"packingStationService",
			packingStationService);
		SetPrivateStaticField(typeof(GameContext), "instance", context);
		nextWorkerPosition = 10;
		nextTaskObjectId = 1;
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
	public void GetTaskCountSnapshot_SeparatesLifecycleStatesAndBlockedSubset()
	{
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.Picking));
		taskManager.AddRestoredReturnedTask(new TestWorkerTask(WorkerTask.TaskType.Picking));

		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Picking),
			WorkerStatusAction.Working);
		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Picking),
			WorkerStatusAction.WaitingForItems);
		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Picking),
			WorkerStatusAction.TrafficBlock);

		TaskCountSnapshot snapshot = metrics.GetTaskCountSnapshot(WorkerTask.TaskType.Picking);

		Assert.That(snapshot.Ready, Is.EqualTo(1));
		Assert.That(snapshot.Returned, Is.EqualTo(1));
		Assert.That(snapshot.Active, Is.EqualTo(3));
		Assert.That(snapshot.Blocked, Is.EqualTo(2));
		Assert.That(snapshot.Waiting, Is.EqualTo(2));
		Assert.That(snapshot.Total, Is.EqualTo(5));
	}

	[Test]
	public void GetCapsuleRelocationTaskCountSnapshot_UsesConcreteTaskClass()
	{
		taskManager.EnqueueTask(CreateCapsuleTask(WorkerTask.TaskType.OB));
		taskManager.AddRestoredReturnedTask(CreateCapsuleTask(WorkerTask.TaskType.IB));
		AddActiveTask(
			CreateCapsuleTask(WorkerTask.TaskType.CargoTransfer),
			WorkerStatusAction.WaitingForTargetBuilding);
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.OB));

		TaskCountSnapshot snapshot = metrics.GetCapsuleRelocationTaskCountSnapshot();

		Assert.That(snapshot.Ready, Is.EqualTo(1));
		Assert.That(snapshot.Returned, Is.EqualTo(1));
		Assert.That(snapshot.Active, Is.EqualTo(1));
		Assert.That(snapshot.Blocked, Is.EqualTo(1));
		Assert.That(snapshot.Total, Is.EqualTo(3));
	}

	[Test]
	public void GetTaskCountSnapshot_BuildingScopePartitionsAllLogisticsCategories()
	{
		Building firstBuilding = new("First Task Metrics Building", new List<GridCell>(), CargoProcessStage.Packed);
		Building secondBuilding = new("Second Task Metrics Building", new List<GridCell>(), CargoProcessStage.Packed);
		buildingManager.Register(firstBuilding);
		buildingManager.Register(secondBuilding);

		foreach (LogisticsWorkCategory category in Enum.GetValues(typeof(LogisticsWorkCategory)))
		{
			taskManager.EnqueueTask(CreateLogisticsTask(category, firstBuilding.RuntimeBuildingId));
			taskManager.AddRestoredReturnedTask(CreateLogisticsTask(category, secondBuilding.RuntimeBuildingId));
			AddActiveTask(
				CreateLogisticsTask(category, firstBuilding.RuntimeBuildingId),
				WorkerStatusAction.Working);
			AddActiveTask(
				CreateLogisticsTask(category, secondBuilding.RuntimeBuildingId),
				WorkerStatusAction.WaitingForItems);
			taskManager.EnqueueTask(CreateLogisticsTask(category, 0));

			int expectedAllReady = 2;
			int expectedUnassignedReady = 1;
			if (category == LogisticsWorkCategory.CapsuleRelocate)
			{
				taskManager.EnqueueTask(CreateLogisticsTask(category, uint.MaxValue));
				expectedAllReady = 3;
				expectedUnassignedReady = 2;
			}

			AssertTaskSnapshot(
				metrics.GetTaskCountSnapshot(category),
				expectedAllReady,
				1,
				2,
				1,
				$"{category} all");
			AssertTaskSnapshot(
				metrics.GetTaskCountSnapshot(category, firstBuilding.RuntimeBuildingId),
				1,
				0,
				1,
				0,
				$"{category} first building");
			AssertTaskSnapshot(
				metrics.GetTaskCountSnapshot(category, secondBuilding.RuntimeBuildingId),
				0,
				1,
				1,
				1,
				$"{category} second building");
			AssertTaskSnapshot(
				metrics.GetTaskCountSnapshot(category, 0),
				expectedUnassignedReady,
				0,
				0,
				0,
				$"{category} Hub / Unassigned");
			AssertTaskSnapshot(
				metrics.GetTaskCountSnapshot(category, uint.MaxValue),
				0,
				0,
				0,
				0,
				$"{category} unknown building query");
			AssertTaskPartition(category);
		}
	}

	[Test]
	public void GetTaskCountSnapshot_BuildingScopeSupportsLegacyTaskOwnership()
	{
		Building firstBuilding = new("Legacy Picking Task Building", new List<GridCell>(), CargoProcessStage.Picked);
		Building secondBuilding = new("Legacy Storing Task Building", new List<GridCell>(), CargoProcessStage.Picked);
		buildingManager.Register(firstBuilding);
		buildingManager.Register(secondBuilding);

		taskManager.EnqueueTask(new PickingTask(null, firstBuilding.RuntimeBuildingId));
		taskManager.EnqueueTask(new StoringTask(null, secondBuilding.RuntimeBuildingId));
		taskManager.EnqueueTask(new CapsuleRelocationTask(
			WorkerTask.TaskType.Storing,
			null,
			null,
			firstBuilding.RuntimeBuildingId,
			CapsuleRelocationReason.StateMismatch));
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.Picking));

		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.Picking, firstBuilding.RuntimeBuildingId),
			1,
			0,
			0,
			0,
			"Legacy Picking building");
		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.Picking, 0),
			1,
			0,
			0,
			0,
			"Unsupported Picking concrete is unassigned");
		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.Storing, firstBuilding.RuntimeBuildingId),
			1,
			0,
			0,
			0,
			"Legacy Capsule task also keeps its Storing type");
		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.Storing, secondBuilding.RuntimeBuildingId),
			1,
			0,
			0,
			0,
			"Legacy Storing building");
		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.CapsuleRelocate, firstBuilding.RuntimeBuildingId),
			1,
			0,
			0,
			0,
			"Legacy Capsule concrete category");

		AssertTaskPartition(LogisticsWorkCategory.Picking);
		AssertTaskPartition(LogisticsWorkCategory.Storing);
		AssertTaskPartition(LogisticsWorkCategory.CapsuleRelocate);
	}

	[Test]
	public void GetTaskCountSnapshot_UnassignedActiveTaskDoesNotUseWorkerAffiliation()
	{
		Building building = new("Worker Affiliation Task Metrics Building", new List<GridCell>(), CargoProcessStage.Picked);
		buildingManager.Register(building);
		ItemTransferTask task = CreateItemTransferTask(WorkerTask.TaskType.Picking, 0);
		HumanWorker worker = CreateWorker();
		worker.SetPrimaryBuildingId(building.RuntimeBuildingId);
		Assert.That(worker.SetTask(task), Is.True);
		worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
		taskManager.AddRestoredInProgressTask(task);

		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.Picking, building.RuntimeBuildingId),
			0,
			0,
			0,
			0,
			"Worker affiliation is not task ownership");
		AssertTaskSnapshot(
			metrics.GetTaskCountSnapshot(LogisticsWorkCategory.Picking, 0),
			0,
			0,
			1,
			1,
			"Unassigned active task");
		AssertTaskPartition(LogisticsWorkCategory.Picking);
	}

	[Test]
	public void TaskStateChanged_RaisesAfterSuccessfulLifecycleChangesOnly()
	{
		int eventCount = 0;
		taskManager.OnTaskStateChanged += () => ++eventCount;

		TestWorkerTask readyTask = new(WorkerTask.TaskType.PackingInput);
		taskManager.EnqueueTask(readyTask);
		Assert.That(eventCount, Is.EqualTo(1));

		taskManager.EnqueueTask(null);
		Assert.That(taskManager.InvalidateTask(readyTask), Is.True);
		Assert.That(taskManager.InvalidateTask(readyTask), Is.False);
		Assert.That(eventCount, Is.EqualTo(2));

		TestWorkerTask returnedTask = new(WorkerTask.TaskType.PackingInput);
		taskManager.AddRestoredReturnedTask(returnedTask);
		Assert.That(eventCount, Is.EqualTo(3));

		TestWorkerTask activeTask = new(WorkerTask.TaskType.PackingInput);
		HumanWorker activeWorker = AddActiveTask(activeTask, WorkerStatusAction.Working);
		Assert.That(eventCount, Is.EqualTo(4));
		Assert.That(taskManager.ReturnTask(activeWorker), Is.True);
		Assert.That(eventCount, Is.EqualTo(5));

		TestWorkerTask completingTask = new(WorkerTask.TaskType.PackingInput);
		AddActiveTask(completingTask, WorkerStatusAction.Working);
		Assert.That(eventCount, Is.EqualTo(6));
		taskManager.CompleteTask(completingTask);
		taskManager.CompleteTask(completingTask);
		Assert.That(eventCount, Is.EqualTo(7));

		taskManager.ResetRuntimeState();
		Assert.That(eventCount, Is.EqualTo(8));
	}

	[Test]
	public void TaskStateChanged_BatchesRestoreMutationsIntoFinalSnapshot()
	{
		int eventCount = 0;
		TaskCountSnapshot observedSnapshot = default;
		taskManager.OnTaskStateChanged += () =>
		{
			++eventCount;
			observedSnapshot = metrics.GetTaskCountSnapshot(WorkerTask.TaskType.Storing);
		};

		InvokeNonPublic(typeof(TaskManager), taskManager, "BeginTaskStateChangeBatch");
		taskManager.ResetRuntimeState();
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.Storing));
		taskManager.AddRestoredReturnedTask(new TestWorkerTask(WorkerTask.TaskType.Storing));
		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Storing),
			WorkerStatusAction.Working);

		Assert.That(eventCount, Is.Zero);
		InvokeNonPublic(typeof(TaskManager), taskManager, "EndTaskStateChangeBatch");

		Assert.That(eventCount, Is.EqualTo(1));
		Assert.That(observedSnapshot.Ready, Is.EqualTo(1));
		Assert.That(observedSnapshot.Returned, Is.EqualTo(1));
		Assert.That(observedSnapshot.Active, Is.EqualTo(1));
	}

	private HumanWorker AddActiveTask(WorkerTask task, WorkerStatusAction action)
	{
		HumanWorker worker = CreateWorker();
		Assert.That(worker.SetTask(task), Is.True);
		worker.SetWorkerAction(action);
		taskManager.AddRestoredInProgressTask(task);
		return worker;
	}

	private WorkerTask CreateLogisticsTask(LogisticsWorkCategory category, uint buildingId)
	{
		return category switch
		{
			LogisticsWorkCategory.Picking => CreateItemTransferTask(WorkerTask.TaskType.Picking, buildingId),
			LogisticsWorkCategory.Storing => CreateItemTransferTask(WorkerTask.TaskType.Storing, buildingId),
			LogisticsWorkCategory.PackingInput => CreateItemTransferTask(WorkerTask.TaskType.PackingInput, buildingId),
			LogisticsWorkCategory.Packing => CreatePackingTask(buildingId),
			LogisticsWorkCategory.PackingOutput => CreateItemTransferTask(WorkerTask.TaskType.PackingOutput, buildingId),
			LogisticsWorkCategory.CapsuleRelocate => new CapsuleRelocationTask(
				WorkerTask.TaskType.CargoTransfer,
				null,
				null,
				buildingId,
				CapsuleRelocationReason.StateMismatch),
			_ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
		};
	}

	private static ItemTransferTask CreateItemTransferTask(WorkerTask.TaskType taskType, uint buildingId)
	{
		return new ItemTransferTask(
			taskType,
			new ItemTransferJob(
				null,
				TransferObjectType.Item,
				TransferObjectType.Item,
				buildingId));
	}

	private PackingTask CreatePackingTask(uint buildingId)
	{
		PackingStation station = CreateGameObject(
			$"Task Metrics Packing Station {nextTaskObjectId++}",
			active: false).AddComponent<PackingStation>();
		if (buildingId != 0)
			facilityManager.RegisterFacility(buildingId, station);

		return new PackingTask(station);
	}

	private void AssertTaskPartition(LogisticsWorkCategory category)
	{
		TaskCountSnapshot all = metrics.GetTaskCountSnapshot(category);
		TaskCountSnapshot unassigned = metrics.GetTaskCountSnapshot(category, 0);
		int ready = unassigned.Ready;
		int returned = unassigned.Returned;
		int active = unassigned.Active;
		int blocked = unassigned.Blocked;

		for (int i = 0; i < buildingManager.RegisteredBuildings.Count; ++i)
		{
			Building building = buildingManager.RegisteredBuildings[i];
			if (building == null)
				continue;

			TaskCountSnapshot buildingTasks =
				metrics.GetTaskCountSnapshot(category, building.RuntimeBuildingId);
			ready += buildingTasks.Ready;
			returned += buildingTasks.Returned;
			active += buildingTasks.Active;
			blocked += buildingTasks.Blocked;
		}

		Assert.That(ready, Is.EqualTo(all.Ready), $"{category} Ready partition");
		Assert.That(returned, Is.EqualTo(all.Returned), $"{category} Returned partition");
		Assert.That(active, Is.EqualTo(all.Active), $"{category} Active partition");
		Assert.That(blocked, Is.EqualTo(all.Blocked), $"{category} Blocked partition");
	}

	private static void AssertTaskSnapshot(
		TaskCountSnapshot snapshot,
		int ready,
		int returned,
		int active,
		int blocked,
		string scope)
	{
		Assert.That(snapshot.Ready, Is.EqualTo(ready), $"{scope} Ready");
		Assert.That(snapshot.Returned, Is.EqualTo(returned), $"{scope} Returned");
		Assert.That(snapshot.Active, Is.EqualTo(active), $"{scope} Active");
		Assert.That(snapshot.Blocked, Is.EqualTo(blocked), $"{scope} Blocked");
		Assert.That(snapshot.Waiting, Is.EqualTo(ready + returned), $"{scope} Waiting");
		Assert.That(snapshot.Total, Is.EqualTo(ready + returned + active), $"{scope} Total");
	}

	private static CapsuleRelocationTask CreateCapsuleTask(WorkerTask.TaskType taskType)
	{
		return new CapsuleRelocationTask(
			taskType,
			null,
			null,
			0,
			CapsuleRelocationReason.StateMismatch);
	}

	private HumanWorker CreateWorker()
	{
		GameObject workerObject = CreateGameObject("Task Metrics Test Worker");
		GameObject slotObject = new("SlotRoot");
		slotObject.transform.SetParent(workerObject.transform, false);
		HumanWorker worker = workerObject.AddComponent<HumanWorker>();
		workerObject.AddComponent<CarryBoxAbility>();
		worker.OnPositionSet(
			new int3(nextWorkerPosition++, 0, nextWorkerPosition++),
			FacingDirection.North);
		return worker;
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

	private sealed class TestWorkerTask : WorkerTask
	{
		public TestWorkerTask(TaskType type) : base(type) { }

		public override string GetStatusSummary() => Type.ToString();

		protected override IBaseNode BuildWorkNode()
		{
			return new ActionNode(CompleteImmediately);
		}

		public override bool CheckTaskEnd() => false;

#if UNITY_EDITOR
		public override string ShowStatus() => Type.ToString();
#endif

		private static IBaseNode.NodeState CompleteImmediately(in BTContext context)
		{
			return IBaseNode.NodeState.Success;
		}
	}
}
