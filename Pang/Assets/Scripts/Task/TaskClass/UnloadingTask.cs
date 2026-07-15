using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class UnloadingTask : WorkerTask
{
	private Rocket targetRocket;
	private CargoPort cargoPort;

	private bool IsUnloadEnd = false;

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

		SelectorNode root = new();

		SequenceNode resume = new();
		resume.Add(new ActionNode(CheckWorkerCarriesPayload));
		resume.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetZoneTarget));
		resume.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, PutOnBuffer));
		root.Add(resume);

		SequenceNode start = new();
		start.Add(AIWorker.ReturnBox());
		start.Add(AIWorker.MoveToTarget(WorkerStatusTarget.Rocket, InteractionKind.Pick, SetRocketTarget));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, UnloadFromRocket));
		start.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Put, SetZoneTarget));
		start.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, PutOnBuffer));
		root.Add(start);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return IsUnloadEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return CanDispatchToWorkerZones(worker, targetRocket, cargoPort);
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
		if (task.cargoPort == null || task.cargoPort.CanPutBox() == false)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.cargoPort);
		return Success;
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
			return AIWorker.KeepTaskWaiting(ctx);
		}

		if (task.WorkerCarryBox.GetBox(out BoxBase box) == false)
			return Failure;

		if (task.cargoPort.PutBox(box))
		{
			task.IsUnloadEnd = true;
			return Success;
		}

		task.WorkerCarryBox.PutBox(box);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return AIWorker.KeepTaskWaiting(ctx);
	}

}
