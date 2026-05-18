using NUnit.Framework.Constraints;
using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static ActionNode;
using static IBaseNode;
using static IBaseNode.NodeState;

public abstract partial class AIWorker
{
	static private WMSystem WMSys => GameContext.Instance.WMSys;
	static private WorkPolicyService WorkPolicyService => GameContext.Instance.WMSys.WorkPolicyService;
	static private HumanIncidentService HumanIncident => GameContext.Instance.HumanIncident;
	static private WorkerStandbyService StandbyService => GameContext.Instance.WorkerStandbyService;
	private static readonly string RecoveryGoalKey = "RecoveryGoalPos";
	private static readonly string StandbyGoalKey = "StandbyGoalPos";

	protected virtual IBaseNode BuildWorkerBaseNode() { return null; }
	protected static SequenceNode BuildRecoveryNode()
	{
		SequenceNode root = new();
		root.Add(new ActionNode(CheckRecoveryNeeded));
		root.Add(MoveToRecoveryPoint());
		root.Add(new ActionNode(Recover));

		return root;
	}

	protected static SequenceNode BuildStandbyNode()
	{
		SequenceNode root = new();
		root.Add(new ActionNode(CheckStandbyNeeded));
		root.Add(MoveToStandbyPoint());

		return root;
	}

	// AI's basic actions
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
		CarryBoxAbility boxStatus = context.Worker.CarryingAbility;

		if (boxStatus == null)
		{
			Debug.LogError("This Worker Has No BOX ABILITY BUT TRIED TO PICK OR SOMETHING");
			return Failure;
		}

