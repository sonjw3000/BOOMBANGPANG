using System;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public enum CapsuleRelocationReason
{
	SourceMustClear,
	DestinationNeedsCapsule,
	StateMismatch,
	RoleChanged,
}

public sealed class CapsuleRelocationTask : WorkerTask
{
	private readonly CapsuleDock sourceDock;
	private CapsuleDock targetDock;
	private readonly uint buildingId;
	private readonly CapsuleRelocationReason reason;
	private readonly uint targetBuildingId;
	private readonly CapsuleDockState targetDockState;
	private readonly Type targetDockType;
	private readonly WorkerStatusTarget targetWorkerStatus;
	private bool isTaskEnd;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;

	internal CapsuleDock SourceDock => sourceDock;
	internal CapsuleDock TargetDock => targetDock;
	internal uint BuildingId => buildingId;
	internal CapsuleRelocationReason Reason => reason;

	public CapsuleRelocationTask(
		TaskType taskType,
		CapsuleDock sourceDock,
		CapsuleDock targetDock,
		uint buildingId,
		CapsuleRelocationReason reason) : base(taskType)
	{
		this.sourceDock = sourceDock;
		this.targetDock = targetDock;
		this.buildingId = buildingId;
		this.reason = reason;
		targetDockState = targetDock != null ? targetDock.DockState : CapsuleDockState.Empty;
		targetDockType = targetDock?.GetType();
		targetWorkerStatus = targetDock != null ? targetDock.BuildingTarget : WorkerStatusTarget.None;
		if (targetDock != null && GameContext.HasInstance)
			GameContext.Instance.FacilityMgr?.TryGetBuildingId(targetDock, out targetBuildingId);
		TrackDependencyBox(sourceDock?.DockedCapsule);
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (buildingId == 0 || WorkerManager == null || sourceDock == null)
			return false;

		int bestDistance = int.MaxValue;
		foreach (AIWorker candidate in WorkerManager.Workers)
		{
			if (candidate == null ||
				candidate.PrimaryBuildingId != buildingId ||
				candidate.CanAcceptPreferredTask(this) == false)
			{
				continue;
			}

			int distance = math.abs(candidate.GridPosition.x - sourceDock.GridPosition.x) + math.abs(candidate.GridPosition.z - sourceDock.GridPosition.z);
			if (distance >= bestDistance)
				continue;

			bestDistance = distance;
			worker = candidate;
		}

		return true;
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError("No carryBox ability but assigned to capsule relocation task.");
	}

	protected override void OnTaskInvalidated()
	{
		NotifyRelocationEnded();
	}

	internal void NotifyRelocationEnded()
	{
		if (GameContext.HasInstance == false)
			return;

		GameContext.Instance.CapsuleRelocateCoordinator?.NotifyRelocationEnded(sourceDock, targetDock);
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		SequenceNode resume = new();
		resume.Add(new ActionNode(CheckWorkerCarriesPayload));
		resume.Add(AIWorker.MoveToTarget(GetTargetTarget(), InteractionKind.Put, SetTargetDock));
		resume.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToTarget));
		root.Add(resume);

		SequenceNode start = new();
		start.Add(AIWorker.ReturnBox());
		start.Add(AIWorker.MoveToTarget(GetSourceTarget(), InteractionKind.Pick, SetSourceTarget));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		start.Add(AIWorker.MoveToTarget(GetTargetTarget(), InteractionKind.Put, SetTargetDock));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToTarget));
		root.Add(start);
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return worker != null &&
			(buildingId == 0 || worker.PrimaryBuildingId == buildingId) &&
			CanDispatchToWorkerZones(worker, sourceDock, targetDock);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		if (ReferenceEquals(targetDock, facility))
			return true;

		return ReferenceEquals(sourceDock, facility) && HasActivePayload == false;
	}

	internal override FacilityTaskInvalidationAction HandleFacilityInvalidating(
		IFacility facility,
		in FacilityInvalidationContext context)
	{
		if (ReferenceEquals(targetDock, facility))
		{
			if (GameContext.HasInstance)
				GameContext.Instance.CapsuleRelocateCoordinator?.RemoveDock(targetDock);

			targetDock = null;
			return FacilityTaskInvalidationAction.Reevaluate;
		}

		return ReferenceEquals(sourceDock, facility) && HasActivePayload == false
			? FacilityTaskInvalidationAction.Invalidate
			: FacilityTaskInvalidationAction.None;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		string sourceName = sourceDock != null ? sourceDock.name : "None";
		string targetName = targetDock != null ? targetDock.name : "None";
		return $"[CapsuleRelocationTask:{Type}] {sourceName} -> {targetName} ({reason})";
	}
