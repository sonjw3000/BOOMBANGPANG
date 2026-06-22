using System.Collections.Generic;
using System;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class UnloadingTask : WorkerTask
{
	private Rocket targetRocket;
	private CargoPort cargoPort;

	private bool IsUnloadEnd = false;

	static private CargoPortService CargoPortService => GameContext.Instance.CargoPortSvc;

	public UnloadingTask(Rocket rocket, CargoPort cargoPort = null) : base(TaskType.Unloading)
	{
		targetRocket = rocket;
		this.cargoPort = cargoPort;
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
		// 1. 로켓 이동
		// 2. 화물 하역
		// 3. inboundmanager의 bufferzone으로 이동
		// 4. payload를 zone에 올리기
		// 5. 완료

		SequenceNode root = new();

		root.Add(AIWorker.ReturnBox());
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.Rocket, InteractionKind.Pick, SetRocketTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, UnloadFromRocket));
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetZoneTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, PutOnBuffer));

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return IsUnloadEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[UnloadingTask] RocketPos: {targetRocket.GridPosition}";
	}
#endif

	public override string GetStatusSummary()
	{
		string rocketName = targetRocket != null ? targetRocket.name : "None";
		string portName = cargoPort != null ? cargoPort.name : "None";
		string progressText = IsUnloadEnd ? "Unload complete." : "Moving cargo from rocket to cargo port.";
		return $"Rocket: {rocketName}\nPort: {portName} / {progressText}";
	}

	// 
	public static NodeState SetRocketTarget(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;
		ctx.LocalBlackBoard.SetTargetBuilding(task.targetRocket);

		return Success;
	}

	public static NodeState UnloadFromRocket(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;
		Rocket rocket = task.targetRocket;

		if (rocket == null)
		{
			// todo 여기서 task를 end 해야함 failled로
			Debug.Log("No rocket here!!!!!!");
			return Failure;
		}

		AIWorker worker = ctx.Worker;
		if (worker.CarryingAbility == null || worker.CarryingAbility.CarryingBox != null)
			return Failure;

		if (rocket.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				rocket.PutBox(box);
			return Failure;
		}

		if (rocket.CanGetBox() == false)
			GameContext.Instance.RocketSvc.DisableRocket(task.targetRocket);

		return Success;
	}

	public static NodeState SetZoneTarget(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;
		BoxBase box = task.WorkerCarryBox?.CarryingBox;
		if (task.cargoPort == null)
			task.cargoPort = CargoPortService.FindClosestAvailablePortForBox(
				ctx.Worker.GridPosition,
				InteractionKind.Put,
				box,
				predicate: candidate => candidate is InboundCargoPort);

		ctx.LocalBlackBoard.SetTargetBuilding(task.cargoPort);
		if (task.cargoPort != null)
			return Success;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return AIWorker.MoveToStandbyWhileWaiting(ctx);
	}

	public static NodeState PutOnBuffer(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		if (task.cargoPort == null)
		{
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			Debug.Log("No Cargoport Available!!");
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		if (task.WorkerCarryBox.GetBox(out BoxBase box) == false)
			return Failure;

		if (task.cargoPort.PutBox(box))
		{
			task.IsUnloadEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		return Failure;
	}

}
