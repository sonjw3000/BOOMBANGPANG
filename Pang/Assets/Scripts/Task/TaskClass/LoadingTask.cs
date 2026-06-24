using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class LoadingTask : WorkerTask
{
	private bool isLoadEnd = false;

	private CargoPort targetPort = null;

	static private LaunchStationService LaunchStationService => GameContext.Instance.OBWorkflowSvc.LaunchStationService;
	static private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	static private GridService GridService => GameContext.Instance.GridService;

	public LoadingTask(CargoPort cargoPort) : base(TaskType.Loading)
	{
		this.targetPort = cargoPort;
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
		SequenceNode root = new();

		root.Add(AIWorker.ReturnBox());
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Pick, SetLoadTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, PickCargo));

		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.LaunchStation, InteractionKind.Put, SetLaunchStation));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, StoreCargo));

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isLoadEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return CanDispatchToWorkerZones(worker, targetPort);
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
			return $"CargoPort: {targetPort?.name ?? "None"}\nLoading complete.";

		return $"CargoPort: {targetPort?.name ?? "None"}\nMoving cargo to launch station.";
	}

	static private NodeState SetLoadTarget(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask as LoadingTask;
		ctx.LocalBlackBoard.SetTargetBuilding(task.targetPort);

		return Success;
	}

	static private NodeState PickCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);

		if (task.targetPort == null)
		{
			Debug.LogError("No available load port found!");
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		if (ctx.Worker.CarryingAbility == null || ctx.Worker.CarryingAbility.CarryingBox != null)
			return Failure;

		if (task.targetPort.GetBox(out BoxBase box) == false || ctx.Worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				task.targetPort.PutBox(box);

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		return Success;
	}

	static private NodeState SetLaunchStation(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(ctx.Worker.CarryingAbility?.CarryingBox, ctx.Worker);
		var launchStation = GetLaunchStationForTask(task, ctx.Worker.GridPosition, InteractionKind.Put, zoneFilter);

		ctx.LocalBlackBoard.SetTargetBuilding(launchStation);
		return Success;
	}

	static private NodeState StoreCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var carryAbility = ctx.Worker.CarryingAbility;
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(carryAbility?.CarryingBox, ctx.Worker);
		var launchStation = GetLaunchStationForTask(task, ctx.Worker.GridPosition, InteractionKind.Pick, zoneFilter);
		if (launchStation == null)
		{
			// Future: launch station service should wake this worker when storage has room.
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			Debug.LogError("No available launch station found!");
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		if (carryAbility == null || carryAbility.CarryingBox == null)
		{
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			Debug.LogError("Loading worker has no carried box.");
			return Running;
		}

		launchStation.TryGetAddon<CargoStorageAddon>(out var pad);
		if (pad == null || pad.CanStoreCargo(carryAbility.CarryingBox) == false)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			// Future: cargo storage/launch station service should disable and re-enable this worker when capacity opens.
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		if (carryAbility.GetBox(out var box) == false || pad.TryStoreCargo(box) == false)
		{
			if (box != null)
				carryAbility.PutBox(box);

			return Failure;
		}

		task.isLoadEnd = true;
		return Success;
	}

	private static LaunchStation GetLaunchStationForTask(LoadingTask task, in Unity.Mathematics.int3 from, InteractionKind interactionKind, ZoneFilter zoneFilter)
	{
		if (task?.targetPort != null)
		{
			GridCell targetCell = GridService?.GetCell(task.targetPort.GridPosition);
			if (targetCell != null && targetCell.BuildingId != 0)
			{
				LaunchStationService.TryFindDestination(targetCell.BuildingId, from, interactionKind, zoneFilter, out LaunchStation localStation);
				if (localStation != null)
					return localStation;
			}
		}

		LaunchStationService.TryFindDestination(0, from, interactionKind, zoneFilter, out LaunchStation globalStation);
		return globalStation;
	}
}
