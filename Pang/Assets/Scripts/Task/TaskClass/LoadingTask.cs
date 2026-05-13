using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public class LoadingTask : WorkerTask
{
	private bool isLoadEnd = false;

	private CargoPort targetPort = null;

	static private LaunchStationService LaunchStations => GameContext.Instance.OBWorkflowMgr.LaunchStations;

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
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return Failure;
		}

		BoxBase box = ctx.Worker.CarryingAbility?.CarryingBox;

		task.targetPort.MoveToBox(box);
		task.targetPort.SetInputReady(true);

		return Success;
	}

	static private NodeState SetLaunchStation(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var launchStation = LaunchStations.GetClosestAvailableTarget(ctx.Worker.GridPosition, InteractionKind.Put);

		ctx.LocalBlackBoard.SetTargetBuilding(launchStation);
		return Success;
	}

	static private NodeState StoreCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var carryAbility = ctx.Worker.CarryingAbility;
				
		var launchStation = LaunchStations.GetClosestAvailableTarget(ctx.Worker.GridPosition, InteractionKind.Pick);
		if (launchStation == null)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.LaunchStation);
			Debug.LogError("No available launch station found!");
			return Failure;
		}

		if (carryAbility == null || carryAbility.CarryingBox == null)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			Debug.LogError("Loading worker has no carried box.");
			return Failure;
		}

		launchStation.TryGetAddon<CargoStorageAddon>(out var pad);

		if (carryAbility.GetBox(out var box))
			pad.StoreCargo(box);

		task.isLoadEnd = true;
		return Success;
	}
}
