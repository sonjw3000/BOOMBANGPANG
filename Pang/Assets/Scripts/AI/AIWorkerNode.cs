using Unity.Mathematics;
using UnityEngine;
using Assets.Scripts.AI.BT;
using static ActionNode;
using static IBaseNode;
using static IBaseNode.NodeState;

public abstract partial class AIWorker
{
	static private WMSystem WMSys => GameContext.Instance.WMSys;
	static private WorkPolicyService WorkPolicyService => GameContext.Instance.WMSys.WorkPolicyService;
	static private HumanIncidentService HumanIncident => GameContext.Instance.HumanIncident;
	static private AirlockService AirlockService => GameContext.Instance.AirlockSvc;
	private static readonly string TransitAirlockKey = "TransitAirlock";
	private static readonly string TransitDirectionKey = "TransitDirection";
	private static readonly string TransitStartedKey = "TransitStarted";

	protected virtual IBaseNode BuildWorkerBaseNode() { return null; }
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
			context.Worker.routeFinder.ConsumeArrivedGoal();
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
		WMSys.BoxPoolService.TryFindDestination(0, context.Worker.GridPosition, InteractionKind.Pick, FacilityFilter.ForWorker(context.Worker), out BoxPool pool);
		context.LocalBlackBoard.SetTargetBuilding(pool);

