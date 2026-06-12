using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public class LoadingTask : WorkerTask
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

	public LoadingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new LoadingTaskSaveData
		{
			TargetPortId = targetPort != null && getPlaceableId != null ? getPlaceableId(targetPort.gameObject) : -1,
			IsLoadEnd = isLoadEnd,
		};
	}

	public void RestoreState(bool isLoadEnd)
	{
		this.isLoadEnd = isLoadEnd;
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

		root.Add(AIWorker.CheckBoxAndGet(BoxType.Cargo));
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

		BoxBase box = ctx.Worker.CarryingAbility?.CarryingBox;

		if (box == null)
			return Failure;

		if (task.targetPort.Stacks.Count <= 0)
		{
			task.targetPort.SetInputReady(true);
			task.isLoadEnd = true;
			return Success;
		}

		TransferResultKind result = ItemTransferUtility.MoveAllStacks(new(task.targetPort, box, ReportWaitingForShipping));
		if (result == TransferResultKind.Complete)
		{
			task.targetPort.SetInputReady(true);
			return Success;
		}

		if (result == TransferResultKind.Partial)
		{
			TaskMgr.EnqueueTask(new LoadingTask(task.targetPort));
			return Success;
		}

		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
		// Future: replace this polling standby with a box/cargo-port service wake-up when a suitable carrier is available.
		return AIWorker.MoveToStandbyWhileWaiting(ctx);
	}

	private static void ReportWaitingForShipping(ItemStack stack)
	{
		if (stack is ItemPackage pkg == false)
		{
			Debug.LogError("Not ItemStack In OB CargoPort!!!");
			return;
		}

		pkg.ReportOutboundProgress(GameContext.Instance.OrderMgr, PackageOutboundStage.WaitingForShipping);
	}

	static private NodeState SetLaunchStation(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var launchStation = GetLaunchStationForTask(task, ctx.Worker.GridPosition, InteractionKind.Put);

		ctx.LocalBlackBoard.SetTargetBuilding(launchStation);
		return Success;
	}

	static private NodeState StoreCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var carryAbility = ctx.Worker.CarryingAbility;
				
		var launchStation = GetLaunchStationForTask(task, ctx.Worker.GridPosition, InteractionKind.Pick);
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

	private static LaunchStation GetLaunchStationForTask(LoadingTask task, in Unity.Mathematics.int3 from, InteractionKind interactionKind)
	{
		if (task?.targetPort != null)
		{
			GridCell targetCell = GridService?.GetCell(task.targetPort.GridPosition);
			if (targetCell != null && targetCell.BuildingId != 0)
			{
				LaunchStation localStation = LaunchStationService.GetClosestAvailableTarget(targetCell.BuildingId, from, interactionKind);
				if (localStation != null)
					return localStation;
			}
		}

		return LaunchStationService.GetClosestAvailableTarget(from, interactionKind);
	}
}
