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
	WasteExport,
}

internal enum CapsuleDockPlayerPreemptionAction
{
	None,
	Reevaluate,
	Invalidate,
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
	private readonly CargoRouteKind routeKind;
	private bool isTaskEnd;
	private TaskInvalidationReason terminalInvalidationReason = TaskInvalidationReason.Unknown;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;

	internal CapsuleDock SourceDock => sourceDock;
	internal CapsuleDock TargetDock => targetDock;
	internal uint BuildingId => buildingId;
	internal CapsuleRelocationReason Reason => reason;
	internal CargoRouteKind RouteKind => routeKind;
	internal bool HasPickedCapsulePayload => ActivePayload is CargoCapsule;
	internal bool UsesDock(CapsuleDock dock) =>
		dock != null && (ReferenceEquals(sourceDock, dock) || ReferenceEquals(targetDock, dock));

	public CapsuleRelocationTask(
		TaskType taskType,
		CapsuleDock sourceDock,
		CapsuleDock targetDock,
		uint buildingId,
		CapsuleRelocationReason reason,
		CargoRouteKind? restoredRouteKind = null) : base(taskType)
	{
		this.sourceDock = sourceDock;
		this.targetDock = targetDock;
		this.buildingId = buildingId;
		this.reason = reason;
		targetDockState = targetDock != null ? targetDock.DockState : CapsuleDockState.Empty;
		targetDockType = targetDock?.GetType();
		targetWorkerStatus = targetDock != null ? targetDock.BuildingTarget : WorkerStatusTarget.None;
		routeKind = restoredRouteKind ?? sourceDock?.DockedCapsule?.RouteKind ?? CargoRouteKind.Standard;
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

	internal CapsuleDockPlayerPreemptionAction PreemptDockForPlayer(CapsuleDock dock)
	{
		if (dock == null)
			return CapsuleDockPlayerPreemptionAction.None;

		if (ReferenceEquals(targetDock, dock))
		{
			targetDock = null;
			if (GameContext.HasInstance)
				GameContext.Instance.CapsuleRelocateCoordinator?.NotifyRelocationTargetReleased(dock);
			return CapsuleDockPlayerPreemptionAction.Reevaluate;
		}

		if (ReferenceEquals(sourceDock, dock) && HasActivePayload == false)
			return CapsuleDockPlayerPreemptionAction.Invalidate;

		return CapsuleDockPlayerPreemptionAction.None;
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		SequenceNode resume = new();
		resume.Add(new ActionNode(CheckWorkerCarriesPayload));
		AddTargetPlacementNodes(resume);
		root.Add(resume);

		SequenceNode start = new();
		start.Add(AIWorker.ReturnBox());
		start.Add(AIWorker.MoveToTarget(GetSourceTarget(), InteractionKind.Pick, SetSourceTarget));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		AddTargetPlacementNodes(start);
		root.Add(start);
		return root;
	}

	private void AddTargetPlacementNodes(SequenceNode sequence)
	{
		sequence.Add(AIWorker.MoveToTarget(GetTargetTarget(), InteractionKind.Put, SetTargetDock));
		if (Type == TaskType.OB)
		{
			sequence.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, PrepareOutboundPlacement));
			sequence.Add(AIWorker.MoveToTarget(GetTargetTarget(), InteractionKind.Put, SetTargetDock));
			sequence.Add(new ActionNode(StoreCapsuleToTarget));
			return;
		}

