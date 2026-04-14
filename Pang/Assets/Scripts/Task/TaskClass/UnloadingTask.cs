using System.Collections.Generic;
using Unity.Mathematics;
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

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();

		if (carryBox == null)
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

		SequenceNode moveToRocket = AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Cargo,
			setGoal: SetRocketTarget,
			interact: null);

		SequenceNode moveToCargoPort = AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Cargo,
			setGoal: SetZoneTarget,
			interact: null);

		root.Add(moveToRocket);
		root.Add(AIWorker.BuildWorkTimeInteract(
				"PickCargo",
				SetPickTime,
				null));
		root.Add(AIWorker.BuildWorkTimeInteract("PickTime", SetPickTime, UnloadFromRocket));

		root.Add(moveToCargoPort);
		root.Add(AIWorker.BuildWorkTimeInteract(
				"PutCargo",
				SetPutTime,
				null));
		root.Add(AIWorker.BuildWorkTimeInteract("PutTime", SetPutTime, PutOnBuffer));

		root.Add(new ActionNode(SetTaskEnd));

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

		if (task.targetRocket == null)
		{
			// todo
			// rocket이 파괴되었을수도 있다 이 때 task를 파괴한다

			return Failure;
		}

		ctx.LocalBlackBoard.Set<int3>("goalPos", task.targetRocket.GetClosestInteractionPoint(InteractionKind.Pick, ctx.Worker.GridPosition));

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

		BoxBase box = worker.GetComponent<CarryBoxAbility>().CarringBox;
		if (box == null)
		{
			Debug.Log("No Box OMG!!");
			return Failure;
		}
		
		var items = rocket.GetPayload();
		box.AddItem(items);

		// todo
		// 새로운 작업이 필요할것이다
		
		// disable rocket
		if (items.Count == 0)
			GameContext.Instance.RocketMgr.DisableRocket(task.targetRocket);
		else
		{
			// todo
			// add new task to unload remaining items
			UnloadingTask newTask = new(task.targetRocket);
			GameContext.Instance.TaskMgr.EnqueueTask(newTask);
		}

		return Success;
	}

	public static NodeState SetZoneTarget(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		task.cargoPort = PortService.GetClosestAvailablePort(ctx.Worker.GridPosition);

		if (task.cargoPort == null)
		{
			Debug.Log("No Cargoport Available!!");
			return Failure;
		}
		
		ctx.LocalBlackBoard.Set<int3>("goalPos", task.cargoPort.GetClosestInteractionPoint(InteractionKind.Put, ctx.Worker.GridPosition));

		return Success;
	}

	public static NodeState PutOnBuffer(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		if (task.cargoPort == null)
		{
			Debug.Log("No Cargoport Available!!");
			return Failure;
		}

		// load on cargoport

		BoxBase box = task.carryBox.CarringBox;

		List<uint> ids = new(box.Stacks.Count);
		List<int> cnts = new(box.Stacks.Count);

		foreach (var stack in box.Stacks)
		{
			ids.Add(stack.ItemID);
			cnts.Add(stack.Quantity);
		}

		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			int befStack = cnts[i];
			int moveTocargo = task.cargoPort.AddItem(ids[i], cnts[i]);

			box.RemoveItem(ids[i], moveTocargo);
		}

		return Success;
	}

	public static NodeState SetTaskEnd(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;
		task.IsUnloadEnd = true;

		return Success;
	}

	public static NodeState SetPickTime(in BTContext ctx)
	{
		ctx.LocalBlackBoard.Set("PickCargo", WorkPolicyService.GetWorkTime(ctx.Worker));
		return Success;
	}

	public static NodeState SetPutTime(in BTContext ctx)
	{
		ctx.LocalBlackBoard.Set("PutCargo", WorkPolicyService.GetWorkTime(ctx.Worker));
		return Success;
	}

}