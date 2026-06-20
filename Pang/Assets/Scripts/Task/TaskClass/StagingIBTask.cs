using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class StagingIBTask : WorkerTask
{
	private readonly InboundCargoPort sourcePort;
	private readonly uint buildingId;
	private BoxPool targetPool;
	private bool isTaskEnd;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;
	private static BoxPoolService CapsuleStorageService => GameContext.Instance.WMSys.BoxPoolService;

	public StagingIBTask(InboundCargoPort sourcePort, uint buildingId) : base(TaskType.Storing)
	{
		this.sourcePort = sourcePort;
		this.buildingId = buildingId;
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
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.BoxPool, InteractionKind.Put, SetTargetPool));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCapsule));
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
		string targetName = targetPool != null ? targetPool.name : "None";
		return $"[StagingIBTask] {sourceName} -> {targetName}";
	}
#endif

	public override string GetStatusSummary()
	{
		string sourceName = sourcePort != null ? sourcePort.name : "None";
		string targetName = targetPool != null ? targetPool.name : "Pending BoxPool";
		return $"Staging IB\nFrom: {sourceName}\nTo: {targetName}";
	}

	private BoxPool ResolveTargetPool(in int3 from)
	{
		if (targetPool != null && targetPool.CanPutBox())
			return targetPool;

		targetPool = CapsuleStorageService != null
			? CapsuleStorageService.GetClosestAvailableTarget(buildingId, from, InteractionKind.Put)
			: null;
		return targetPool;
	}

	public static NodeState SetSourceTarget(in BTContext ctx)
	{
		StagingIBTask task = (StagingIBTask)ctx.Worker.CurrentTask;
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
		StagingIBTask task = (StagingIBTask)ctx.Worker.CurrentTask;
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

	public static NodeState SetTargetPool(in BTContext ctx)
	{
		StagingIBTask task = (StagingIBTask)ctx.Worker.CurrentTask;
		BoxPool targetPool = task.ResolveTargetPool(ctx.Worker.GridPosition);
		if (targetPool == null)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.BoxPool);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(targetPool);
		return Success;
	}

	public static NodeState StoreCapsule(in BTContext ctx)
	{
		StagingIBTask task = (StagingIBTask)ctx.Worker.CurrentTask;
		if (task.targetPool == null)
			return Failure;

		if (task.WorkerCarryBox.GetBox(out BoxBase box) == false)
			return Failure;

		if (task.targetPool.PutBox(box))
		{
			task.isTaskEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		task.targetPool = null;
		return Failure;
	}
}
