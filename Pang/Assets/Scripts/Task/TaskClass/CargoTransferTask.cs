using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class CargoTransferTask : WorkerTask
{
	private readonly OutboundCargoPort sourcePort;
	private readonly InboundCargoPort targetPort;
	private bool isTaskEnd;

	internal OutboundCargoPort SourcePort => sourcePort;
	internal InboundCargoPort TargetPort => targetPort;

	public CargoTransferTask(OutboundCargoPort sourcePort, InboundCargoPort targetPort = null) : base(TaskType.CargoTransfer)
	{
		this.sourcePort = sourcePort;
		this.targetPort = targetPort;
		TrackDependencyBox(sourcePort?.DockedCapsule);
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError("No carryBox ability but assigned to cargo transfer task.");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		SequenceNode resume = new();
		resume.Add(new ActionNode(CheckWorkerCarriesPayload));
		resume.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetTargetPort));
		resume.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToPort));
		root.Add(resume);

		SequenceNode start = new();
		start.Add(AIWorker.ReturnBox());
		start.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Pick, SetSourceTarget));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		start.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetTargetPort));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToPort));
		root.Add(start);
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return CanDispatchToWorkerZones(worker, sourcePort, targetPort);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		return ReferenceEquals(targetPort, facility) ||
			(ReferenceEquals(sourcePort, facility) && HasActivePayload == false);
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		string sourceName = sourcePort != null ? sourcePort.name : "None";
		string targetName = targetPort != null ? targetPort.name : "None";
		return $"[CargoTransferTask] {sourceName} -> {targetName}";
	}
#endif

	public override string GetStatusSummary()
	{
		string sourceName = sourcePort != null ? sourcePort.name : "None";
		string targetName = targetPort != null ? targetPort.name : "Pending Inbound Cargo Port";
		return $"Cargo Transfer\nFrom: {sourceName}\nTo: {targetName}";
	}

	public static NodeState SetSourceTarget(in BTContext ctx)
	{
		CargoTransferTask task = (CargoTransferTask)ctx.Worker.CurrentTask;
		if (task.sourcePort == null || task.sourcePort.CanGetBox() == false)
		{
			task.isTaskEnd = true;
			return Failure;
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.sourcePort);
		return Success;
	}

	public static NodeState PickCapsule(in BTContext ctx)
	{
		CargoTransferTask task = (CargoTransferTask)ctx.Worker.CurrentTask;
		if (task.sourcePort == null)
		{
			task.isTaskEnd = true;
			return Failure;
		}

		AIWorker worker = ctx.Worker;
		if (worker.CarryingAbility == null || worker.CarryingAbility.CarryingBox != null)
			return Failure;

		if (task.sourcePort.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				task.sourcePort.PutBox(box);

			task.isTaskEnd = task.sourcePort.HasCapsule == false;
			return Failure;
		}

		return Success;
	}

	public static NodeState SetTargetPort(in BTContext ctx)
	{
		CargoTransferTask task = (CargoTransferTask)ctx.Worker.CurrentTask;
		if (task.targetPort == null || task.targetPort.CanPutBox() == false)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetPort);
		return Success;
	}

	public static NodeState StoreCapsuleToPort(in BTContext ctx)
	{
		CargoTransferTask task = (CargoTransferTask)ctx.Worker.CurrentTask;
		if (task.targetPort == null)
			return Failure;

		if (task.WorkerCarryBox.GetBox(out BoxBase box) == false)
			return Failure;

		if (task.targetPort.PutBox(box))
		{
			task.isTaskEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return AIWorker.KeepTaskWaiting(ctx);
	}
}