		if (boxStatus.CarryingBox == null)
			return Failure;
		return Success;
	}

	private static NodeState CheckWorkerHasNoBox(in BTContext context)
	{
		CarryBoxAbility boxStatus = context.Worker.CarryingAbility;

		if (boxStatus == null)
		{
			Debug.LogError("This Worker Has No BOX ABILITY BUT TRIED TO PICK OR SOMETHING");
			return Failure;
		}

		if (boxStatus.CarryingBox != null)
			return Failure;
		
		return Success;
	}

	private static NodeState SetGoalClosestBoxPoolPick(in BTContext context)
	{
		BoxPool pool = WMSys.BoxPoolMgr.GetClosestAvailableTarget(context.Worker.GridPosition, InteractionKind.Pick);
		context.LocalBlackBoard.SetTargetBuilding(pool);

		return Success;
	}

	private static NodeState SetGoalClosestBoxPoolPut(in BTContext context)
	{
		BoxPool pool = WMSys.BoxPoolMgr.GetClosestAvailableTarget(context.Worker.GridPosition, InteractionKind.Put);
		context.LocalBlackBoard.SetTargetBuilding(pool);

		return Success;
	}

	private static NodeState PickBox(in BTContext context)
	{
		context.LocalBlackBoard.TryGetTargetBuilding(out var building);
		BoxPool pool = building as BoxPool;

		pool.GetBox(out var box);

		if (box == null)
		{
			// todo
			// pool에 사용 가능한 박스가 없다는 점을 플레이어에게 알려줘야함
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return Failure;
		}

		context.LocalBlackBoard.RemoveTargetBuilding();

		return context.Worker.TryAttachBox(box) ? Success : Failure;
	}

	private static NodeState PutBox(in BTContext context)
	{
		context.LocalBlackBoard.TryGetTargetBuilding(out var building);
		BoxPool pool = building as BoxPool;

		if (context.Worker.TryDetachBox(out var box) == false)
		{
			// error
			Debug.LogError("Worker tried to put box but has no box attached...");
			return Failure;
		}

		context.LocalBlackBoard.RemoveTargetBuilding();

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

		return Success;
	}

	public static NodeState TaskFailed(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskFailed...");

		return Success;
	}

	private static NodeState TryRemoveTargetBuilding(in BTContext ctx)
	{
		ctx.LocalBlackBoard.RemoveTargetBuilding();

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
		SequenceNode moveToAndPick = MoveToTarget(WorkerStatusTarget.BoxPool, InteractionKind.Pick, SetGoalClosestBoxPoolPick);
		moveToAndPick.Add(new ActionNode(PickBox));
		
		node.Add(moveToAndPick);
		
		return node;
	}

	static public SelectorNode ReturnBox()
	{
		SelectorNode node = new();
		SequenceNode moveToAndReturn = MoveToTarget(WorkerStatusTarget.BoxPool, InteractionKind.Put, SetGoalClosestBoxPoolPut);
		moveToAndReturn.Add(new ActionNode(PutBox));

		node.Add(new ActionNode(CheckWorkerHasNoBox));
		node.Add(moveToAndReturn);

		return node;
	}

	public static SequenceNode MoveToTarget(WorkerStatusTarget target, InteractionKind kind, ActionFunc settingTargetBuilding = null)
	{
		SequenceNode node = new();

		if (settingTargetBuilding != null)
			node.Add(new ActionNode(settingTargetBuilding));

		node.Add(new ActionNode((in BTContext ctx) =>
		{
			ctx.Worker.SetWorkerTarget(target);

			if (ctx.LocalBlackBoard.TryGetTargetBuilding(out var building) == false ||
				building == null)
			{
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				ctx.LocalBlackBoard.RemoveTargetBuilding();
				return Failure;
			}

			var interaction = building as IInteractionPoint;
			int3 goalPos = interaction.GetClosestInteractionPoint(kind, in ctx.Worker.position);

			ctx.Worker.routeFinder.enabled = true;
			ctx.Worker.routeFinder.SetGoalPosition(goalPos);

			return Success;

		}));
		node.Add(new ActionNode(MoveTo));

		return node;
	}

	private static SequenceNode MoveToRecoveryPoint()
	{
		SequenceNode node = new();
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			if (ctx.LocalBlackBoard.TryGet(RecoveryGoalKey, out int3 goalPos) == false)
				return Failure;

			ctx.Worker.routeFinder.enabled = true;
			ctx.Worker.routeFinder.SetGoalPosition(goalPos);
			return Success;
		}));
		node.Add(new ActionNode(MoveTo));

		return node;
	}

	private static SequenceNode MoveToStandbyPoint()
	{
		SequenceNode node = new();
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			if (ctx.LocalBlackBoard.TryGet(StandbyGoalKey, out int3 goalPos) == false)
				return Failure;

			ctx.Worker.routeFinder.enabled = true;
			ctx.Worker.routeFinder.SetGoalPosition(goalPos);
			return Success;
		}));
		node.Add(new ActionNode(MoveTo));
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.Idle);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.StandbyZone);
			ctx.LocalBlackBoard.Remove<int3>(StandbyGoalKey);
			return Success;
		}));

		return node;
	}

	public static SelectorNode CheckBoxAndGet(BoxType boxRequirement)
	{
		SelectorNode node = new();
		if (boxRequirement != BoxType.None)
		{
			node.Add(new ActionNode(CheckWorkerHasBox));
			node.Add(GetBox(boxRequirement));
		}
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
		if (interact != null)
		{
			node.Add(new ActionNode(interact));
			node.Add(new ActionNode(TryRemoveTargetBuilding));
		}
		node.Add(calculateFatigue);
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			if (ctx.Worker is HumanWorker == false)
				return Success;

			var res = HumanIncident.TryCreateIncident(ctx.Worker, actionType);

			if (res == null)
				return Success;

			ctx.LocalBlackBoard.Set("IncidentState", res);
			return Success;
		}));

		return node;
	}

	protected static NodeState CheckRecoveryNeeded(in BTContext ctx)
	{
		if (ctx.Worker.CurrentTask != null)
			return Failure;

		if (ctx.Worker.TryCanBeginRecovery(out var recoveryPoint) == false)
			return Failure;

		ctx.Worker.BeginRecovery();
		ctx.LocalBlackBoard.Set(RecoveryGoalKey, recoveryPoint);
		return Success;
	}

	protected static NodeState CheckStandbyNeeded(in BTContext ctx)
	{
		if (ctx.Worker.CurrentTask != null)
			return Failure;

		if (ctx.Worker.NeedsRecovery())
			return Failure;

		switch (StandbyService.TryFindStandbyPoint(ctx.Worker, out var standbyPoint))
		{
			case StandbyPointResult.Success:
				ctx.LocalBlackBoard.Set(StandbyGoalKey, standbyPoint);
				return Success;

			case StandbyPointResult.NoZone:
			case StandbyPointResult.NoAvailablePoint:
				ctx.Worker.SetWorkerTarget(WorkerStatusTarget.StandbyZone);
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				return Running;

			default:
				return Failure;
		}
	}

	protected static NodeState Recover(in BTContext ctx)
	{
		ctx.Worker.SetWorkerAction(ctx.Worker.GetRecoveryAction());
		ctx.Worker.TickRecovery(ctx.DeltaTime);

		if (ctx.Worker.IsRecoveryComplete() == false)
			return Running;

		ctx.Worker.SetWorkerAction(WorkerStatusAction.None);
		ctx.LocalBlackBoard.Remove<int3>(RecoveryGoalKey);
		return Success;
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