		sequence.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToTarget));
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	protected override bool TryGetTerminalInvalidationReason(out TaskInvalidationReason reason)
	{
		reason = terminalInvalidationReason;
		return reason != TaskInvalidationReason.Unknown;
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
			TaskType.OB when sourceDock is CapsuleBuffer sourceBuffer => CanDispatchOutbound(sourceBuffer),
			_ => true,
		};
	}

	private static bool CanDispatchOutbound(CapsuleBuffer sourceBuffer)
	{
		if (sourceBuffer == null || sourceBuffer.CanDispatchToOutbound() == false)
			return false;

		if (GameContext.HasInstance == false ||
			GameContext.Instance.FacilityMgr == null ||
			GameContext.Instance.BuildingMgr == null ||
			GameContext.Instance.FacilityMgr.TryGetBuildingId(sourceBuffer, out uint sourceBuildingId) == false ||
			GameContext.Instance.BuildingMgr.TryGetBuilding(sourceBuildingId, out Building sourceBuilding) == false ||
			sourceBuilding is not LaunchBuilding launchBuilding)
		{
			return true;
		}

		return launchBuilding.TryPrepareOutboundDispatch(sourceBuffer);
	}

	private bool CanUseTarget()
	{
		if (targetDock == null)
			return false;

		CargoCapsule payload = ActivePayload as CargoCapsule ?? sourceDock?.DockedCapsule;
		if (payload != null && targetDock.CanAcceptCargoRoute(payload.RouteKind) == false)
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
		if (task.CanUseSource() == false)
		{
			task.MarkTerminalFailure(TaskInvalidationReason.SourceUnavailable);
			return Failure;
		}

		if (task.TryResolveReplacementTarget() == false)
		{
			ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.sourceDock);
		return Success;
	}

	public static NodeState PickCapsule(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.sourceDock == null)
		{
			task.MarkTerminalFailure(TaskInvalidationReason.SourceUnavailable);
			return Failure;
		}

		AIWorker worker = ctx.Worker;
		if (worker.CarryingAbility == null || worker.CarryingAbility.CarryingBox != null)
			return Failure;
		if (task.CanUseSource() == false)
		{
			task.MarkTerminalFailure(TaskInvalidationReason.SourceUnavailable);
			return Failure;
		}
		if (task.Type == TaskType.OB &&
			GameContext.Instance.CapsuleRelocateCoordinator.TryHoldSourceForPotentialReturn(task.sourceDock) == false)
		{
			task.MarkTerminalFailure(TaskInvalidationReason.CoordinatorOwnershipLost);
			return Failure;
		}

		if (task.sourceDock.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				task.sourceDock.PutBox(box);

			if (task.sourceDock.HasCapsule == false)
				task.MarkTerminalFailure(TaskInvalidationReason.PayloadMissing);
			return Failure;
		}

		if (task.sourceDock is WasteBinDock wasteBinDock && box is WasteBin wasteBin && wasteBin.IsFull)
			wasteBinDock.ProvisionReplacementBin();

		if (task.sourceDock is Rocket rocket && rocket.CanGetBox() == false)
			GameContext.Instance.RocketSvc.DisableRocket(rocket);

		worker.ReportBoxHandling(box);
		return Success;
	}

	public static NodeState SetTargetDock(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.TryRedirectRejectedOutboundPayload() == false ||
			task.TryResolveReplacementTarget() == false ||
			task.CanUseTarget() == false)
		{
			ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetDock);
		return Success;
	}

	private bool TryRedirectRejectedOutboundPayload()
	{
		// Staging uses OB tasks to move labeled capsules out of IB buffers.
		// Packed-manifest validation belongs to outbound-ready OBStandby buffers.
		if (Type != TaskType.OB ||
			ActivePayload is not CargoCapsule capsule ||
			sourceDock is not CapsuleBuffer sourceBuffer ||
			sourceBuffer.DockState != CapsuleDockState.OBStandby)
			return true;

		GameContext context = GameContext.HasInstance ? GameContext.Instance : null;
		FacilityManager facilityManager = context?.FacilityMgr;
		BuildingManager buildingManager = context?.BuildingMgr;
		if (facilityManager == null ||
			buildingManager == null ||
			facilityManager.TryGetBuildingId(sourceBuffer, out uint sourceBuildingId) == false ||
			buildingManager.TryGetBuilding(sourceBuildingId, out Building sourceBuilding) == false ||
			sourceBuilding is not LaunchBuilding)
		{
			return true;
		}

		OutboundWorkflowService outbound = context?.OBWorkflowSvc;
		if (outbound == null ||
			(outbound.HasDispatchBlockingCargo(capsule) == false &&
			 outbound.HasCompleteDispatchManifest(capsule)))
		{
			return true;
		}

		if (targetDock is CapsuleBuffer currentBuffer &&
			ReferenceEquals(targetDock, sourceDock) &&
			currentBuffer.DockState == CapsuleDockState.OBStandby &&
			currentBuffer.CanPutBox() &&
			currentBuffer.CanAcceptCargoRoute(capsule.RouteKind))
		{
			capsule.SetLogisticsState(CapsuleLogisticsState.OBStandby);
			return true;
		}

		CapsuleRelocateCoordinator coordinator = context.CapsuleRelocateCoordinator;
		if (coordinator == null ||
			sourceBuffer.CanPutBox() == false ||
			sourceBuffer.CanAcceptCargoRoute(capsule.RouteKind) == false ||
			(facilityManager != null && facilityManager.IsInvalidating(sourceBuffer)))
			return false;

		if (coordinator.TryReplaceActiveTargetWithHeldSource(targetDock, sourceBuffer) == false)
			return false;

		Debug.Log(
			$"[OutboundQualityControl] Redirecting rejected capsule from {targetDock?.name ?? "None"} to {sourceBuffer.name}.");
		capsule.SetLogisticsState(CapsuleLogisticsState.OBStandby);
		targetDock = sourceBuffer;
		return true;
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
				candidate.CanAcceptCargoRoute((ActivePayload as CargoCapsule)?.RouteKind ?? RouteKind) &&
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

		if (task.WorkerCarryBox == null || task.WorkerCarryBox.GetBox(out BoxBase box) == false)
		{
			task.MarkTerminalFailure(TaskInvalidationReason.PayloadMissing);
			return Failure;
		}

		if (task.targetDock.PutBox(box))
		{
			if (task.Type != TaskType.OB)
				ctx.Worker.ReportBoxHandling(box);
			task.terminalInvalidationReason = TaskInvalidationReason.Unknown;
			task.isTaskEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return AIWorker.KeepTaskWaiting(ctx);
	}

	private static NodeState PrepareOutboundPlacement(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.ActivePayload is not CargoCapsule capsule)
		{
			task.MarkTerminalFailure(TaskInvalidationReason.PayloadMissing);
			return Failure;
		}

		ctx.Worker.ReportBoxHandling(capsule);
		return Success;
	}

	private void MarkTerminalFailure(TaskInvalidationReason reason)
	{
		terminalInvalidationReason = reason;
		isTaskEnd = true;
	}
}
