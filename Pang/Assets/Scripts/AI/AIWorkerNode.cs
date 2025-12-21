using Unity.Mathematics;
using UnityEngine;
using static ActionNode;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class AIWorker
{
	static private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	static private WMSystem WMSys => GameContext.Instance.WMSys;
	// AI's basic actions
	private static NodeState SetDestination(in BTContext context)
	{
		// for real
		context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
		context.Worker.routeFinder.enabled = true;
		context.Worker.routeFinder.SetGoalPosition(goalPos);

		return Success;
	}

	public static NodeState CheckFulfilled(in BTContext ctx)
	{
		if (ctx.Worker.CurrentTask.CheckTaskEnd())
			return Success;

		return Failure;
	}

	private static NodeState MoveTo(in BTContext context)
	{
		if (context.Worker.routeFinder.IsGoal)
		{
			//Debug.Log("Goal Hit!");
			context.Worker.routeFinder.enabled = false;
			return Success;
		}
		context.Worker.enabled = false;

		return Running;
	}

	private static NodeState DoWork(in BTContext context)
	{
		if (context.Worker.CurrentTask == null)
			return Failure;

		return context.Worker.CurrentTask.UpdateTaskNode(context);
	}

	private static NodeState CheckWorkerHasBox(in BTContext context)
	{
		CarryBoxAbility boxStatus = context.Worker.CurrentTask.CarryingAbility;

		if (boxStatus == null)
		{
			Debug.LogError("This Worker Has No BOX ABILITY BUT TRIED TO PICK OR SOMETHING");
			return Failure;
		}

		if (boxStatus.CarringBox == null)
			return Failure;
		return Success;
	}

	private static NodeState SetGoalClosestBoxPool(in BTContext context)
	{
		BoxPool pool = WMSys.BoxPoolMgr.GetClosestAvailablePool(context.Worker.GridPosition);

		if (pool == null)
		{
			// todo
			// 사용 가능한 pool이 없는 상태라는 것을 플레이어에게 보여줘야함
			return Failure;
		}

		context.LocalBlackBoard.Set<int3>("goalPos", pool.GridPosition);
		context.LocalBlackBoard.Set<BoxPool>("targetBoxPool", pool);

		return Success;
	}

	private static NodeState PickBox(in BTContext context)
	{
		context.LocalBlackBoard.TryGet<BoxPool>("targetBoxPool", out var pool);
		pool.GetBox(out var box);

		if (box == null)
		{
			// todo
			// pool에 사용 가능한 박스가 없다는 점을 플레이어에게 알려줘야함
			return Failure;
		}

		return context.Worker.TryAttachBox(box) ? Success : Failure;
	}

	public static NodeState TaskCompleted(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskCompleted!");

		// todo
		// 이벤트로 만들어보자
		// task end actions

		WorkerMgr.AddIdleWorker(ctx.Worker);

		return Success;
	}

	public static NodeState TaskFailed(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskFailed...");

		return Success;
	}

	// for tote box getting
	// tote를 가지고 오기 위해 만든 노드
	// picking이던 뭐던 잘 쓰면 된다
	public static SelectorNode GetBox(BoxType type)
	{
		// todo
		// boxtype에 대한 판단을 하게 해주어야함
		SelectorNode node = new SelectorNode();
		SequenceNode moveToAndPick = MoveToTarget(SetGoalClosestBoxPool);
		moveToAndPick.Add(new ActionNode(PickBox));
		
		node.Add(new ActionNode(CheckWorkerHasBox));
		node.Add(moveToAndPick);
		
		return node;
	}

	public static SequenceNode MoveToTarget(ActionFunc goalSettingFunc)
	{
		SequenceNode node = new SequenceNode();

		node.Add(new ActionNode(goalSettingFunc));
		node.Add(new ActionNode(SetDestination));
		node.Add(new ActionNode(MoveTo));

		return node;
	}

	// picking, storing에서 목적지를 갱신하며 이동할 때 사용
	public static SequenceNode BuildCarryMoveInteract(BoxType boxRequirement, ActionFunc setGoal, ActionFunc interact)
	{
		SequenceNode node = new SequenceNode();

		if (boxRequirement != BoxType.None) node.Add(GetBox(boxRequirement));
		node.Add(MoveToTarget(setGoal));
		node.Add(new ActionNode(interact));

		return node;
	}

}
