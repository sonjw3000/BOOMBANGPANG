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
	private InboundWorkflowService inboundWorkflow;
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
		GameObject inboundWorkflowObject = CreateGameObject(
			"Capsule Relocation Test Inbound Workflow",
			active: false);
		inboundWorkflow = inboundWorkflowObject.AddComponent<InboundWorkflowService>();
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
		SetPrivateField(typeof(GameContext), context, "inboundWorkflowService", inboundWorkflow);
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
			taskManager: taskManager,
			buildingManager: buildingManager,
			facilityManager: facilityManager);
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
	public void CompleteTask_NormalOutbound_ClearsCoordinatorOwnership()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Normal OB Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Normal OB Target");
		CargoCapsule capsule = CreateCapsule("Normal OB Capsule", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
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
		AssertCoordinatorOwnershipReleased(source, target);
	}

	[Test]
	public void CanDispatchTo_StageSpecificEndpointRules_EvaluatesOnlyWorkerRule()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		InboundCargoPort source = CreateDock<InboundCargoPort>(building, "Stage Rule Dispatch Source");
		CapsuleBuffer target = CreateBuffer(building, "Stage Rule Dispatch Target", CapsuleDockState.Empty);
		ApplyFacilityRule(source, ItemProcessStage.Unlabeled);
		ApplyFacilityRule(target, ItemProcessStage.Unlabeled);
		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.IB,
			source,
			target,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		HumanWorker worker = CreateWorker();
		worker.SetPrimaryBuildingId(building.RuntimeBuildingId);

		Assert.That(
			FacilityFilter.ForWorker(worker).MatchesCurrentRules(source),
			Is.False,
			"The full Facility filter must remain stage-aware for cargo queries.");
		Assert.That(task.CanDispatchTo(worker), Is.True);
	}

	[Test]
	public void CanDispatchTo_WorkerRestrictedEndpoint_StillRejectsWorker()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		InboundCargoPort source = CreateDock<InboundCargoPort>(building, "Worker Rule Dispatch Source");
		CapsuleBuffer target = CreateBuffer(building, "Worker Rule Dispatch Target", CapsuleDockState.Empty);
		ApplyFacilityRule(source, ItemProcessStage.Unlabeled, WorkerKind.Robot);
		ApplyFacilityRule(target, ItemProcessStage.Unlabeled);
		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.IB,
			source,
			target,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		HumanWorker worker = CreateWorker();
		worker.SetPrimaryBuildingId(building.RuntimeBuildingId);

		Assert.That(task.CanDispatchTo(worker), Is.False);
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
		ApplyBufferRule(previousTarget, ItemProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, ItemProcessStage.Labeled);

		CapsuleRelocationTask task = new(
			WorkerTask.TaskType.IB,
			source,
			previousTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		Assert.That(coordinator.RestoreActiveRelocation(source, previousTarget, payloadAlreadyPicked: false), Is.True);
		ApplyBufferRule(previousTarget, ItemProcessStage.Packed);

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
		ApplyBufferRule(previousTarget, ItemProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, ItemProcessStage.Labeled);

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

		ApplyBufferRule(previousTarget, ItemProcessStage.Packed);

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
		ApplyBufferRule(staleTarget, ItemProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, ItemProcessStage.Labeled);

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

		ApplyBufferRule(staleTarget, ItemProcessStage.Packed);
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
		ApplyBufferRule(source, ItemProcessStage.Packed);
		ApplyBufferRule(target, ItemProcessStage.Labeled);

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

		ApplyBufferRule(source, ItemProcessStage.Labeled);
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
		ApplyBufferRule(staleTarget, ItemProcessStage.Labeled);
		ApplyBufferRule(replacementTarget, ItemProcessStage.Labeled);

		CapsuleRelocationTask staleTask = new(
			WorkerTask.TaskType.IB,
			source,
			staleTarget,
			building.RuntimeBuildingId,
			CapsuleRelocationReason.RuleRouting);
		taskManager.EnqueueTask(staleTask);
		Assert.That(coordinator.RestoreActiveRelocation(source, staleTarget, payloadAlreadyPicked: false), Is.True);
		ApplyBufferRule(staleTarget, ItemProcessStage.Packed);

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
		HumanWorker worker = CreateWorker();
		AssignInProgress(originalTask, worker);
		BTContext taskContext = CreateTaskContext(worker);

		// The task has lost the Coordinator's active-source reservation. Pick must
		// invalidate rather than complete it, then dirty evaluation recreates the work.
		Assert.That(CapsuleRelocationTask.PickCapsule(in taskContext), Is.EqualTo(IBaseNode.NodeState.Failure));
		Assert.That(originalTask.CheckTaskEnd(), Is.True);
		originalTask.EndTask();
		coordinator.ProcessDirty();

		Assert.That(originalTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		LinkedList<WorkerTask> queue = taskManager.TaskQueue[WorkerTask.TaskType.OB];
		Assert.That(queue.Count, Is.EqualTo(1), "source reevaluation must enqueue replacement OB work");
		CapsuleRelocationTask replacementTask = queue.OfType<CapsuleRelocationTask>().Single();
		Assert.That(replacementTask, Is.Not.SameAs(originalTask));
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
		Building building = CreateLaunchBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Launch OB Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Launch OB Target");
		CargoCapsule payload = CreateCapsule("Launch OB Payload", CapsuleLogisticsState.OB);
		Assert.That(source.TryDockCapsule(payload), Is.True);

		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
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
	public void ProcessDirty_ItemStageChanged_RelocatesToMatchingRuleExactlyOnce()
	{
		NeverOutboundReadyBuilding building = new(
			"Rule Stage Change Building",
			new List<GridCell>());
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(building, "Unlabeled Rule Source", CapsuleDockState.IB);
		CapsuleBuffer target = CreateBuffer(building, "Labeled Rule Target", CapsuleDockState.Empty);
		Assert.That(
			buildingManager.TryRegisterFacility(building.RuntimeBuildingId, source),
			Is.True);
		Assert.That(
			buildingManager.TryRegisterFacility(building.RuntimeBuildingId, target),
			Is.True);
		ApplyBufferRule(source, ItemProcessStage.Unlabeled);
		ApplyBufferRule(target, ItemProcessStage.Labeled);
		CargoCapsule capsule = CreateCapsule("Rule Stage Change Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 914, 3, ItemStatus.None);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		buildingManager.RefreshItemContainerState(source);
		coordinator.ProcessDirty();

		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear], Is.Empty);
		Assert.That(coordinator.PendingSendCount, Is.Zero);

		source.Stacks.Single().SetStatus(ItemStatus.Labeled);
		buildingManager.RefreshItemContainerState(source);
		coordinator.ProcessDirty();

		CapsuleRelocationTask relocation = taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(relocation), Is.SameAs(source));
		Assert.That(GetTaskTarget(relocation), Is.SameAs(target));
		Assert.That(
			(CapsuleRelocationReason)GetPrivateField(
				typeof(CapsuleRelocationTask),
				relocation,
				"reason"),
			Is.EqualTo(CapsuleRelocationReason.RuleRouting));

		buildingManager.RefreshItemContainerState(source);
		coordinator.ProcessDirty();

		Assert.That(
			taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear]
				.OfType<CapsuleRelocationTask>()
				.Count(),
			Is.EqualTo(1));
	}

	[Test]
	public void ProcessDirty_DockTaskDefersRuleRelocationUntilDependencyEnds()
	{
		NeverOutboundReadyBuilding building = new(
			"Rule Dependency Building",
			new List<GridCell>());
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(building, "Busy Rule Source", CapsuleDockState.IB);
		CapsuleBuffer target = CreateBuffer(building, "Busy Rule Target", CapsuleDockState.Empty);
		Assert.That(
			buildingManager.TryRegisterFacility(building.RuntimeBuildingId, source),
			Is.True);
		Assert.That(
			buildingManager.TryRegisterFacility(building.RuntimeBuildingId, target),
			Is.True);
		ApplyBufferRule(source, ItemProcessStage.Unlabeled);
		ApplyBufferRule(target, ItemProcessStage.Labeled);
		CargoCapsule capsule = CreateCapsule("Busy Rule Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 915, 2, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		FacilityDependentTestTask blocker = new(source);
		taskManager.EnqueueTask(blocker);

		buildingManager.RefreshItemContainerState(source);
		coordinator.ProcessDirty();

		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear], Is.Empty);
		Assert.That(coordinator.PendingSendCount, Is.EqualTo(1));
		Assert.That(coordinator.IsReserved(source), Is.False);
		Assert.That(coordinator.IsReserved(target), Is.False);

		Assert.That(
			taskManager.InvalidateTask(blocker, TaskInvalidationReason.DispatchInvalid),
			Is.True);

		CapsuleRelocationTask relocation = taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(relocation), Is.SameAs(source));
		Assert.That(GetTaskTarget(relocation), Is.SameAs(target));
		Assert.That(coordinator.PendingSendCount, Is.Zero);

		buildingManager.RefreshItemContainerState(source);
		coordinator.ProcessDirty();
		Assert.That(
			taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear]
				.OfType<CapsuleRelocationTask>()
				.Count(),
			Is.EqualTo(1));
	}

	[Test]
	public void LabelingTask_CompletionRelocatesToLabeledRuleWithoutManualDirty()
	{
		ActivateInboundWorkflow();
		Building building = new(
			"Rule Labeling Completion Building",
			new List<GridCell>(),
			ItemProcessStage.Labeled);
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(building, "Labeling Rule Source", CapsuleDockState.IB);
		CapsuleBuffer target = CreateBuffer(building, "Labeled Rule Destination", CapsuleDockState.Empty);
		Assert.That(
			buildingManager.TryRegisterFacility(building.RuntimeBuildingId, source),
			Is.True);
		Assert.That(
			buildingManager.TryRegisterFacility(building.RuntimeBuildingId, target),
			Is.True);
		ApplyBufferRule(source, ItemProcessStage.Unlabeled);
		ApplyBufferRule(target, ItemProcessStage.Labeled);
		CargoCapsule capsule = CreateCapsule("Labeling Rule Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 916, 2, ItemStatus.None);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		coordinator.ProcessDirty();
		InvokeNonPublic(typeof(TaskManager), taskManager, "ProcessTaskBuildQueue");
		LabelingTask labelingTask = taskManager.TaskQueue[WorkerTask.TaskType.Labeling]
			.OfType<LabelingTask>()
			.Single();
		taskManager.TaskQueue[WorkerTask.TaskType.Labeling].Remove(labelingTask);
		HumanWorker worker = CreateWorker();
		Assert.That(worker.SetTask(labelingTask), Is.True);
		taskManager.AddRestoredInProgressTask(labelingTask);
		BTContext taskContext = CreateTaskContext(worker);

		Assert.That(
			LabelingTask.ApplyLabel(in taskContext),
			Is.EqualTo(IBaseNode.NodeState.Success));
		Assert.That(source.Stacks.Single().Status, Is.EqualTo(ItemStatus.Labeled));
		coordinator.ProcessDirty();

		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear], Is.Empty);
		Assert.That(coordinator.PendingSendCount, Is.EqualTo(1));

		Assert.That(labelingTask.CheckTaskEnd(), Is.True);
		labelingTask.EndTask();

		CapsuleRelocationTask relocation = taskManager.TaskQueue[WorkerTask.TaskType.CapsuleClear]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(relocation), Is.SameAs(source));
		Assert.That(GetTaskTarget(relocation), Is.SameAs(target));
		Assert.That(coordinator.PendingSendCount, Is.Zero);
	}

	[Test]
	public void LabelingTask_OutboundPromotionInvalidatesReadyTaskBeforeDispatch()
	{
		ActivateInboundWorkflow();
		Building building = new(
			"Generic Labeling Outbound Promotion Building",
			new List<GridCell>(),
			ItemProcessStage.Any);
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(building, "Generic Labeling Outbound Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Generic Labeling Outbound Target");
		Assert.That(buildingManager.TryRegisterFacility(building.RuntimeBuildingId, source), Is.True);
		ApplyBufferRule(source, ItemProcessStage.Unlabeled);
		CargoCapsule capsule = CreateCapsule("Generic Labeling Outbound Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 917, 2, ItemStatus.None);
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		coordinator.ProcessDirty();
		InvokeNonPublic(typeof(TaskManager), taskManager, "ProcessTaskBuildQueue");
		LabelingTask labelingTask = taskManager.TaskQueue[WorkerTask.TaskType.Labeling]
			.OfType<LabelingTask>()
			.Single();

		InvokeNonPublic(typeof(Building), building, "SetOverrideCapsuleThreshold", true);
		InvokeNonPublic(typeof(Building), building, "SetCapsuleThresholdPercent", 0.0f);
		Assert.That(building.TrySetOutboundTargetStage(ItemProcessStage.Unlabeled), Is.True);
		coordinator.ProcessDirty();

		Assert.That(labelingTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(capsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.OB));
		CapsuleRelocationTask outboundTask = taskManager.TaskQueue[WorkerTask.TaskType.OB]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(outboundTask), Is.SameAs(source));
		Assert.That(GetTaskTarget(outboundTask), Is.SameAs(target));
	}

	[Test]
	public void LabelingService_ReenabledDiscoversMatchedGenericBufferWithoutNewDirty()
	{
		Building building = new(
			"Generic Labeling Reactivation Building",
			new List<GridCell>(),
			ItemProcessStage.Any);
		buildingManager.Register(building);
		CapsuleBuffer buffer = CreateBuffer(building, "Generic Labeling Reactivation Buffer", CapsuleDockState.IB);
		Assert.That(buildingManager.TryRegisterFacility(building.RuntimeBuildingId, buffer), Is.True);
		ApplyBufferRule(buffer, ItemProcessStage.Unlabeled);
		CargoCapsule capsule = CreateCapsule("Generic Labeling Reactivation Capsule", CapsuleLogisticsState.Inside);
		AddCargo(capsule, 918, 2, ItemStatus.None);
		Assert.That(buffer.TryDockCapsule(capsule), Is.True);
		coordinator.ProcessDirty();
		InvokeNonPublic(typeof(TaskManager), taskManager, "ProcessTaskBuildQueue");
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.Labeling], Is.Empty);

		ActivateInboundWorkflow();
		Assert.That(
			InvokeNonPublic(
				typeof(InboundWorkflowService),
				inboundWorkflow,
				"IsLabelingTargetReady",
				building.RuntimeBuildingId,
				buffer),
			Is.EqualTo(true));
		InvokeNonPublic(typeof(TaskManager), taskManager, "ProcessTaskBuildQueue");

		LabelingTask task = taskManager.TaskQueue[WorkerTask.TaskType.Labeling]
			.OfType<LabelingTask>()
			.Single();
		Assert.That(task.BuildingId, Is.EqualTo(building.RuntimeBuildingId));
		Assert.That(task.TargetBuffer, Is.SameAs(buffer));
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
	public void ProcessDirty_PickingOutputStaysInsideUntilPlayerPreemptsOwningTask()
	{
		const uint itemId = 914;
		ContentOutboundReadyBuilding building = new(
			"Picking Output Ownership Building",
			new List<GridCell>());
		buildingManager.Register(building);
		CapsuleBuffer source = CreateBuffer(
			building,
			"Picking Output Ownership Source",
			CapsuleDockState.OBStandby);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(
			building,
			"Picking Output Ownership Target");
		CargoCapsule capsule = CreateCapsule(
			"Picking Output Ownership Capsule",
			CapsuleLogisticsState.Inside);
		AddCargo(capsule, itemId, 3, ItemStatus.Labeled);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		ItemTransferTask pickingTask = new(
			WorkerTask.TaskType.Picking,
			new ItemTransferJob(
				planner: null,
				TransferObjectType.Item,
				TransferObjectType.Item,
				building.RuntimeBuildingId));
		InvokeNonPublic(
			typeof(ItemTransferTask),
			pickingTask,
			"RetainPickingOutput",
			new WorkLine(
				WorkLineAction.Put,
				source,
				source,
				itemId,
				3));
		HumanWorker worker = CreateWorker();
		Assert.That(worker.SetTask(pickingTask), Is.True);
		taskManager.AddRestoredInProgressTask(pickingTask);

		coordinator.MarkDirty(source);
		coordinator.ProcessDirty();

		Assert.That(capsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.Inside));
		Assert.That(taskManager.TaskQueue[WorkerTask.TaskType.OB], Is.Empty);
		Assert.That(pickingTask.DependsOnFacility(source), Is.True);

		Assert.That(coordinator.TryClaimForPlayer(source), Is.True);
		Assert.That(taskManager.TryPreemptCapsuleDockForPlayer(source), Is.True);
		Assert.That(pickingTask.CurrentStatus, Is.EqualTo(WorkerTask.Status.Invalidated));
		Assert.That(pickingTask.DependsOnFacility(source), Is.False);
		coordinator.ReleasePlayerClaim(source);
		coordinator.ProcessDirty();

		Assert.That(capsule.LogisticsState, Is.EqualTo(CapsuleLogisticsState.OB));
		CapsuleRelocationTask outboundTask = taskManager.TaskQueue[WorkerTask.TaskType.OB]
			.OfType<CapsuleRelocationTask>()
			.Single();
		Assert.That(GetTaskSource(outboundTask), Is.SameAs(source));
		Assert.That(GetTaskTarget(outboundTask), Is.SameAs(target));
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
	public void DiscardRestoredTask_ClearsCoordinatorOwnership()
	{
		AlwaysOutboundReadyBuilding building = CreateBuilding();
		CapsuleBuffer source = CreateBuffer(building, "Discard Source", CapsuleDockState.IB);
		OutboundCargoPort target = CreateDock<OutboundCargoPort>(building, "Discard Target");
		CapsuleRelocationTask task = CreateOutboundTask(building, source, target);
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
		AssertCoordinatorOwnershipReleased(source, target);
		Assert.That(
			processStats.GetStats(WorkerTask.TaskType.OB).CurrentQueue,
			Is.EqualTo(3),
			"discarding an unregistered restored task must not decrement queue stats");
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

	private Building CreateLaunchBuilding()
	{
		Building building = new(
			"Capsule Relocation Launch Test Building",
			new List<GridCell>(),
			ItemProcessStage.LaunchReady);
		buildingManager.Register(building);
		Assert.That(building.RuntimeBuildingId, Is.Not.Zero);
		return building;
	}

	private Building CreateStorageBuilding()
	{
		Building building = new(
			"Capsule Relocation Storage Test Building",
			new List<GridCell>(),
			ItemProcessStage.Picked);
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

	private void ActivateInboundWorkflow()
	{
		inboundWorkflow.gameObject.SetActive(true);
		// EditMode runners do not consistently dispatch MonoBehaviour OnEnable.
		// Invoke it explicitly while keeping event binding idempotent.
		InvokeNonPublic(typeof(InboundWorkflowService), inboundWorkflow, "OnEnable");
	}

	private void ApplyBufferRule(CapsuleBuffer buffer, ItemProcessStage stage)
	{
		ApplyFacilityRule(buffer, stage);
	}

	private void ApplyFacilityRule(
		IFacility facility,
		ItemProcessStage stage,
		WorkerKind requiredWorkerKind = WorkerKind.None)
	{
		FacilityRule rule = new();
		rule.SetRequiredContentState(FacilityContentState.HasItems);
		rule.SetRequiredItemProcessStage(stage);
		if (requiredWorkerKind != WorkerKind.None)
		{
			FacilityWorkerRule workerRule = new();
			workerRule.SetRequiredWorkerKind(requiredWorkerKind);
			rule.SetWorkerRule(workerRule);
		}

		Component component = facility as Component;
		FacilityRulePreset preset = facilityRuleManager.CreatePreset(
			$"{component?.name ?? "Facility"} {stage}",
			rule);
		Assert.That(facilityRuleManager.ApplyPreset(facility, preset.Id), Is.True);
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

	private static CapsuleDock GetTaskSource(CapsuleRelocationTask task)
	{
		return (CapsuleDock)GetPrivateField(typeof(CapsuleRelocationTask), task, "sourceDock");
	}

	private static CapsuleDock GetTaskTarget(CapsuleRelocationTask task)
	{
		return (CapsuleDock)GetPrivateField(typeof(CapsuleRelocationTask), task, "targetDock");
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
			: base(displayName, occupiedCells, ItemProcessStage.Labeled)
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
			: base(displayName, occupiedCells, ItemProcessStage.Any)
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
			: base(displayName, occupiedCells, ItemProcessStage.Any)
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
			: base(displayName, occupiedCells, ItemProcessStage.Any)
		{
		}

		protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
		{
			return OutboundReady && capsuleBuffer?.DockedCapsule != null;
		}
	}

	private sealed class FacilityDependentTestTask : WorkerTask
	{
		private readonly IFacility facility;

		public FacilityDependentTestTask(IFacility facility)
			: base(TaskType.PackingInput)
		{
			this.facility = facility;
		}

		public override bool DependsOnFacility(IFacility candidate) =>
			ReferenceEquals(facility, candidate);

		public override string GetStatusSummary() => "Facility dependency blocker";

		protected override IBaseNode BuildWorkNode() =>
			new ActionNode(CompleteImmediately);

		public override bool CheckTaskEnd() => false;

#if UNITY_EDITOR
		public override string ShowStatus() => GetStatusSummary();
#endif

		private static IBaseNode.NodeState CompleteImmediately(in BTContext context) =>
			IBaseNode.NodeState.Success;
	}
}
