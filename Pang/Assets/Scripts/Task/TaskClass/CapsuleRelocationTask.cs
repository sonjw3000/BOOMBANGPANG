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
	private readonly CapsuleDock targetDock;
	private readonly uint buildingId;
	private readonly CapsuleRelocationReason reason;
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

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.ReturnBox());
		root.Add(AIWorker.MoveToTarget(GetSourceTarget(), InteractionKind.Pick, SetSourceTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		root.Add(AIWorker.MoveToTarget(GetTargetTarget(), InteractionKind.Put, SetTargetDock));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToTarget));
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
		return targetDock != null ? targetDock.BuildingTarget : WorkerStatusTarget.None;
	}

	private bool CanUseSource()
	{
		if (sourceDock == null || sourceDock.CanGetBox() == false)
			return false;

		return Type switch
		{
			TaskType.IB when sourceDock is InboundCargoPort => sourceDock.IsCapsuleEmpty() == false,
			TaskType.CapsuleClear when sourceDock is CapsuleBuffer sourceBuffer => sourceBuffer.BufferState == CapsuleBufferState.IBOnly && sourceBuffer.IsCapsuleEmpty(),
			TaskType.CapsuleSupply when sourceDock is CapsuleBuffer sourceBuffer => sourceBuffer.BufferState == CapsuleBufferState.Empty && sourceBuffer.IsCapsuleEmpty(),
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
			TaskType.IB when targetDock is CapsuleBuffer targetBuffer => targetBuffer.CanReceiveFromInbound(),
			TaskType.CapsuleClear when targetDock is CapsuleBuffer targetBuffer => targetBuffer.BufferState == CapsuleBufferState.Empty && targetBuffer.CanPutBox(),
			TaskType.CapsuleSupply when targetDock is CapsuleBuffer targetBuffer => targetBuffer.BufferState == CapsuleBufferState.OBOnly && targetBuffer.CanPutBox(),
			_ => targetDock.CanPutBox(),
		};
	}

	public static NodeState SetSourceTarget(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
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

		return Success;
	}

	public static NodeState SetTargetDock(in BTContext ctx)
	{
		CapsuleRelocationTask task = (CapsuleRelocationTask)ctx.Worker.CurrentTask;
		if (task.CanUseTarget() == false)
		{
			ctx.Worker.SetWorkerTarget(task.GetTargetTarget());
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetDock);
		return Success;
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
		return AIWorker.MoveToStandbyWhileWaiting(ctx);
	}
}
