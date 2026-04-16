using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static ActionNode;
using static IBaseNode;
using static IBaseNode.NodeState;

public abstract partial class AIWorker
{
	static private WMSystem WMSys => GameContext.Instance.WMSys;
	static private WorkPolicyService WorkPolicyService => GameContext.Instance.WMSys.WorkPolicyService;
	static private HumanIncidentService HumanIncident => GameContext.Instance.HumanIncident;

	protected virtual IBaseNode BuildWorkerBaseNode() { return null; }

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
		context.Worker.SetWorkerAction(WorkerStatusAction.MovingTo);
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

	private static NodeState CheckWorkerHasNoBox(in BTContext context)
	{
		CarryBoxAbility boxStatus = context.Worker.CurrentTask.CarryingAbility;

		if (boxStatus == null)
		{
			Debug.LogError("This Worker Has No BOX ABILITY BUT TRIED TO PICK OR SOMETHING");
			return Failure;
		}

		if (boxStatus.CarringBox != null)
			return Failure;
		return Success;
	}

	private static NodeState SetGoalClosestBoxPool(in BTContext context)
	{
		BoxPool pool = WMSys.BoxPoolMgr.GetClosestAvailablePool(context.Worker.GridPosition);
		context.Worker.SetWorkerTarget(WorkerStatusTarget.BoxPool);

		if (pool == null)
		{
			// todo
			// 사용 가능한 pool이 없는 상태라는 것을 플레이어에게 보여줘야함
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);

			return Failure;
		}

		context.LocalBlackBoard.Set("goalPos", pool.GridPosition);
		context.LocalBlackBoard.Set("targetBoxPool", pool);

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

	private static NodeState PutBox(in BTContext context)
	{
		context.LocalBlackBoard.TryGet<BoxPool>("targetBoxPool", out var pool);

		if (context.Worker.TryDetachBox(out var box) == false)
		{
			// error
			Debug.LogError("Worker tried to put box but has no box attached...");
			return Failure;
		}

		if (pool.PutBox(box) == false)
		{
			// todo
			// pool이 가득 찼다는 것을 플레이어에게 알려야함
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return Failure;
		}

		return Success;
	}

	public static NodeState TaskCompleted(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		//Debug.Log("TaskCompleted!");

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
		SelectorNode node = new();
		SequenceNode moveToAndPick = MoveToTarget(SetGoalClosestBoxPool);
		moveToAndPick.Add(new ActionNode(PickBox));
		
		node.Add(new ActionNode(CheckWorkerHasBox));
		node.Add(moveToAndPick);
		
		return node;
	}

	static public SelectorNode ReturnBox()
	{
		SelectorNode node = new();
		SequenceNode moveToAndReturn = MoveToTarget(SetGoalClosestBoxPool);
		moveToAndReturn.Add(new ActionNode(PutBox));

		node.Add(new ActionNode(CheckWorkerHasNoBox));
		node.Add(moveToAndReturn);

		return node;
	}

	public static SequenceNode MoveToTarget(ActionFunc goalSettingFunc)
	{
		SequenceNode node = new();

		node.Add(new ActionNode(goalSettingFunc));
		node.Add(new ActionNode(SetDestination));
		node.Add(new ActionNode(MoveTo));

		return node;
	}

	// picking, storing에서 목적지를 갱신하며 이동할 때 사용
	public static SequenceNode BuildCarryMoveInteract(BoxType boxRequirement, ActionFunc setGoal, ActionFunc interact)
	{
		SequenceNode node = new();

		if (boxRequirement != BoxType.None) node.Add(GetBox(boxRequirement));
		node.Add(MoveToTarget(setGoal));
		if (interact != null) node.Add(new ActionNode(interact));

		return node;
	}

	public static SequenceNode BuildWorkTimeInteract(WorkActionType actionType, ActionFunc interact)
	{
		SequenceNode node = new();

		var setWorkTime = new ActionNode((in BTContext ctx) => {
			ctx.LocalBlackBoard.Set(actionType.ToString(), WorkPolicyService.GetWorkTime(ctx.Worker, actionType));
			return Success;
		});
		var work = new DoWorkNode(actionType);

		var calculateFatigue = new ActionNode((in BTContext ctx) => {
			float fatigue = WorkPolicyService.GetWorkFatigue(ctx.Worker, actionType);
			ctx.Worker.AddFatigue(fatigue);
			return Success;
		});

		node.Add(setWorkTime);
		node.Add(work);
		if (interact != null) node.Add(new ActionNode(interact));
		node.Add(calculateFatigue);
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			if (ctx.Worker is HumanWorker == false)
				return Success;

			var res = HumanIncident.TryCreateIncident(ctx.Worker, actionType);

			if (res == null)
				return Success;

			return Failure;
		}));

		return node;
	}

	// for incident
	protected static NodeState CheckWorkerIncident(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<HumanIncidentResponseType>("IncidentState", out var responseType) &&
			responseType != HumanIncidentResponseType.None)
		{
			return Success;
		}

		return Failure;
	}

	protected static NodeState IsIncidentWorkMistake(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<HumanIncidentResponseType>("IncidentState", out var responseType) &&
			responseType != HumanIncidentResponseType.WorkMistake)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.HandlingMistake);
			return Success;
		}

		return Failure;
	}

	protected static NodeState IsIncidentCollapse(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<HumanIncidentResponseType>("IncidentState", out var responseType) &&
			responseType != HumanIncidentResponseType.AbortTask)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.Collapse);

			return Success;
		}

		return Failure;
	}

	protected static NodeState AbortTask(in BTContext ctx)
	{
		//WorkerTask task = ctx.Worker.currentTask;
		//ctx.Worker.SetTask(null);
		
		// task manager에게 대체 worker가 task를 수행해야 한다고 알림

		return Success;
	}

	protected static NodeState EndWorkerIncident(in BTContext ctx)
	{
		ctx.LocalBlackBoard.Set("IncidentState", HumanIncidentResponseType.None);

		return Success;
	}

	protected static SequenceNode BuildHumanIncidentNode()
	{
		SequenceNode root = new SequenceNode();
		
		SequenceNode collapse = new SequenceNode();
		collapse.Add(new ActionNode(IsIncidentCollapse));
		collapse.Add(new ActionNode(AbortTask));

		SequenceNode mistake= new SequenceNode();
		mistake.Add(new ActionNode(IsIncidentWorkMistake));
		mistake.Add(new DoWorkNode(WorkActionType.HandleMistake));

		SelectorNode handleIncident = new SelectorNode();
		handleIncident.Add(collapse);
		handleIncident.Add(mistake);

		root.Add(new ActionNode(CheckWorkerIncident));
		root.Add(handleIncident);

		return root;
	}

}
