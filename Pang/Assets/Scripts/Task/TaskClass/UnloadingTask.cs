using System.Collections.Generic;
using System;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public class UnloadingTask : WorkerTask
{
	private Rocket targetRocket;
	private CargoPort cargoPort;

	private bool IsUnloadEnd = false;

	static private CargoPortService PortService => GameContext.Instance.IBWorkflowMgr.CargoPorts;

	public UnloadingTask(Rocket rocket) : base(TaskType.Unloading)
	{
		targetRocket = rocket;
	}

	public UnloadingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new UnloadingTaskSaveData
		{
			TargetRocketId = targetRocket != null && getPlaceableId != null ? getPlaceableId(targetRocket.gameObject) : -1,
			CargoPortId = cargoPort != null && getPlaceableId != null ? getPlaceableId(cargoPort.gameObject) : -1,
			IsUnloadEnd = IsUnloadEnd,
		};
	}

	public void RestoreState(CargoPort cargoPort, bool isUnloadEnd)
	{
		this.cargoPort = cargoPort;
		IsUnloadEnd = isUnloadEnd;
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

		root.Add(AIWorker.CheckBoxAndGet(BoxType.Cargo));
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

		// items를 worker에게 건내줘야함
		AIWorker worker = ctx.Worker;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
		{
			Debug.Log("No Box OMG!!");
			return Failure;
		}
		
		TransferResultKind result = ItemTransferUtility.MoveAllStacks(new(rocket, box));

		// todo
		// 새로운 작업이 필요할것이다
		
		// disable rocket
		if (result == TransferResultKind.Complete)
			GameContext.Instance.RocketMgr.DisableRocket(task.targetRocket);
		else if (result == TransferResultKind.Partial)
		{
			// todo
			// add new task to unload remaining items
			UnloadingTask newTask = new(task.targetRocket);
			GameContext.Instance.TaskMgr.EnqueueTask(newTask);
		}
		else
		{
			return Failure;
		}

		return Success;
	}

	public static NodeState SetZoneTarget(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;
		task.cargoPort = PortService.GetClosestAvailableTarget(ctx.Worker.GridPosition, InteractionKind.Put);

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
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		// load on cargoport

		BoxBase box = task.WorkerCarryBox.CarryingBox;

		TransferResultKind result = ItemTransferUtility.MoveAllStacks(new(box, task.cargoPort));

		if (result == TransferResultKind.Complete)
		{
			task.IsUnloadEnd = true;
			return Success;
		}

		return result == TransferResultKind.Partial ? Running : Failure;
	}

}
