using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class IBTask : WorkerTask
{
	private readonly InboundCargoPort sourcePort;
	private readonly uint buildingId;
	private CapsuleBuffer targetBuffer;
	private bool isTaskEnd;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;
	private static BuildingManager BuildingManager => GameContext.Instance.BuildingMgr;

	internal InboundCargoPort SourcePort => sourcePort;
	internal uint BuildingId => buildingId;

	public IBTask(InboundCargoPort sourcePort, uint buildingId, CapsuleBuffer targetBuffer = null) : base(TaskType.Storing)
	{
		this.sourcePort = sourcePort;
		this.buildingId = buildingId;
		this.targetBuffer = targetBuffer;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (buildingId == 0 || WorkerManager == null || sourcePort == null)
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

			int distance = math.abs(candidate.GridPosition.x - sourcePort.GridPosition.x) + math.abs(candidate.GridPosition.z - sourcePort.GridPosition.z);
			if (distance >= bestDistance)
				continue;

			bestDistance = distance;
			worker = candidate;
		}

		// Restrict dispatch to workers assigned to this building.
		return true;
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError("No carryBox ability but assigned to staging inbound task.");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.ReturnBox());
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Pick, SetSourceTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCapsule));
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CapsuleBuffer, InteractionKind.Put, SetTargetBuffer));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsuleInBuffer));
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
		string targetName = targetBuffer != null ? targetBuffer.name : "None";
		return $"[IBTask] {sourceName} -> {targetName}";
	}
#endif

	public override string GetStatusSummary()
	{
		string sourceName = sourcePort != null ? sourcePort.name : "None";
		string targetName = targetBuffer != null ? targetBuffer.name : "Pending CapsuleBuffer";
		return $"Inbound Transfer\nFrom: {sourceName}\nTo: {targetName}";
	}

	private CapsuleBuffer ResolveTargetBuffer(in int3 from)
	{
		if (targetBuffer != null && targetBuffer.CanReceiveFromInbound())
			return targetBuffer;

		if (buildingId == 0 || BuildingManager == null || BuildingManager.TryGetBuilding(buildingId, out Building building) == false)
			return null;

		targetBuffer = building.ResolveInboundBufferTarget(from);
		return targetBuffer;
	}

	public static NodeState SetSourceTarget(in BTContext ctx)
	{
		IBTask task = (IBTask)ctx.Worker.CurrentTask;
		if (task.sourcePort == null || task.sourcePort.CanGetBox() == false || task.sourcePort.IsCapsuleEmpty())
		{
			task.isTaskEnd = true;
			return Failure;
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.sourcePort);
		return Success;
	}

	public static NodeState PickCapsule(in BTContext ctx)
	{
		IBTask task = (IBTask)ctx.Worker.CurrentTask;
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
			return Failure;
		}

		return Success;
	}

	public static NodeState SetTargetBuffer(in BTContext ctx)
	{
		IBTask task = (IBTask)ctx.Worker.CurrentTask;
		CapsuleBuffer targetBuffer = task.ResolveTargetBuffer(ctx.Worker.GridPosition);
		if (targetBuffer == null)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CapsuleBuffer);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(targetBuffer);
		return Success;
	}

	public static NodeState StoreCapsuleInBuffer(in BTContext ctx)
	{
		IBTask task = (IBTask)ctx.Worker.CurrentTask;
		if (task.targetBuffer == null)
			return Failure;

		if (task.WorkerCarryBox.GetBox(out BoxBase box) == false)
			return Failure;

		if (task.targetBuffer.PutBox(box))
		{
			task.isTaskEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		task.targetBuffer = null;
		return Failure;
	}
}
