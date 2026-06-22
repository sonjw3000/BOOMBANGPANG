using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class CargoTransferTask : WorkerTask
{
	private readonly OutboundCargoPort sourcePort;
	private InboundCargoPort targetPort;
	private bool isTaskEnd;

	private static GridService GridService => GameContext.Instance.GridService;

	internal OutboundCargoPort SourcePort => sourcePort;
	internal InboundCargoPort TargetPort => targetPort;

	public CargoTransferTask(OutboundCargoPort sourcePort, InboundCargoPort targetPort = null) : base(TaskType.CargoTransfer)
	{
		this.sourcePort = sourcePort;
		this.targetPort = targetPort;
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError("No carryBox ability but assigned to cargo transfer task.");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.ReturnBox());
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Pick, SetSourceTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetTargetPort));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToPort));
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
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

	private InboundCargoPort ResolveTargetPort(in int3 from)
	{
		if (targetPort != null && targetPort.CanPutBox())
			return targetPort;

		targetPort = FindClosestLinkedInboundPort(sourcePort, from);
		return targetPort;
	}

	private static InboundCargoPort FindClosestLinkedInboundPort(OutboundCargoPort outboundCargoPort, in int3 from)
	{
		if (outboundCargoPort == null || GridService == null)
			return null;

		InboundCargoPort bestCandidate = null;
		int bestScore = int.MaxValue;
		for (int i = 0; i < outboundCargoPort.LinkedPorts.Count; ++i)
		{
			if (outboundCargoPort.LinkedPorts[i] is not InboundCargoPort candidate || candidate.CanPutBox() == false)
				continue;

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(candidate, InteractionKind.Put, from, GridService, out _, out int score) == false)
				continue;

			if (score >= bestScore)
				continue;

			bestScore = score;
			bestCandidate = candidate;
		}

		return bestCandidate;
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
		InboundCargoPort targetPort = task.ResolveTargetPort(ctx.Worker.GridPosition);
		if (targetPort == null)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(targetPort);
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
		task.targetPort = null;
		return Failure;
	}
}
