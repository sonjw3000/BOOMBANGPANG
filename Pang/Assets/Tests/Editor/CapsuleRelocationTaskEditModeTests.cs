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
	private CapsuleBufferService bufferService;
	private FacilityRuleManager facilityRuleManager;
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
		GameObject ruleManagerObject = CreateGameObject("Capsule Relocation Test Rule Manager", active: false);
		facilityRuleManager = ruleManagerObject.AddComponent<FacilityRuleManager>();
		SetPrivateField(typeof(GameContext), context, "facilityRuleManager", facilityRuleManager);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		GameObject dockServiceObject = CreateGameObject("Capsule Relocation Test Dock Service", active: false);
		dockService = dockServiceObject.AddComponent<CapsuleDockService>();
		SetPrivateField(typeof(GameContext), context, "capsuleDockService", dockService);
		dockServiceObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<CapsuleDock>),
			dockService,
			"TryBindFacilityManager");
		GameObject bufferServiceObject = CreateGameObject("Capsule Relocation Test Buffer Service", active: false);
		bufferService = bufferServiceObject.AddComponent<CapsuleBufferService>();
		SetPrivateField(typeof(GameContext), context, "capsuleBufferService", bufferService);
		ruleManagerObject.SetActive(true);
		bufferServiceObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<CapsuleBuffer>),
			bufferService,
			"TryBindFacilityManager");

		coordinator = new CapsuleRelocateCoordinator(
			dockService,
			bufferService: bufferService,
			evaluateDirtyDock: dock =>
			{
				if (facilityManager.TryGetBuildingId(dock, out uint buildingId) &&
					buildingManager.TryGetBuilding(buildingId, out Building building))
				{
					InvokeNonPublic(
						typeof(Building),
						building,
						"ReevaluateCapsuleDockAvailability",
						dock);
				}
			},
			evaluateDirtyBuilding: buildingId =>
			{
				if (buildingManager.TryGetBuilding(buildingId, out Building building))
					InvokeNonPublic(typeof(Building), building, "ReevaluateCapsuleRouting");
			});
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
	public void SetSourceTarget_EmptyRocketCapsule_IsValidUnloadingSource()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		Rocket source = CreateDock<Rocket>(building, "Empty Rocket Source");
		InboundCargoPort target = CreateDock<InboundCargoPort>(building, "Empty Rocket Target");
		CargoCapsule capsule = CreateCapsule("Empty Rocket Capsule", CapsuleLogisticsState.IB);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		Assert.That(
			(CapsuleLogisticsState)InvokeNonPublic(
				typeof(Rocket),
				source,
				"RefreshPayloadLogisticsState"),
			Is.EqualTo(CapsuleLogisticsState.Empty));

		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.Unloading,
			source,
			target,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.SourceMustClear);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);

		Assert.That(CapsuleRelocationTask.SetSourceTarget(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));
		Assert.That(capsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Empty));
	}

	[Test]
	public void SetSourceTarget_RuleTargetChangedBeforePickup_SelectsNewMatchingBuffer()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		InboundCargoPort source = CreateDock<InboundCargoPort>(building, "Rule Revalidation Source");
		CapsuleBuffer previousTarget = CreateBuffer(building, "Previous Rule Target", CapsuleDockState.IB);
		CapsuleBuffer replacementTarget = CreateBuffer(building, "Replacement Rule Target", CapsuleDockState.OBStandby);
		CargoCapsule capsule = CreateCapsule("Rule Revalidation Capsule", CapsuleLogisticsState.IB);
		AddCargo(capsule, 902, 2, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		ApplyBufferRule(previousTarget, CargoProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, CargoProcessStage.Labeled);

		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.IB,
			source,
			previousTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		Assert.That(coordinator.RestoreActiveRelocation(source, previousTarget, payloadAlreadyPicked: false), Is.True);
		ApplyBufferRule(previousTarget, CargoProcessStage.Packed);

		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);

		Assert.That(CapsuleRelocationTask.SetSourceTarget(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));
		Assert.That(GetTaskTarget(task), Is.SameAs(replacementTarget));
		Assert.That(coordinator.IsRelocationTargetActive(previousTarget), Is.False);
		Assert.That(coordinator.IsRelocationTargetActive(replacementTarget), Is.True);
	}

	[Test]
	public void StoreCapsuleToTarget_RuleTargetChangedAfterWork_RestartsMovementBeforePlacement()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		InboundCargoPort source = CreateDock<InboundCargoPort>(building, "Rule Store Revalidation Source");
		CapsuleBuffer previousTarget = CreateBuffer(building, "Rule Store Previous Target", CapsuleDockState.IB);
		CapsuleBuffer replacementTarget = CreateBuffer(building, "Rule Store Replacement Target", CapsuleDockState.OBStandby);
		CargoCapsule capsule = CreateCapsule("Rule Store Revalidation Capsule", CapsuleLogisticsState.IB);
		AddCargo(capsule, 904, 2, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		ApplyBufferRule(previousTarget, CargoProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, CargoProcessStage.Labeled);

		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.IB,
			source,
			previousTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);
		Assert.That(source.TryUndockCapsule(out CargoCapsule pickedCapsule), Is.True);
		Assert.That(pickedCapsule, Is.SameAs(capsule));
		Assert.That(worker.CarryingAbility.PutBox(capsule), Is.True);
		Assert.That(coordinator.RestoreActiveRelocation(source, previousTarget, payloadAlreadyPicked: true), Is.True);
		Assert.That(CapsuleRelocationTask.SetTargetDock(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		ApplyBufferRule(previousTarget, CargoProcessStage.Packed);

		Assert.That(CapsuleRelocationTask.StoreCapsuleToTarget(in taskContext), Is.EqualTo(IBaseNode.NodeState.Running));
		Assert.That(GetTaskTarget(task), Is.SameAs(replacementTarget));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(capsule));
		Assert.That(previousTarget.DockedCapsule, Is.Null);
		Assert.That(replacementTarget.DockedCapsule, Is.Null, "replacement must not receive the capsule before movement is rerun");
		Assert.That(coordinator.IsRelocationTargetActive(previousTarget), Is.False);
		Assert.That(coordinator.IsRelocationTargetActive(replacementTarget), Is.True);
	}

	[Test]
	public void ProcessDirty_RuleChangedForReturnedTask_InvalidatesAndRematchesStaleAssignment()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		InboundCargoPort source = CreateDock<InboundCargoPort>(building, "Returned Rule Source");
		CapsuleBuffer staleTarget = CreateBuffer(building, "Returned Rule Stale Target", CapsuleDockState.Empty);
		CapsuleBuffer replacementTarget = CreateBuffer(building, "Returned Rule Replacement Target", CapsuleDockState.OBStandby);
		CargoCapsule capsule = CreateCapsule("Returned Rule Capsule", CapsuleLogisticsState.IB);
		AddCargo(capsule, 905, 2, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		ApplyBufferRule(staleTarget, CargoProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, CargoProcessStage.Labeled);

		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.IB,
			source,
			staleTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		Assert.That(coordinator.RestoreActiveRelocation(source, staleTarget, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		Assert.That(taskManager.ReturnTask(worker), Is.True);
		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Returned));

		ApplyBufferRule(staleTarget, CargoProcessStage.Packed);
		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();

		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(source.DockedCapsule, Is.SameAs(capsule));
		Assert.That(coordinator.IsRelocationSourceActive(source), Is.True);
		Assert.That(coordinator.IsRelocationTargetActive(staleTarget), Is.False);
		Assert.That(coordinator.IsReserved(staleTarget), Is.False);
		Assert.That(coordinator.IsReserved(source), Is.True);
		CapsuleRelocationTask replacementTask = taskManager.TaskQueue[WorkerTask.TaskType.IB]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(replacementTask), Is.SameAs(source));
		Assert.That(GetTaskTarget(replacementTask), Is.SameAs(replacementTarget));
	}

	[Test]
	public void ProcessDirty_RuleChangedForReturnedTask_SourceNowMatches_InvalidatesWithoutReplacement()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Returned Matched Source", CapsuleDockState.IB);
		CapsuleBuffer target = CreateBuffer(building, "Returned Matched Target", CapsuleDockState.Empty);
		CargoCapsule capsule = CreateCapsule("Returned Matched Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 906, 2, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		ApplyBufferRule(source, CargoProcessStage.Packed);
		ApplyBufferRule(target, CargoProcessStage.Labeled);

		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.CapsuleClear,
			source,
			target,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		Assert.That(taskManager.ReturnTask(worker), Is.True);

		ApplyBufferRule(source, CargoProcessStage.Labeled);
		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();

		Assert.That(task.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(source.DockedCapsule, Is.SameAs(capsule));
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear], Is.Empty);
		AssertCoordinatorOwnershipReleased(source, target);
	}

	[Test]
	public void ProcessDirty_RuleChangedForReadyTask_InvalidatesAndRematchesImmediately()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		InboundCargoPort source = CreateDock<InboundCargoPort>(building, "Queued Rule Source");
		CapsuleBuffer staleTarget = CreateBuffer(building, "Queued Stale Target", CapsuleDockState.Empty);
		CapsuleBuffer replacementTarget = CreateBuffer(building, "Queued Replacement Target", CapsuleDockState.OBStandby);
		CargoCapsule capsule = CreateCapsule("Queued Rule Capsule", CapsuleLogisticsState.IB);
		AddCargo(capsule, 903, 2, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		ApplyBufferRule(staleTarget, CargoProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, CargoProcessStage.Labeled);

		CapsuleRelocationTask staleTask = new(
			WorkerTask.TaskType.IB,
			source,
			staleTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		taskManager.EnqueueTask(staleTask);
		Assert.That(coordinator.RestoreActiveRelocation(source, staleTarget, payloadAlreadyPicked: false), Is.True);
		ApplyBufferRule(staleTarget, CargoProcessStage.Packed);

		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();

		Assert.That(staleTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(coordinator.IsRelocationTargetActive(staleTarget), Is.False);
		CapsuleRelocationTask replacementTask = taskManager.TaskQueue[WorkerTask.TaskType.IB]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(replacementTask), Is.SameAs(source));
		Assert.That(GetTaskTarget(replacementTask), Is.SameAs(replacementTarget));
		Assert.That(coordinator.IsRelocationTargetActive(replacementTarget), Is.True);
	}

	[Test]
	public void PickCapsule_CoordinatorHoldLost_ReevaluatesSource()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Ownership Lost Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Ownership Lost Target");
		CargoCapsule capsule = CreateCapsule("Ownership Lost Capsule", CapsuleLogisticsState.OB);
		AddCargo(capsule, 901, 1, ItemStatus.Labeled);
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
		coordinator.ProcessDirty();

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
	public void SetTargetDock_NonLaunchOutbound_DoesNotApplyLaunchManifestValidation()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Non-Launch OB Source", CapsuleDockState.OBStandby);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Non-Launch OB Target");
		CargoCapsule payload = CreateCapsule("Non-Launch OB Payload", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(payload), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		Assert.That(coordinator.RestoreActiveRelocation(source, target, payloadAlreadyPicked: false), Is.True);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);
		Assert.That(CapsuleRelocationTask.PickCapsule(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		Assert.That(CapsuleRelocationTask.SetTargetDock(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		Assert.That(GetTaskTarget(task), Is.SameAs(target));
		Assert.That(payload.LogisticsState, Is.EqualTo(CapsuleLogisticsState.OB));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(payload));
	}

	[Test]
	public void SetTargetDock_LaunchOutboundWithIncompleteManifest_RuleOnlySourceRedirectsToSource()
	{
		LaunchBuilding building = CreateLaunchBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Launch OB Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Launch OB Target");
		CargoCapsule payload = CreateCapsule("Launch OB Payload", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(payload), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
		MarkBuildingTaskBuilt(building, task);
		HumanWorker worker = CreateWorker();
		AssignInProgress(task, worker);
		BTContext taskContext = CreateTaskContext(worker);
		Assert.That(source.TryUndockCapsule(out CargoCapsule pickedPayload), Is.True);
		Assert.That(pickedPayload, Is.SameAs(payload));
		Assert.That(worker.CarryingAbility.PutBox(payload), Is.True);
		Assert.That(
			coordinator.RestoreActiveRelocation(
				source,
				target,
				payloadAlreadyPicked: true,
				holdSourceForPotentialReturn: true),
			Is.True);
		LogAssert.Expect(
			LogType.Log,
			new Regex(@"\[OutboundQualityControl\] Redirecting rejected capsule"));

		Assert.That(CapsuleRelocationTask.SetTargetDock(in taskContext), Is.EqualTo(IBaseNode.NodeState.Success));

		Assert.That(GetTaskTarget(task), Is.SameAs(source));
		Assert.That(payload.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Inside));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(payload));
	}

	[Test]
	public void ProcessDirty_NormalizesBufferCapsulesFromPhysicalContent()
	{
		NeverOutboundReadyBuilding building = new(
			"Capsule State Normalization Building",
			new List<GridCell>());
		buildingManager.Register(building);

		CapsuleBuffer insideBuffer = CreateBuffer(building, "Inside Normalization Buffer", CapsuleDockState.IB);
		CargoCapsule insideCapsule = CreateCapsule("Inside Normalization Capsule", CapsuleLogisticsState.IB);
		AddCargo(insideCapsule, 911, 2, ItemStatus.Labeled);
		Assert.That(insideBuffer.TryDockCapsule(insideCapsule), Is.True);

		CapsuleBuffer emptyBuffer = CreateBuffer(building, "Empty Normalization Buffer", CapsuleDockState.Empty);
		CargoCapsule emptyCapsule = CreateCapsule("Empty Normalization Capsule", CapsuleLogisticsState.Inside);
		Assert.That(emptyBuffer.TryDockCapsule(emptyCapsule), Is.True);

		coordinator.MarkDirty(insideBuffer);
		coordinator.MarkDirty(emptyBuffer);
		coordinator.ProcessDirty();

		Assert.That(insideCapsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Inside));
		Assert.That(emptyCapsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Empty));
	}

	[Test]
	public void ProcessDirty_OutboundReadyContentPromotesToObAndQueuesDispatch()
	{
		ContentOutboundReadyBuilding building = new(
			"Capsule OB Promotion Building",
			new List<GridCell>());
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(building, "OB Promotion Source", CapsuleDockState.OBStandby);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "OB Promotion Target");
		CargoCapsule capsule = CreateCapsule("OB Promotion Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 912, 3, ItemStatus.Packed);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();

		Assert.That(capsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.OB));
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.OB].Count, Is.EqualTo(1));
		CapsuleRelocationTask task = taskManager.TaskQueue[WorkerTask.TaskType.OB]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(task), Is.SameAs(source));
		Assert.That(GetTaskTarget(task), Is.SameAs(target));
	}

	[Test]
	public void ProcessDirty_ReadyOutboundNoLongerEligible_InvalidatesBeforeDispatch()
	{
		ToggleOutboundReadyBuilding building = new(
			"Capsule OB Demotion Building",
			new List<GridCell>())
		{
			OutboundReady = true,
		};
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(building, "OB Demotion Source", CapsuleDockState.OBStandby);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "OB Demotion Target");
		CargoCapsule capsule = CreateCapsule("OB Demotion Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 913, 3, ItemStatus.Packed);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();
		CapsuleRelocationTask staleTask = taskManager.TaskQueue[WorkerTask.TaskType.OB]
			.OfType<CapsuleRelocationTask>()
			.Single();

		building.OutboundReady = false;
		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();

		Assert.That(capsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Inside));
		Assert.That(staleTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.OB], Is.Empty);
		AssertCoordinatorOwnershipReleased(source, target);
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

	private LaunchBuilding CreateLaunchBuilding()
	{
		LaunchBuilding building = new(
			"Capsule Relocation Launch Test Building",
			new List<GridCell>());
		buildingManager.Register(building);
		Assert.That(building.RuntimeBuildingId, Is.Not.Zero);
		return building;
	}

	private StorageBuilding CreateStorageBuilding()
	{
		StorageBuilding building = new(
			"Capsule Relocation Storage Test Building",
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

	private static void AddCargo(
		CargoCapsule capsule,
		uint itemId,
		int quantity,
		ItemStatus status)
	{
		ItemStack stack = new(itemId, status: status);
		Assert.That(stack.AddItem(quantity), Is.EqualTo(quantity));
		List<ItemStack> stacks =
			(List<ItemStack>)GetPrivateField(typeof(BoxBase), capsule, "stacks");
		Dictionary<uint, int> totals =
			(Dictionary<uint, int>)GetPrivateField(typeof(BoxBase), capsule, "itemTotals");
		stacks.Add(stack);
		totals[itemId] = totals.GetValueOrDefault(itemId) + quantity;
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

	private void ApplyBufferRule(CapsuleBuffer buffer, CargoProcessStage stage)
	{
		FacilityRule rule = new();
		rule.SetRequiredCapsuleBufferState(CapsuleBufferStateRequirement.Inside);
		rule.SetRequiredCargoProcessStage(stage);
		FacilityRulePreset preset = facilityRuleManager.CreatePreset($"{buffer.name} {stage}", rule);
		Assert.That(facilityRuleManager.ApplyPreset(buffer, preset.Id), Is.True);
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

	private sealed class NeverOutboundReadyBuilding : Building
	{
		public NeverOutboundReadyBuilding(string displayName, List<GridCell> occupiedCells)
			: base(displayName, occupiedCells, BuildingType.Generic)
		{
		}

		protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
		{
			return false;
		}
	}

	private sealed class ContentOutboundReadyBuilding : Building
	{
		public ContentOutboundReadyBuilding(string displayName, List<GridCell> occupiedCells)
			: base(displayName, occupiedCells, BuildingType.Generic)
		{
		}

		protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
		{
			return capsuleBuffer != null && capsuleBuffer.IsCapsuleEmpty() == false;
		}
	}

	private sealed class ToggleOutboundReadyBuilding : Building
	{
		public bool OutboundReady { get; set; }

		public ToggleOutboundReadyBuilding(string displayName, List<GridCell> occupiedCells)
			: base(displayName, occupiedCells, BuildingType.Generic)
		{
		}

		protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
		{
			return OutboundReady && capsuleBuffer?.DockedCapsule != null;
		}
	}
}
