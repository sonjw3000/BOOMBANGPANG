using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Assets.Scripts.AI.BT;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CapsuleRelocationTaskEditModeTests
{
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private GameContext context;
	private TaskManager taskManager;
	private WorkerManager workerManager;
	private FacilityManager facilityManager;
	private BuildingManager buildingManager;
	private ProcessStatsCollector processStats;
	private OutboundWorkflowService outboundWorkflow;
	private RestFacilityService restFacilityService;
	private CapsuleDockService dockService;
	private CapsuleRelocateCoordinator coordinator;
	private uint nextBoxId;
	private int nextPosition;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		taskManager = CreateComponent<TaskManager>("Capsule Relocation Test Task Manager");
		workerManager = CreateComponent<WorkerManager>("Capsule Relocation Test Worker Manager");
		facilityManager = CreateComponent<FacilityManager>("Capsule Relocation Test Facility Manager");
		buildingManager = CreateComponent<BuildingManager>("Capsule Relocation Test Building Manager");
		processStats = CreateComponent<ProcessStatsCollector>("Capsule Relocation Test Process Stats");
		outboundWorkflow = CreateComponent<OutboundWorkflowService>("Capsule Relocation Test Outbound Workflow");
		restFacilityService = CreateComponent<RestFacilityService>("Capsule Relocation Test Rest Facility Service");

		// EditMode AddComponent normally invokes Awake. Explicit invocation also keeps this
		// fixture deterministic on runners that defer MonoBehaviour lifecycle callbacks.
		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");
		InvokeNonPublic(typeof(WorkerManager), workerManager, "Awake");
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");
		InvokeNonPublic(typeof(ProcessStatsCollector), processStats, "Awake");

		GameObject contextObject = CreateGameObject("Capsule Relocation Test Context", active: false);
		context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "facilityManager", facilityManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "processStats", processStats);
		SetPrivateField(typeof(GameContext), context, "outboundWorkflowService", outboundWorkflow);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		GameObject dockServiceObject = CreateGameObject("Capsule Relocation Test Dock Service", active: false);
		dockService = dockServiceObject.AddComponent<CapsuleDockService>();
		SetPrivateField(typeof(GameContext), context, "capsuleDockService", dockService);
		dockServiceObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<CapsuleDock>),
			dockService,
			"TryBindFacilityManager");

		coordinator = new CapsuleRelocateCoordinator(dockService);
		SetPrivateField(typeof(GameContext), context, "capsuleRelocateCoordinator", coordinator);
		nextBoxId = 1;
		nextPosition = 20;
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
	public void CompleteTask_NormalOutbound_ClearsBuildingMarkerAndCoordinatorOwnership()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Normal OB Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Normal OB Target");
		CargoCapsule capsule = CreateCapsule("Normal OB Capsule", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);

		Assert.That(CapsuleRelocationTask.PickCapsule(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));
		Assert.That(CapsuleRelocationTask.StoreCapsuleToTarget(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));
		Assert.That(task.CheckTaskEnd(), Is.True);
		task.EndTask();

		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Completed));
		Assert.That(worker.CurrentTask, Is.Null);
		Assert.That(target.DockedCapsule, Is.SameAs(capsule));
		AssertOutboundMarkerCleared(building, source);
		AssertCoordinatorOwnershipReleased(source, target);
	}

	[Test]
	public void SetSourceTarget_SourceLostBeforePickup_InvalidatesAndClearsOwnership()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Lost Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Lost Source Target");
		CargoCapsule capsule = CreateCapsule("Lost Source Capsule", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		Assert.That(source.TryUndockCapsule(out CargoCapsule removed), Is.True);
		Assert.That(removed, Is.SameAs(capsule));
		BTContext taskContext = CreateTaskContext(worker);

		Assert.That(CapsuleRelocationTask.SetSourceTarget(in taskContext), Is.EqualTo(IBaseNode.NodeState.Failure));
		Assert.That(task.CheckTaskEnd(), Is.True);
		task.EndTask();

		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(worker.CurrentTask, Is.Null);
		AssertOutboundMarkerCleared(building, source);
		AssertCoordinatorOwnershipReleased(source, target);
	}

	[Test]
	public void PickCapsule_CoordinatorHoldLost_ReevaluatesSource()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Ownership Lost Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Ownership Lost Target");
		CargoCapsule capsule = CreateCapsule("Ownership Lost Capsule", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		CapsuleRelocationTask originalTask = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, originalTask);
		HumanWorker worker = CreateWorker();
		AssignInProgress(originalTask, worker);
		BTContext taskContext = CreateTaskContext(worker);

		// The Building still owns this task marker, but the Coordinator has lost the
		// active-source reservation. Pick must invalidate rather than complete it.
		Assert.That(CapsuleRelocationTask.PickCapsule(in taskContext), Is.EqualTo(IBaseNode.NodeState.Failure));
		Assert.That(originalTask.CheckTaskEnd(), Is.True);
		originalTask.EndTask();

		Assert.That(originalTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		LinkedList<WorkerTask> queue = taskManager.TaskQueue[WorkerTask.TaskType.OB];
		Assert.That(queue.Count, Is.EqualTo(1), "source reevaluation must enqueue replacement OB work");
		CapsuleRelocationTask replacementTask = queue.OfType<CapsuleRelocationTask>().Single();
		Assert.That(replacementTask, Is.Not.SameAs(originalTask));
		AssertOutboundMarkerOwnedBy(building, source, replacementTask);
		Assert.That(coordinator.IsRelocationSourceActive(source), Is.True);
		Assert.That(coordinator.IsRelocationTargetActive(target), Is.True);
		Assert.That(coordinator.IsReserved(source), Is.True);
		Assert.That(coordinator.IsReserved(target), Is.True);
	}

	[Test]
	public void SetTargetDock_TemporarilyUnavailable_KeepsAssignedTask()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Waiting Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Waiting Target");
		CargoCapsule payload = CreateCapsule("Waiting Payload", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(payload), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);
		Assert.That(CapsuleRelocationTask.PickCapsule(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		CargoCapsule blocker = CreateCapsule("Waiting Target Blocker", CapsuleLogisticsState.OB);
		Assert.That(target.TryDockCapsule(blocker), Is.True);
		Assert.That(CapsuleRelocationTask.SetTargetDock(in taskContext), Is.EqualTo(IBaseNode.NodeState.Running));

		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Assigned));
		Assert.That(task.CheckTaskEnd(), Is.False);
		Assert.That(worker.CurrentTask, Is.SameAs(task));
		Assert.That(GetTaskTarget(task), Is.SameAs(target));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(payload));
	}

	[Test]
	public void PlayerPreemptsTarget_WithPayload_ReusesTaskAndSelectsReplacement()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Redirect Source", CapsuleDockState.IB);
		OutboundCargoPort originalTarget = CreateDock<OutboundCargoPort>(building, "Redirect Original Target");
		OutboundCargoPort replacementTarget = CreateDock<OutboundCargoPort>(building, "Redirect Replacement Target");
		CargoCapsule payload = CreateCapsule("Redirect Payload", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(payload), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, originalTarget);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, originalTarget, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);
		Assert.That(CapsuleRelocationTask.PickCapsule(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		Assert.That(coordinator.TryClaimForPlayer(originalTarget), Is.True);
		Assert.That(taskManager.TryPreemptCapsuleDockForPlayer(originalTarget), Is.True);
		Assert.That(GetTaskTarget(task), Is.Null);
		Assert.That(CapsuleRelocationTask.SetTargetDock(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Assigned));
		Assert.That(worker.CurrentTask, Is.SameAs(task));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(payload));
		Assert.That(GetTaskTarget(task), Is.SameAs(replacementTarget));
		Assert.That(coordinator.IsPlayerClaimed(originalTarget), Is.True);
		Assert.That(coordinator.IsRelocationTargetActive(replacementTarget), Is.True);
		Assert.That(coordinator.IsReserved(replacementTarget), Is.True);
		AssertOutboundMarkerOwnedBy(building, source, task);
	}

	[Test]
	public void CapsuleTransferRestore_EmptyRecoveryReference_IsTreatedAsAbsent()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Restore Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Restore Target");
		TaskSaveData saveData = new()
		{
			TaskType = WorkerTask.TaskType.OB,
			RecoveryBox = new BoxReferenceSaveData
			{
				BoxType = BoxType.None,
				BoxId = 0,
			},
			CapsuleTransfer = new CapsuleTransferTaskSaveData
			{
				HasTaskType = true,
				TaskType = WorkerTask.TaskType.OB,
				HasReason = true,
				Reason = CapsuleRelocationReason.DestinationNeedsCapsule,
				HasRouteKind = true,
				RouteKind = CargoRouteKind.Standard,
				BuildingId = building.RuntimeBuildingId,
				SourcePlaceableId = 1,
				TargetPlaceableId = 2,
			},
		};
		Dictionary<int, GameObject> restoredPlaceables = new()
		{
			[1] = source.gameObject,
			[2] = target.gameObject,
		};

		bool hasRecoveryReference = (bool)InvokeNonPublic(
			typeof(GameSaveService),
			null,
			"HasBoxReference",
			saveData.RecoveryBox);
		WorkerTask restoredTask = TaskSaveDataExtensions.Restore(saveData.CapsuleTransfer, restoredPlaceables);

		Assert.That(hasRecoveryReference, Is.False);
		Assert.That(restoredTask, Is.TypeOf<CapsuleRelocationTask>());
		taskManager.EnqueueTask(restoredTask);
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.OB].Contains(restoredTask), Is.True);
		Assert.That(restoredTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Ready));
	}

	[Test]
	public void DiscardRestoredTask_ClearsBuildingAndCoordinatorOwnership()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Discard Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Discard Target");
		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		processStats.AddQueue(WorkerTask.TaskType.OB, amount: 3);
		Assert.That(processStats.GetStats(WorkerTask.TaskType.OB).CurrentQueue, Is.EqualTo(3));

		bool discarded = (bool)InvokeNonPublic(
			typeof(TaskManager),
			taskManager,
			"DiscardRestoredTask",
			task,
			TaskInvalidationReason.RestoreInvalidReference);

		Assert.That(discarded, Is.True);
		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		AssertOutboundMarkerCleared(building, source);
		AssertCoordinatorOwnershipReleased(source, target);
		Assert.That(
			processStats.GetStats(WorkerTask.TaskType.OB).CurrentQueue,
			Is.EqualTo(3),
			"discarding an unregistered restored task must not decrement queue stats");
	}

	[Test]
	public void RestoreInvariant_CoordinatorOnlyMarker_RecoversSourceAndTargetOwnership()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Invariant Ghost Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Invariant Ghost Target");
		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);

		LogAssert.Expect(
			LogType.Error,
			new Regex(@"\[CapsuleRelocationInvariant\].*trigger=restore-test"));
		int violations = (int)InvokeNonPublic(
			typeof(Building),
			building,
			"ValidateCapsuleRelocationInvariants",
			"restore-test",
			true);

		Assert.That(violations, Is.EqualTo(1));
		AssertOutboundMarkerCleared(building, source);
		AssertCoordinatorOwnershipReleased(source, target);
	}

	[Test]
	public void RestoreInvariant_StaleMarker_DoesNotReleaseManagedRelocationSource()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Managed Invariant Source", CapsuleDockState.IB);
		OutboundCargoPort staleTarget = CreateDock<OutboundCargoPort>(building, "Stale Marker Target");
		CapsuleBuffer managedTarget = CreateBuffer(building, "Managed Relocation Target", CapsuleDockState.Empty);
		CapsuleRelocationTask staleTask = CreateOutboundTask(building, source, staleTarget);
		CapsuleRelocationTask managedTask = new(
			WorkerTask.TaskType.CapsuleClear,
			source,
			managedTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.StateMismatch);
		MarkBuildingTaskBuilt(building, staleTask);
		taskManager.EnqueueTask(managedTask);
		Assert.That(coordinator.RestoreActiveRelocation(source, managedTarget, payloadAlreadyPicked: false), Is.True);

		LogAssert.Expect(
			LogType.Error,
			new Regex(@"\[CapsuleRelocationInvariant\].*trigger=restore-managed-source-test"));
		int violations = (int)InvokeNonPublic(
			typeof(Building),
			building,
			"ValidateCapsuleRelocationInvariants",
			"restore-managed-source-test",
			true);

		Assert.That(violations, Is.EqualTo(1));
		AssertOutboundMarkerCleared(building, source);
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear].Contains(managedTask), Is.True);
		Assert.That(coordinator.IsRelocationSourceActive(source), Is.True);
		Assert.That(coordinator.IsReserved(source), Is.True);
		Assert.That(coordinator.IsRelocationTargetActive(managedTarget), Is.True);
		Assert.That(coordinator.IsReserved(managedTarget), Is.True);
		Assert.That(coordinator.IsRelocationTargetActive(staleTarget), Is.False);
		Assert.That(coordinator.IsReserved(staleTarget), Is.False);
	}

	[Test]
	public void RestoreInvariant_OrphanTargetCandidate_DoesNotReleaseManagedSourceReservation()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer sharedDock = CreateBuffer(building, "Shared Target And Source", CapsuleDockState.IB);
		CapsuleBuffer managedTarget = CreateBuffer(building, "Shared Source Managed Target", CapsuleDockState.Empty);
		CapsuleRelocationTask managedTask = new(
			WorkerTask.TaskType.CapsuleClear,
			sharedDock,
			managedTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.StateMismatch);
		taskManager.EnqueueTask(managedTask);
		Assert.That(coordinator.TryReserveActiveTarget(sharedDock), Is.True);
		Assert.That(coordinator.RestoreActiveRelocation(sharedDock, managedTarget, payloadAlreadyPicked: false), Is.True);

		InvokeNonPublic(
			typeof(Building),
			target: null,
			"TryReleaseOrphanedRelocationTarget",
			sharedDock,
			taskManager,
			coordinator);

		Assert.That(coordinator.IsRelocationTargetActive(sharedDock), Is.True);
		Assert.That(coordinator.IsRelocationSourceActive(sharedDock), Is.True);
		Assert.That(coordinator.IsReserved(sharedDock), Is.True);
		Assert.That(coordinator.IsRelocationTargetActive(managedTarget), Is.True);
		Assert.That(coordinator.IsReserved(managedTarget), Is.True);
	}

	private AlwaysOutboundReadyBuilding CreateBuilding()
	{
		AlwaysOutboundReadyBuilding building = new(
			"Capsule Relocation Test Building",
			new List<GridCell>());
		buildingManager.Register(building);
		Assert.That(building.RuntimeBuildingId, Is.Not.Zero);
		return building;
	}

	private CapsuleBuffer CreateBuffer(
		Building building,
		string objectName,
		CapsuleDockState dockState)
	{
		CapsuleBuffer buffer = CreateComponent<CapsuleBuffer>(objectName);
		buffer.SetDockState(dockState);
		return RegisterDock(building, buffer);
	}

	private T CreateDock<T>(Building building, string objectName) where T : CapsuleDock
	{
		T dock = CreateComponent<T>(objectName);
		return RegisterDock(building, dock);
	}

	private T RegisterDock<T>(Building building, T dock) where T : CapsuleDock
	{
		dock.OnPositionSet(new int3(nextPosition++, 0, 20), FacingDirection.North);
		facilityManager.RegisterFacility(building.RuntimeBuildingId, dock);
		Assert.That(
			facilityManager.TryGetBuildingId(dock, out uint registeredBuildingId),
			Is.True);
		Assert.That(registeredBuildingId, Is.EqualTo(building.RuntimeBuildingId));
		return dock;
	}

	private CargoCapsule CreateCapsule(string objectName, CapsuleLogisticsState logisticsState)
	{
		CargoCapsule capsule = CreateComponent<CargoCapsule>(objectName);
		SetPrivateField(typeof(BoxBase), capsule, "boxType", BoxType.Capsule);
		capsule.SetBoxId(nextBoxId++);
		InvokeNonPublic(typeof(BoxBase), capsule, "MarkValid");
		capsule.SetLogisticsState(logisticsState);
		return capsule;
	}

	private HumanWorker CreateWorker()
	{
		GameObject workerObject = CreateGameObject("Capsule Relocation Test Worker");
		GameObject slotObject = new("SlotRoot");
		slotObject.transform.SetParent(workerObject.transform, false);
		HumanWorker worker = workerObject.AddComponent<HumanWorker>();
		workerObject.AddComponent<CarryBoxAbility>();
		worker.OnPositionSet(new int3(10, 0, 10), FacingDirection.North);
		Assert.That(worker.CarryingAbility, Is.Not.Null);
		return worker;
	}

	private CapsuleRelocationTask CreateOutboundTask(
		Building building,
		CapsuleBuffer source,
		OutboundCargoPort target)
	{
		return new CapsuleRelocationTask(
			WorkerTask.TaskType.OB,
			source,
			target,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.DestinationNeedsCapsule);
	}

	private void AssignInProgress(CapsuleRelocationTask task, HumanWorker worker)
	{
		Assert.That(worker.SetTask(task), Is.True);
		taskManager.AddRestoredInProgressTask(task);
		Assert.That(worker.CurrentTask, Is.SameAs(task));
		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Assigned));
	}

	private static BTContext CreateTaskContext(AIWorker worker)
	{
		return new BTContext
		{
			Worker = worker,
			LocalBlackBoard = new BlackBoard(),
			GlobalBlackBoard = new BlackBoard(),
		};
	}

	private static void MarkBuildingTaskBuilt(Building building, CapsuleRelocationTask task)
	{
		InvokeNonPublic(typeof(Building), building, "OnCapsuleRelocationTaskBuilt", task);
		AssertOutboundMarkerOwnedBy(building, (CapsuleBuffer)GetTaskSource(task), task);
	}

	private static CapsuleDock GetTaskSource(CapsuleRelocationTask task)
	{
		return (CapsuleDock)GetPrivateField(typeof(CapsuleRelocationTask), task, "sourceDock");
	}

	private static CapsuleDock GetTaskTarget(CapsuleRelocationTask task)
	{
		return (CapsuleDock)GetPrivateField(typeof(CapsuleRelocationTask), task, "targetDock");
	}

	private static void AssertOutboundMarkerOwnedBy(
		Building building,
		CapsuleBuffer source,
		CapsuleRelocationTask expectedOwner)
	{
		HashSet<CapsuleBuffer> queuedBuffers = (HashSet<CapsuleBuffer>)GetPrivateField(
			typeof(Building),
			building,
			"queuedOutboundBuffers");
		Dictionary<CapsuleBuffer, CapsuleRelocationTask> owners =
			(Dictionary<CapsuleBuffer, CapsuleRelocationTask>)GetPrivateField(
				typeof(Building),
				building,
				"queuedOutboundTaskOwners");

		Assert.That(queuedBuffers.Contains(source), Is.True);
		Assert.That(owners.TryGetValue(source, out CapsuleRelocationTask owner), Is.True);
		Assert.That(owner, Is.SameAs(expectedOwner));
	}

	private static void AssertOutboundMarkerCleared(Building building, CapsuleBuffer source)
	{
		HashSet<CapsuleBuffer> queuedBuffers = (HashSet<CapsuleBuffer>)GetPrivateField(
			typeof(Building),
			building,
			"queuedOutboundBuffers");
		Dictionary<CapsuleBuffer, CapsuleRelocationTask> owners =
			(Dictionary<CapsuleBuffer, CapsuleRelocationTask>)GetPrivateField(
				typeof(Building),
				building,
				"queuedOutboundTaskOwners");

		Assert.That(queuedBuffers.Contains(source), Is.False);
		Assert.That(owners.ContainsKey(source), Is.False);
	}

	private void AssertCoordinatorOwnershipReleased(CapsuleDock source, CapsuleDock target)
	{
		Assert.That(coordinator.IsReserved(source), Is.False);
		Assert.That(coordinator.IsReserved(target), Is.False);
		Assert.That(coordinator.IsRelocationSourceActive(source), Is.False);
		Assert.That(coordinator.IsRelocationTargetActive(target), Is.False);
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

	private sealed class AlwaysOutboundReadyBuilding : Building
	{
		public AlwaysOutboundReadyBuilding(string displayName, List<GridCell> occupiedCells)
			: base(displayName, occupiedCells, BuildingType.Staging)
		{
		}

		protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
		{
			return capsuleBuffer?.DockedCapsule?.LogisticsState == CapsuleLogisticsState.OB;
		}
	}
}
