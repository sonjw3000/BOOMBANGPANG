using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class OBTask : WorkerTask
{
	private readonly CapsuleBuffer sourceBuffer;
	private readonly uint buildingId;
	private readonly OutboundCargoPort targetPort;
	private bool isTaskEnd;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;

	internal CapsuleBuffer SourceBuffer => sourceBuffer;
	internal uint BuildingId => buildingId;
	internal OutboundCargoPort TargetPort => targetPort;

	public OBTask(CapsuleBuffer sourceBuffer, uint buildingId, OutboundCargoPort targetPort = null) : base(TaskType.OB)
	{
		this.sourceBuffer = sourceBuffer;
		this.buildingId = buildingId;
		this.targetPort = targetPort;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (buildingId == 0 || WorkerManager == null || sourceBuffer == null)
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

			int distance = math.abs(candidate.GridPosition.x - sourceBuffer.GridPosition.x) + math.abs(candidate.GridPosition.z - sourceBuffer.GridPosition.z);
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
			Debug.LogError("No carryBox ability but assigned to outbound transfer task.");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.ReturnBox());
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CapsuleBuffer, InteractionKind.Pick, SetSourceTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetTargetPort));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleToPort));
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return CanDispatchToWorkerZones(worker, sourceBuffer, targetPort);
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		string sourceName = sourceBuffer != null ? sourceBuffer.name : "None";
		string targetName = targetPort != null ? targetPort.name : "None";
		return $"[OBTask] {sourceName} -> {targetName}";
	}
#endif

	public override string GetStatusSummary()
	{
		string sourceName = sourceBuffer != null ? sourceBuffer.name : "None";
		string targetName = targetPort != null ? targetPort.name : "Pending Outbound Port";
		return $"Outbound Transfer\nFrom: {sourceName}\nTo: {targetName}";
	}

	public static NodeState SetSourceTarget(in BTContext ctx)
	{
		OBTask task = (OBTask)ctx.Worker.CurrentTask;
		if (task.sourceBuffer == null || task.sourceBuffer.CanDispatchToOutbound() == false)
		{
			task.isTaskEnd = true;
			return Failure;
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.sourceBuffer);
		return Success;
	}

	public static NodeState PickCapsule(in BTContext ctx)
	{
		OBTask task = (OBTask)ctx.Worker.CurrentTask;
		if (task.sourceBuffer == null)
		{
			task.isTaskEnd = true;
			return Failure;
		}

		AIWorker worker = ctx.Worker;
		if (worker.CarryingAbility == null || worker.CarryingAbility.CarryingBox != null)
			return Failure;

		if (task.sourceBuffer.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				task.sourceBuffer.PutBox(box);
			return Failure;
		}

		return Success;
	}

	public static NodeState SetTargetPort(in BTContext ctx)
	{
		OBTask task = (OBTask)ctx.Worker.CurrentTask;
		if (task.targetPort == null || task.targetPort.CanPutBox() == false)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetPort);
		return Success;
	}

	public static NodeState StoreCapsuleToPort(in BTContext ctx)
	{
		OBTask task = (OBTask)ctx.Worker.CurrentTask;
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
		return AIWorker.MoveToStandbyWhileWaiting(ctx);
	}
}