#endif

	public override string GetStatusSummary()
	{
		string sourceName = sourceDock != null ? sourceDock.name : "None";
		string targetName = targetDock != null ? targetDock.name : "Pending CapsuleDock";
		return $"Capsule Relocation ({Type})\nFrom: {sourceName}\nTo: {targetName}\nReason: {reason}";
	}

	private WorkerStatusTarget GetSourceTarget()
	{
		return sourceDock != null ? sourceDock.BuildingTarget : WorkerStatusTarget.None;
	}

	private WorkerStatusTarget GetTargetTarget()
	{
		return targetDock != null ? targetDock.BuildingTarget : targetWorkerStatus;
	}

	private bool CanUseSource()
	{
		if (sourceDock == null || sourceDock.CanGetBox() == false)
			return false;

		return Type switch
		{
			TaskType.Unloading when sourceDock is Rocket => sourceDock.DockedCapsule?.LogisticsState == CapsuleLogisticsState.IB,
			TaskType.IB when sourceDock is InboundCargoPort => sourceDock.IsCapsuleEmpty() == false && sourceDock.DockedCapsule?.LogisticsState == CapsuleLogisticsState.IB,
			TaskType.CapsuleClear when sourceDock is CapsuleBuffer sourceBuffer => sourceBuffer.CanRelocateEmptyCapsuleFrom(CapsuleDockState.IB),
			TaskType.CapsuleSupply when sourceDock is CapsuleBuffer sourceBuffer => sourceBuffer.CanRelocateEmptyCapsuleFrom(CapsuleDockState.Empty),
			TaskType.OB when sourceDock is CapsuleBuffer sourceBuffer => sourceBuffer.CanDispatchToOutbound(),
			_ => true,
		};
	}

	private bool CanUseTarget()
	{
		if (targetDock == null)
			return false;

		return Type switch
		{
			TaskType.Unloading when targetDock is InboundCargoPort => targetDock.CanPutBox(),
			TaskType.IB when targetDock is CapsuleBuffer targetBuffer => targetBuffer.CanReceiveFromInbound(),
			TaskType.CapsuleClear when targetDock is CapsuleBuffer targetBuffer => targetBuffer.DockState == CapsuleDockState.Empty && targetBuffer.CanPutBox(),
			TaskType.CapsuleSupply when targetDock is CapsuleBuffer targetBuffer => targetBuffer.DockState == CapsuleDockState.OBStandby && targetBuffer.CanPutBox(),
			_ => targetDock.CanPutBox(),
		};
	}

	public static NodeState SetSourceTarget(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.TryResolveReplacementTarget() == false)
		{
			ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		if (task.CanUseSource() == false)
		{
			task.isTaskEnd = true;
			return Failure;
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.sourceDock);
		return Success;
	}

	public static NodeState PickCapsule(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.sourceDock == null)
		{
			task.isTaskEnd = true;
			return Failure;
		}

		AIWorker worker = ctx.Worker;
		if (worker.CarryingAbility == null || worker.CarryingAbility.CarryingBox != null)
			return Failure;

		if (task.sourceDock.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				task.sourceDock.PutBox(box);

			task.isTaskEnd = task.sourceDock.HasCapsule == false;
			return Failure;
		}

		if (task.sourceDock is Rocket rocket && rocket.CanGetBox() == false)
			GameContext.Instance.RocketSvc.DisableRocket(rocket);

		return Success;
	}

	public static NodeState SetTargetDock(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.TryResolveReplacementTarget() == false || task.CanUseTarget() == false)
		{
			ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetDock);
		return Success;
	}

	private bool TryResolveReplacementTarget()
	{
		if (targetDock != null)
			return true;

		if (targetDockType == null || GameContext.HasInstance == false)
			return false;

		GameContext context = GameContext.Instance;
		CapsuleDockService dockService = context.CapsuleDockSvc;
		CapsuleRelocateCoordinator coordinator = context.CapsuleRelocateCoordinator;
		FacilityManager facilityManager = context.FacilityMgr;
		if (dockService == null || coordinator == null)
			return false;

		if (dockService.TryFindDock(
			targetBuildingId,
			targetDockState,
			false,
			out CapsuleDock replacement,
			candidate => candidate != null &&
				candidate.GetType() == targetDockType &&
				(facilityManager == null || facilityManager.IsInvalidating(candidate) == false) &&
				coordinator.IsReserved(candidate) == false &&
				coordinator.IsRelocationTargetActive(candidate) == false) == false)
		{
			return false;
		}

		if (coordinator.TryReserveActiveTarget(replacement) == false)
			return false;

		targetDock = replacement;
		return true;
	}

	public static NodeState StoreCapsuleToTarget(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.targetDock == null)
			return Failure;

		if (task.WorkerCarryBox.GetBox(out BoxBase box) == false)
			return Failure;

		if (task.targetDock.PutBox(box))
		{
			task.isTaskEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return AIWorker.KeepTaskWaiting(ctx);
	}
}