		return Success;
	}

	private static NodeState SetGoalClosestBoxPoolPut(in BTContext context)
	{
		WMSys.BoxPoolService.TryFindDestination(0, context.Worker.GridPosition, InteractionKind.Put, FacilityFilter.ForWorker(context.Worker), out BoxPool pool);
		context.LocalBlackBoard.SetTargetBuilding(pool);

		return Success;
	}

	private static NodeState PickBox(in BTContext context)
	{
		context.LocalBlackBoard.TryGetTargetBuilding(out var building);
		BoxPool pool = building as BoxPool;

		if (pool == null)
		{
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(context);
		}

		pool.GetBox(out var box);

		if (box == null)
		{
			WMSys.BoxPoolService.TryFindDestination(0, context.Worker.GridPosition, InteractionKind.Pick, FacilityFilter.ForWorker(context.Worker), out BoxPool nextPool);
			context.LocalBlackBoard.SetTargetBuilding(nextPool);

			if (nextPool != null)
			{
				return TryRouteTowardLogicalTarget(context, nextPool, WorkerStatusTarget.BoxPool, InteractionKind.Pick);
			}

			// todo
			// pool에 사용 가능한 박스가 없다는 점을 플레이어에게 알려줘야함
			// todo worker를 off 후 대기시켜야함
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(context);
		}

		context.LocalBlackBoard.RemoveTargetBuilding();

		return context.Worker.TryAttachBox(box) ? Success : Failure;
	}

	private static NodeState PutBox(in BTContext context)
	{
		context.LocalBlackBoard.TryGetTargetBuilding(out var building);
		BoxPool pool = building as BoxPool;

		if (pool == null)
		{
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(context);
		}

		if (context.Worker.TryDetachBox(out var box) == false)
		{
			// error
			Debug.LogError("Worker tried to put box but has no box attached...");
			return Failure;
		}

		context.LocalBlackBoard.RemoveTargetBuilding();

		if (pool.PutBox(box) == false)
		{
			if (context.Worker.TryAttachBox(box) == false)
			{
				Debug.LogError($"Worker failed to retain rejected box {box.Type} #{box.BoxId}.");
				return Failure;
			}

			// todo
			// pool이 가득 찼다는 것을 플레이어에게 알려야함
			// todo worker를 off 후 대기시켜야함
			context.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(context);
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
				if (settingTargetBuilding != null)
				{
					NodeState targetResult = settingTargetBuilding(ctx);
					if (targetResult == Running || targetResult == Failure || targetResult == Abort)
						return targetResult;

					ctx.LocalBlackBoard.TryGetTargetBuilding(out building);
				}
			}

			if (building == null)
			{
				// todo worker를 off 후 대기시켜야함
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				ctx.LocalBlackBoard.RemoveTargetBuilding();
				return KeepTaskWaiting(ctx);
			}

			bool hasTransit = ctx.LocalBlackBoard.TryGet(TransitAirlockKey, out Airlock airlock) && airlock != null;

			if (ctx.Worker.routeFinder.HasActiveGoal)
			{
				if (ctx.Worker.routeFinder.IsGoal)
				{
					ctx.Worker.routeFinder.enabled = false;
					ctx.Worker.routeFinder.ConsumeArrivedGoal();

					if (hasTransit == false)
						return Success;

					NodeState transitResult = TryUseTransitAirlockIfNeeded(ctx);
					if (transitResult == Running || transitResult == Failure || transitResult == Abort)
						return transitResult;

					NodeState rerouteResult = TryRouteTowardLogicalTarget(ctx, building, target, kind);
					if (rerouteResult != Success)
						return rerouteResult;

					return Running;
				}

				ctx.Worker.enabled = false;
				ctx.Worker.SetWorkerAction(WorkerStatusAction.MovingTo);
				return Running;
			}

			if (hasTransit)
			{
				NodeState transitResult = TryUseTransitAirlockIfNeeded(ctx);
				if (transitResult == Running || transitResult == Failure || transitResult == Abort)
					return transitResult;

				NodeState rerouteResult = TryRouteTowardLogicalTarget(ctx, building, target, kind);
				if (rerouteResult != Success)
					return rerouteResult;

				return Running;
			}

			NodeState routeResult = TryRouteTowardLogicalTarget(ctx, building, target, kind);
			if (routeResult != Success)
				return routeResult;

			return Running;
		}));

		return node;
	}

	private static NodeState TryRouteTowardLogicalTarget(
		in BTContext ctx,
		IGridPlaceable targetPlaceable,
		WorkerStatusTarget finalTarget,
		InteractionKind interactionKind)
	{
		if (targetPlaceable == null)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(ctx);
		}

		if (TryGetBuildingId(ctx.Worker.GridPosition, out uint currentBuildingId) == false)
			currentBuildingId = 0;

		if (TryGetBuildingId(targetPlaceable.GridPosition, out uint targetBuildingId) == false)
			targetBuildingId = 0;

		if (currentBuildingId == targetBuildingId)
		{
			if (TryRouteToInteractionPoint(ctx, targetPlaceable, finalTarget, interactionKind))
			{
				ClearTransitState(ctx.LocalBlackBoard);
				return Success;
			}

			if (currentBuildingId != 0 && TryRouteToAirlock(ctx, currentBuildingId, AirlockDirection.InsideToOutside))
				return Success;
		}
		else
		{
			if (currentBuildingId != 0)
			{
				if (TryRouteToAirlock(ctx, currentBuildingId, AirlockDirection.InsideToOutside))
					return Success;
			}
			else
			{
				if (TryRouteToInteractionPoint(ctx, targetPlaceable, finalTarget, interactionKind))
				{
					ClearTransitState(ctx.LocalBlackBoard);
					return Success;
				}

				if (targetBuildingId != 0 && TryRouteToAirlock(ctx, targetBuildingId, AirlockDirection.OutsideToInside))
					return Success;
			}
		}

		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return KeepTaskWaiting(ctx);
	}

	private static bool TryRouteToInteractionPoint(
		in BTContext ctx,
		IGridPlaceable targetPlaceable,
		WorkerStatusTarget finalTarget,
		InteractionKind interactionKind)
	{
		if (targetPlaceable is not IInteractionPoint interaction)
			return false;

		if (InteractionPointSelector.TryGetInteractionPoint(
			interaction,
			interactionKind,
			ctx.Worker.position,
			out int3 goalPos,
			out _) == false)
		{
			return false;
		}

		ctx.Worker.SetWorkerTarget(finalTarget);
		ctx.Worker.routeFinder.enabled = true;
		ctx.Worker.routeFinder.SetGoalPosition(goalPos);
		return true;
	}

	private static bool TryRouteToAirlock(
		in BTContext ctx,
		uint buildingId,
		AirlockDirection direction)
	{
		if (AirlockService == null || buildingId == 0)
			return false;

		if (AirlockService.TryFindDestination(buildingId, ctx.Worker.GridPosition, InteractionKind.Enter, FacilityFilter.ForWorker(ctx.Worker), out Airlock airlock) == false || airlock == null)
			return false;

		if (InteractionPointSelector.TryGetInteractionPoint(
			airlock,
			InteractionKind.Enter,
			ctx.Worker.position,
			out int3 goalPos,
			out _) == false)
		{
			return false;
		}

		ctx.LocalBlackBoard.Set(TransitAirlockKey, airlock);
		ctx.LocalBlackBoard.Set(TransitDirectionKey, direction);
		ctx.LocalBlackBoard.Set(TransitStartedKey, false);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Airlock);
		ctx.Worker.routeFinder.enabled = true;
		ctx.Worker.routeFinder.SetGoalPosition(goalPos);
		return true;
	}

	private static NodeState TryUseTransitAirlockIfNeeded(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet(TransitAirlockKey, out Airlock airlock) == false || airlock == null)
			return Success;

		if (ctx.LocalBlackBoard.TryGet(TransitDirectionKey, out AirlockDirection direction) == false)
		{
			ClearTransitState(ctx.LocalBlackBoard);
			return Failure;
		}

		bool started = ctx.LocalBlackBoard.TryGet(TransitStartedKey, out bool transitStarted) && transitStarted;
		if (started)
		{
			if (HasCompletedTransit(ctx.Worker, airlock, direction))
			{
				ClearTransitState(ctx.LocalBlackBoard);
				return Success;
			}

			if (airlock.IsAvailable)
			{
				ClearTransitState(ctx.LocalBlackBoard);
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				return KeepTaskWaiting(ctx);
			}

			return Running;
		}

		if (AirlockService.TryReserve(airlock, ctx.Worker, direction) == false)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(ctx);
		}

		if (AirlockService.TryBeginEntry(airlock, ctx.Worker) == false)
		{
			AirlockService.Release(airlock, ctx.Worker);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return KeepTaskWaiting(ctx);
		}

		ctx.LocalBlackBoard.Set(TransitStartedKey, true);
		return Running;
	}

	private static bool HasCompletedTransit(AIWorker worker, Airlock airlock, AirlockDirection direction)
	{
		return airlock != null && airlock.HasCompletedTransit(worker, direction);
	}

	private static bool TryGetBuildingId(in int3 position, out uint buildingId)
	{
		GridCell cell = GridService?.GetCell(position);
		buildingId = cell != null ? cell.BuildingId : 0;
		return cell != null;
	}

	private static void ClearTransitState(BlackBoard blackBoard)
	{
		if (blackBoard == null)
			return;

		blackBoard.Remove<Airlock>(TransitAirlockKey);
		blackBoard.Remove<AirlockDirection>(TransitDirectionKey);
		blackBoard.Remove<bool>(TransitStartedKey);
	}

	public static NodeState KeepTaskWaiting(in BTContext ctx)
	{
		return Running;
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

			ctx.LocalBlackBoard.Set("IncidentState", res.responseType);
			return Success;
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
			responseType == HumanIncidentResponseType.WorkMistake)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.HandlingMistake);
			return Success;
		}

		return Failure;
	}

	protected static NodeState IsIncidentCollapse(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<HumanIncidentResponseType>("IncidentState", out var responseType) &&
			responseType == HumanIncidentResponseType.AbortTask)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.Collapse);

			return Success;
		}

		return Failure;
	}

	protected static NodeState AbortTask(in BTContext ctx)
	{
		return ctx.Worker.EnterIncapacitatedState(WorkerOperationalState.Knockout)
			? Success
			: Failure;
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
		mistake.Add(new ActionNode(EndWorkerIncident));

		SelectorNode handleIncident = new SelectorNode();
		handleIncident.Add(collapse);
		handleIncident.Add(mistake);

		root.Add(new ActionNode(CheckWorkerIncident));
		root.Add(handleIncident);

		return root;
	}

}
