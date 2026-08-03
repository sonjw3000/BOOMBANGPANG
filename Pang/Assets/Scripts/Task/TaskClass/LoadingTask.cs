using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class LoadingTask : WorkerTask
{
	private bool isLoadEnd = false;

	private readonly CargoPort sourcePort;
	private LaunchStation targetStation;

	internal CargoPort SourcePort => sourcePort;
	internal LaunchStation TargetStation => targetStation;

	public LoadingTask(CargoPort sourcePort, LaunchStation targetStation) : base(TaskType.Loading)
	{
		this.sourcePort = sourcePort;
		this.targetStation = targetStation;
		TrackDependencyBox(sourcePort?.DockedCapsule);
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
		{
			Debug.LogError("No carryBox ability but assigned to ccc!!");
		}
	}


	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		SequenceNode resume = new();
		resume.Add(new ActionNode(CheckWorkerCarriesPayload));
		resume.Add(AIWorker.MoveToTarget(WorkerStatusTarget.LaunchStation, InteractionKind.Put, SetLaunchStation));
		resume.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCargo));
		root.Add(resume);

		SequenceNode start = new();
		start.Add(AIWorker.ReturnBox());
		start.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Pick, SetLoadTarget));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCargo));
		start.Add(AIWorker.MoveToTarget(WorkerStatusTarget.LaunchStation, InteractionKind.Put, SetLaunchStation));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCargo));
		root.Add(start);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isLoadEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return CanDispatchToWorkerZones(worker, sourcePort, targetStation);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		if (ReferenceEquals(targetStation, facility))
			return true;

		return ReferenceEquals(sourcePort, facility) && HasActivePayload == false;
	}

	internal override FacilityTaskInvalidationAction HandleFacilityInvalidating(
		IFacility facility,
		in FacilityInvalidationContext context)
	{
		if (ReferenceEquals(targetStation, facility))
		{
			targetStation = null;
			return FacilityTaskInvalidationAction.Reevaluate;
		}

		return ReferenceEquals(sourcePort, facility) && HasActivePayload == false
			? FacilityTaskInvalidationAction.Invalidate
			: FacilityTaskInvalidationAction.None;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[LoadingTask] : ";
	}
#endif

	public override string GetStatusSummary()
	{
		if (isLoadEnd)
			return $"CargoPort: {sourcePort?.name ?? "None"}\nLaunchStation: {targetStation?.name ?? "None"}\nLoading complete.";

		return $"CargoPort: {sourcePort?.name ?? "None"}\nLaunchStation: {targetStation?.name ?? "None"}\nMoving cargo to launch station.";
	}

	static private NodeState SetLoadTarget(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask as LoadingTask;
		if (task?.sourcePort == null)
			return Failure;

		if (task.targetStation == null && GameContext.HasInstance)
		{
			task.targetStation = GameContext.Instance.OBWorkflowSvc?.ResolveLoadingTargetStation(
				task.sourcePort,
				task.sourcePort.DockedCapsule,
				ctx.Worker.GridPosition);
		}

		if (task.targetStation == null)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.sourcePort);

		return Success;
	}

	static private NodeState PickCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);

		if (task.sourcePort == null)
		{
			Debug.LogError("No available load port found!");
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		if (ctx.Worker.CarryingAbility == null || ctx.Worker.CarryingAbility.CarryingBox != null)
			return Failure;

		if (task.sourcePort.GetBox(out BoxBase box) == false || ctx.Worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				task.sourcePort.PutBox(box);

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.Worker.ReportBoxHandling(box);
		return Success;
	}

	static private NodeState SetLaunchStation(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		if (task.targetStation == null && GameContext.HasInstance)
		{
			task.targetStation = GameContext.Instance.OBWorkflowSvc?.ResolveLoadingTargetStation(
				task.sourcePort,
				task.ActivePayload,
				ctx.Worker.GridPosition);
		}

		if (task.targetStation == null)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetStation);
		return Success;
	}

	static private NodeState StoreCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var carryAbility = ctx.Worker.CarryingAbility;
		if (task.targetStation == null)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			Debug.LogError("No available launch station found!");
			return AIWorker.KeepTaskWaiting(ctx);
		}

		if (carryAbility == null || carryAbility.CarryingBox == null)
		{
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			Debug.LogError("Loading worker has no carried box.");
			return Running;
		}

		task.targetStation.TryGetAddon<CargoStorageAddon>(out var pad);
		if (pad == null || pad.CanStoreCargo(carryAbility.CarryingBox) == false)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			// Future: cargo storage/launch station service should disable and re-enable this worker when capacity opens.
			return AIWorker.KeepTaskWaiting(ctx);
		}

		if (carryAbility.GetBox(out var box) == false || pad.TryStoreCargo(box) == false)
		{
			if (box != null)
				carryAbility.PutBox(box);

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.Worker.ReportBoxHandling(box);
		task.isLoadEnd = true;
		return Success;
	}
}
