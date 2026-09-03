using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.AI.BT;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class PlayerOverrideEditModeTests
{
	private readonly List<GameObject> objects = new();
	private GameContext previousContext;
	private GameContext context;
	private GridService grid;
	private FacilityManager facilities;
	private PathFindingService pathFinding;
	private RobotWorker worker;
	private FindRoute route;
	private PlayerOverrideService controls;
	private CapsuleRelocateCoordinator coordinator;
	private ItemDefinition itemDefinition;
	private static readonly int3 StartCell = new(10, 0, 10);

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)Field(typeof(GameContext), "instance").GetValue(null);
		Field(typeof(GameContext), "instance").SetValue(null, null);
		grid = Create<GridService>();
		grid.BuildDefaultMap();
		facilities = Create<FacilityManager>();
		WorkerManager workers = Create<WorkerManager>();
		Invoke(typeof(WorkerManager), workers, "Awake");
		WMSystem warehouse = Create<WMSystem>();
		WorkPolicyService policy = Create<WorkPolicyService>();
		Set(policy, "workPolicy", AssetDatabase.LoadAssetAtPath<WorkPolicy>("Assets/ScriptableObjs/WorkPolicy/WorkPolicyTest.asset"));
		Set(warehouse, "workPolicyService", policy);
		pathFinding = Create<PathFindingService>();
		context = Create<GameContext>();
		Set(context, "gridService", grid);
		Set(context, "facilityManager", facilities);
		Set(context, "workerManager", workers);
		Set(context, "warehouseManagement", warehouse);
		Set(context, "pathFindingService", pathFinding);
		Set(context, "trafficCoordinator", Create<TrafficCoordinator>());
		Set(context, "restFacilityService", Create<RestFacilityService>());
		Set(context, "chargingFacilityService", Create<ChargingFacilityService>());
		Set(context, "robotNavigationService", Create<RobotNavigationService>());
		CapsuleDockService docks = Create<CapsuleDockService>();
		Set(context, "capsuleDockService", docks);
		CapsuleBufferService buffers = Create<CapsuleBufferService>();
		Set(context, "capsuleBufferService", buffers);
		Set(context, "cargoPortService", Create<CargoPortService>());
		Set(context, "outboundWorkflowService", Create<OutboundWorkflowService>());
		ItemDatabase items = Create<ItemDatabase>();
		itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
		Set(itemDefinition, "itemID", 123u);
		Set(itemDefinition, "size", 1f);
		Set(items, "itemIDMap", new Dictionary<uint, ItemDefinition> { [123u] = itemDefinition });
		Set(context, "itemDB", items);
		BoxPoolService pools = Create<BoxPoolService>();
		Set(warehouse, "boxPoolService", pools);
		Field(typeof(GameContext), "instance").SetValue(null, context);
		Invoke(typeof(FacilityService<CapsuleDock>), docks, "TryBindFacilityManager");
		Invoke(typeof(FacilityService<CapsuleBuffer>), buffers, "TryBindFacilityManager");
		Invoke(typeof(FacilityService<BoxPool>), pools, "TryBindFacilityManager");
		Invoke(typeof(PathFindingService), pathFinding, "Start");
		coordinator = new CapsuleRelocateCoordinator(docks);
		Set(context, "capsuleRelocateCoordinator", coordinator);
		controls = context.PlayerOverrideSvc;

		worker = Create<RobotWorker>();
		worker.SetRobotIdentity(RobotType.Transfer);
		Set(worker, "navigationDependency", RobotNavigationDependency.FullyAutonomous);
		Set(worker, "baseWorkSpeedMultiplier", 1f);
		Set(worker, "baseMoveSpeedMultiplier", 1f);
		Set(worker, "abilities", WorkerAbility.CarryBox | WorkerAbility.CargoHandling | WorkerAbility.PickingStoring);
		worker.SetPrimaryBuildingId(1);
		worker.ChangeWorkerType(WorkerTask.TaskType.Storing);
		route = worker.gameObject.AddComponent<FindRoute>();
		Set(worker, "routeFinder", route);
		Set(route, "worker", worker);
		worker.OnPositionSet(StartCell, FacingDirection.North);
		grid.GetCell(StartCell).SetBuildingId(1);
		GameObject slot = new("SlotRoot");
		slot.transform.SetParent(worker.transform);
		worker.gameObject.AddComponent<CarryBoxAbility>();
	}

	[TearDown]
	public void TearDown()
	{
		Field(typeof(GameContext), "instance").SetValue(null, null);
		for (int i = objects.Count - 1; i >= 0; --i)
			if (objects[i] != null)
				UnityEngine.Object.DestroyImmediate(objects[i]);
		objects.Clear();
		if (itemDefinition != null) UnityEngine.Object.DestroyImmediate(itemDefinition);
		Field(typeof(GameContext), "instance").SetValue(null, previousContext);
	}

	[TestCase(RobotNavigationWaitReason.Coverage)]
	[TestCase(RobotNavigationWaitReason.OrchestrationCapacity)]
	public void TakeControl_IdleNavigationWait_AcceptsPathAfterTakeover(RobotNavigationWaitReason reason)
	{
		worker.BeginNavigationWait(reason);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(2, 0, 0), out string message), Is.True, message);
		Assert.That(worker.IsWaitingForNavigation, Is.False);
		worker.RunBT(new BlackBoard());
		Invoke(typeof(PathFindingService), pathFinding, "Update");
		Assert.That(route.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Moving));
		Assert.That(route.enabled, Is.True);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void Move_CarryingContainer_ReplacesCommandAndCanStopAtCurrentCell(bool capsule)
	{
		BoxBase box = Carry(capsule);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(2, 0, 0), out _), Is.True);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(0, 0, 3), out _), Is.True);
		Assert.That(worker.TryGetPlayerOverrideDestination(out int3 goal), Is.True);
		Assert.That(goal, Is.EqualTo(StartCell + new int3(0, 0, 3)));
		Assert.That(controls.TryRequestMove(worker, StartCell, out _), Is.True);
		Assert.That(worker.PlayerOverridePhase, Is.EqualTo(PlayerOverridePhase.AwaitingCommand));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(box));
	}

	[Test]
	public void Move_AlreadyManualWithStaleNavigationWait_ClearsWaitAndAcceptsPath()
	{
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Set(worker, "navigationWaitReason", RobotNavigationWaitReason.Coverage);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(2, 0, 0), out _), Is.True);
		worker.RunBT(new BlackBoard());
		Invoke(typeof(PathFindingService), pathFinding, "Update");
		Assert.That(worker.IsWaitingForNavigation, Is.False);
		Assert.That(route.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Moving));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void Release_CarryingDuringMovement_SwitchesToAutomaticAndKeepsPayload(bool capsule)
	{
		BoxBase box = Carry(capsule);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(2, 0, 0), out _), Is.True);
		Assert.That(controls.TryReleaseControl(worker, out string message), Is.True, message);
		Assert.That(worker.ControlMode, Is.EqualTo(WorkerControlMode.Automatic));
		Assert.That(worker.IsReturningPlayerContainer, Is.True);
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(box));
		Assert.That(box.CurrentCarrier, Is.SameAs(worker));
		Assert.That(worker.CanAcceptGeneralTask(WorkerTask.TaskType.Storing), Is.False);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(0, 0, 2), out _), Is.True);
		Assert.That(worker.IsReturningPlayerContainer, Is.False);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void AirlockCommand_ReplacementOrRelease_ReleasesAirlock(bool release)
	{
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Airlock airlock = Create<Airlock>();
		Assert.That(airlock.TryReserve(worker, AirlockDirection.InsideToOutside), Is.True);
		BlackBoard board = (BlackBoard)Get(worker, "playerOverrideBlackBoard");
		board.Set("TransitAirlock", airlock);
		Set(worker, "playerOverridePhase", PlayerOverridePhase.UsingAirlock);
		bool accepted = release
			? controls.TryReleaseControl(worker, out _)
			: controls.TryRequestMove(worker, StartCell, out _);
		Assert.That(accepted, Is.True);
		Assert.That(airlock.IsAvailable, Is.True);
		Assert.That(airlock.ReservedWorker, Is.Null);
		Assert.That(worker.enabled, Is.True);
	}

	[Test]
	public void ReplacedPathRequest_LateResultCannotRestartCancelledMovement()
	{
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(2, 0, 0), out _), Is.True);
		worker.RunBT(new BlackBoard());
		Assert.That(route.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.PathPending));
		Assert.That(controls.TryRequestMove(worker, StartCell, out _), Is.True);
		Invoke(typeof(PathFindingService), pathFinding, "Update");
		Assert.That(route.CurrentMovementState, Is.EqualTo(FindRoute.MovementState.Idle));
		Assert.That(route.HasActiveGoal, Is.False);
		Assert.That(worker.PlayerOverridePhase, Is.EqualTo(PlayerOverridePhase.AwaitingCommand));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void AutomaticReturn_ContainerReachesStorageAndWorkerBecomesAvailable(bool capsule)
	{
		BoxBase box = Carry(capsule);
		BoxInteraction target;
		if (capsule)
		{
			CapsuleBuffer buffer = Create<CapsuleBuffer>();
			Set(buffer, "retainEmptyCapsule", true);
			target = buffer;
		}
		else
		{
			BoxPool pool = Create<BoxPool>();
			Set(pool, "boxStackPos", pool.gameObject);
			target = pool;
		}
		target.OnPositionSet(StartCell, FacingDirection.North);
		target.AddInteractionPoint(InteractionKind.Put, StartCell);
		facilities.RegisterFacility(1, target);
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Assert.That(controls.TryReleaseControl(worker, out _), Is.True);
		IBaseNode node = (IBaseNode)Invoke(typeof(AIWorker), worker, "BuildPlayerContainerReturnNode");
		List<string> trace = new();
		for (int tick = 0; tick < 6 && worker.IsReturningPlayerContainer; ++tick)
		{
			BTContext bt = new() { Worker = worker, LocalBlackBoard = (BlackBoard)Get(worker, "localBlackBoard"), GlobalBlackBoard = new(), DeltaTime = 1000, Tick = tick };
			IBaseNode.NodeState result = node.Evaluate(bt);
			AdvanceWorkClock(node);
			Invoke(typeof(PathFindingService), pathFinding, "Update");
			trace.Add($"{tick}: {result}, action={worker.WorkerState.Action}, route={route.CurrentMovementState}, target={Get(worker, "playerContainerReturnTarget")}");
		}
		Assert.That(worker.CarryingAbility.CarryingBox, Is.Null, string.Join("\n", trace));
		Assert.That(worker.IsReturningPlayerContainer, Is.False);
		Assert.That(worker.CanAcceptGeneralTask(WorkerTask.TaskType.Storing), Is.True);
		Assert.That(box.CurrentCarrier, Is.Null);
		if (target is CapsuleDock dock)
		{
			Assert.That(dock.DockedCapsule, Is.SameAs(box));
			Assert.That(coordinator.IsReserved(dock), Is.False);
			Assert.That(coordinator.IsRelocationTargetActive(dock), Is.False);
		}
		else
			Assert.That(((BoxPool)target).CurrentBoxCount, Is.EqualTo(1));
	}

	[Test]
	public void AutomaticReturn_LoadedTote_TransfersContentsAndManifestBeforeReturningEmptyBox()
	{
		BoxBase box = Carry(false);
		box.SetBoxId(1);
		Assert.That(box.AddItem(123, 2), Is.EqualTo(2));
		foreach (ItemStack stack in box.Stacks) stack.SetStatus(ItemStatus.Labeled);
		OrderLine order = new(null, 123, 2, null);
		context.OBWorkflowSvc.AddPickedToManifest(box, order, 123, 2);
		CargoCapsule destination = Create<CargoCapsule>();
		Set(destination, "boxType", BoxType.Capsule);
		destination.SetBoxId(2);
		CapsuleBuffer buffer = Create<CapsuleBuffer>();
		buffer.OnPositionSet(StartCell, FacingDirection.North);
		buffer.AddInteractionPoint(InteractionKind.Put, StartCell);
		Assert.That(buffer.TryDockCapsule(destination), Is.True);
		facilities.RegisterFacility(1, buffer);
		BoxPool pool = Create<BoxPool>();
		Set(pool, "boxStackPos", pool.gameObject);
		pool.OnPositionSet(StartCell, FacingDirection.North);
		pool.AddInteractionPoint(InteractionKind.Put, StartCell);
		facilities.RegisterFacility(1, pool);
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Assert.That(controls.TryReleaseControl(worker, out _), Is.True);
		IBaseNode node = (IBaseNode)Invoke(typeof(AIWorker), worker, "BuildPlayerContainerReturnNode");
		for (int tick = 0; tick < 12 && worker.IsReturningPlayerContainer; ++tick)
		{
			BTContext bt = new() { Worker = worker, LocalBlackBoard = (BlackBoard)Get(worker, "localBlackBoard"), GlobalBlackBoard = new(), Tick = tick };
			node.Evaluate(bt);
			AdvanceWorkClock(node);
			Invoke(typeof(PathFindingService), pathFinding, "Update");
		}
		Assert.That(destination.GetQuantity(123), Is.EqualTo(2));
		Assert.That(box.Stacks, Is.Empty);
		Assert.That(pool.CurrentBoxCount, Is.EqualTo(1));
		Assert.That(worker.CarryingAbility.CarryingBox, Is.Null);
		Assert.That(worker.IsReturningPlayerContainer, Is.False);
		Assert.That(coordinator.IsPlayerClaimed(buffer), Is.False);
		Assert.That(context.OBWorkflowSvc.TryGetPickingManifest(box, out _), Is.False);
		Assert.That(context.OBWorkflowSvc.TryGetPickingManifest(destination, out PickingManifest manifest), Is.True);
		Assert.That(manifest.Lines[0].PickedQuantity, Is.EqualTo(2));
	}

	[Test]
	public void RetakeControl_DuringAutomaticReturn_ReleasesReservedDock()
	{
		Carry(true);
		CapsuleBuffer buffer = Create<CapsuleBuffer>();
		buffer.OnPositionSet(StartCell, FacingDirection.North);
		buffer.AddInteractionPoint(InteractionKind.Put, StartCell);
		facilities.RegisterFacility(1, buffer);
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Assert.That(controls.TryReleaseControl(worker, out _), Is.True);
		IBaseNode node = (IBaseNode)Invoke(typeof(AIWorker), worker, "BuildPlayerContainerReturnNode");
		BTContext bt = new() { Worker = worker, LocalBlackBoard = (BlackBoard)Get(worker, "localBlackBoard"), GlobalBlackBoard = new() };
		node.Evaluate(bt);
		Assert.That(coordinator.IsReserved(buffer), Is.True);
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(2, 0, 0), out _), Is.True);
		Assert.That(coordinator.IsReserved(buffer), Is.False);
		Assert.That(coordinator.IsRelocationTargetActive(buffer), Is.False);
	}

	[Test]
	public void SaveRecovery_ReturningCapsule_IsNotDroppedAsOrphan()
	{
		BoxBase box = Carry(true);
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Assert.That(controls.TryReleaseControl(worker, out _), Is.True);
		MethodInfo recover = typeof(GameSaveService).GetMethod("RecoverOrphanedLoadedCapsules", BindingFlags.Static | BindingFlags.NonPublic);
		recover.Invoke(null, new object[] { new Dictionary<uint, AIWorker> { [1] = worker } });
		Assert.That(worker.CarryingAbility.CarryingBox, Is.SameAs(box));
		Assert.That(box.IsPlacedOnGrid, Is.False);
	}

	[Test]
	public void AutomaticReturn_NoDestination_RemainsReclaimableAndPersistsState()
	{
		Carry(true);
		Assert.That(controls.TryTakeControl(worker, out _), Is.True);
		Assert.That(controls.TryReleaseControl(worker, out _), Is.True);
		IBaseNode node = (IBaseNode)Invoke(typeof(AIWorker), worker, "BuildPlayerContainerReturnNode");
		BTContext bt = new() { Worker = worker, LocalBlackBoard = (BlackBoard)Get(worker, "localBlackBoard"), GlobalBlackBoard = new() };
		node.Evaluate(bt);
		Assert.That(worker.WorkerState.Action, Is.EqualTo(WorkerStatusAction.WaitingForTargetBuilding));
		WorkerSaveData save = JsonUtility.FromJson<WorkerSaveData>(JsonUtility.ToJson(worker.CaptureState()));
		Assert.That(save.ReturningPlayerContainer, Is.True);
		Assert.That(save.ControlMode, Is.EqualTo(WorkerControlMode.Automatic));
		Assert.That(controls.TryRequestMove(worker, StartCell + new int3(0, 0, 2), out _), Is.True);
	}

	private BoxBase Carry(bool capsule)
	{
		BoxBase box = capsule ? Create<CargoCapsule>() : Create<ToteBox>();
		Set(box, "boxType", capsule ? BoxType.Capsule : BoxType.Personal);
		Set(box, "isValid", true);
		Assert.That(worker.TryAttachBox(box), Is.True);
		return box;
	}

	private static void AdvanceWorkClock(IBaseNode node)
	{
		// DoWorkNode uses Unity's clock, which does not advance in a synchronous EditMode test.
		if (node is DoWorkNode)
			Set(node, "startTime", Time.time - 1000000f);
		if (node is SequenceNode sequence)
			foreach (IBaseNode child in sequence.Children) AdvanceWorkClock(child);
		if (node is SelectorNode selector)
			foreach (IBaseNode child in selector.Children) AdvanceWorkClock(child);
	}

	private T Create<T>() where T : Component
	{
		GameObject go = new($"Player Override Test {typeof(T).Name}");
		go.SetActive(false);
		objects.Add(go);
		return go.AddComponent<T>();
	}

	private static FieldInfo Field(Type type, string name)
	{
		for (; type != null; type = type.BaseType)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (field != null) return field;
		}
		throw new MissingFieldException(name);
	}
	private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
	private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
	private static object Invoke(Type type, object target, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
