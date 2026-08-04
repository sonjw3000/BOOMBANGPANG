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
	protected static SequenceNode BuildRecoveryNode(
		WorkerStatusTarget target,
		InteractionKind interactionKind)
	{
		SequenceNode root = new();
		root.Add(new ActionNode(CheckRecoveryNeeded));
		root.Add(MoveToTarget(target, interactionKind));
		root.Add(new ActionNode(Recover));
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
		node.Add(new ActionNode(WaitForPendingIncident));

		if (settingTargetBuilding != null)
			node.Add(new ActionNode(settingTargetBuilding));

		node.Add(new ActionNode((in BTContext ctx) =>
		{
			ctx.Worker.SetWorkerTarget(target);

			if (ctx.Worker.routeFinder.CurrentMovementState == FindRoute.MovementState.Failed)
			{
				ctx.Worker.routeFinder.CancelCurrentRoute();
				ClearTransitState(ctx.LocalBlackBoard);
				ctx.LocalBlackBoard.RemoveTargetBuilding();
				if (ctx.Worker.IsRecovering)
					ctx.Worker.CancelRecovery(true);
				return Failure;
			}

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
					{
						ctx.Worker.ApplyCarriedMovementFatigue(ctx.Worker.routeFinder.ConsumeTravelledCells());
						return Success;
					}

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

		if (ctx.Worker.IsRecovering)
		{
			ctx.Worker.CancelRecovery(true);
			return Failure;
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

		int3 goalPos = default;
		bool foundReservedPoint =
			targetPlaceable is IWorkerInteractionReservation reservation &&
			reservation.TryGetReservedInteractionPoint(
				ctx.Worker,
				interactionKind,
				out goalPos);

		if (foundReservedPoint)
		{
			if (GridService.IsSameRegion(ctx.Worker.position, goalPos) == false)
				return false;
		}
		else if (InteractionPointSelector.TryGetInteractionPoint(
				interaction,
				interactionKind,
				ctx.Worker.position,
				out goalPos,
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
		AirlockDirection direction,
		bool includeBusy = false)
	{
		if (AirlockService == null || buildingId == 0)
			return false;

		if (AirlockService.TryFindTransitDestination(
			buildingId,
			ctx.Worker.GridPosition,
			InteractionKind.Enter,
			FacilityFilter.ForWorker(ctx.Worker),
			includeBusy,
			out Airlock airlock) == false || airlock == null)
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

	private static NodeState CheckRecoveryNeeded(in BTContext ctx)
	{
		if (ctx.Worker.CurrentTask != null)
			return Failure;

		return ctx.Worker.TryCanBeginRecovery() ? Success : Failure;
	}

	private static NodeState Recover(in BTContext ctx)
	{
		if (ctx.Worker.IsRecoveryReservationValid() == false ||
			ctx.Worker.TryBeginRecoveryUse() == false)
		{
			ctx.Worker.CancelRecovery(true);
			return Failure;
		}

		ctx.Worker.SetWorkerAction(ctx.Worker.GetRecoveryAction());
		ctx.Worker.TickRecovery(ctx.Worker.GetEffectiveRecoveryPerSecond(), ctx.DeltaTime);
		if (ctx.Worker.IsRecoveryComplete() == false)
			return Running;

		ctx.Worker.SetWorkerAction(WorkerStatusAction.None);
		ctx.Worker.CompleteRecovery();
		return Success;
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
		node.Add(new ActionNode(WaitForPendingIncident));

		var setWorkTime = new ActionNode((in BTContext ctx) => {
			ctx.LocalBlackBoard.Set(actionType.ToString(), WorkPolicyService.GetWorkTime(ctx.Worker, actionType));
			return Success;
		});
		var work = new DoWorkNode(actionType);

		node.Add(setWorkTime);
		node.Add(work);
		if (interact != null)
		{
			node.Add(new ActionNode((in BTContext ctx) =>
			{
				ctx.Worker.ClearPendingWorkHandling();
				NodeState interactionResult = interact(ctx);
				bool hasHandling = ctx.Worker.TryConsumePendingWorkHandling(out HumanWorkHandlingResult handling);
				if (hasHandling || interactionResult == Success)
					ApplyCompletedWork(ctx, actionType, in handling);
				return interactionResult;
			}));
			node.Add(new ActionNode(TryRemoveTargetBuilding));
		}
		else
		{
			node.Add(new ActionNode((in BTContext ctx) =>
			{
				HumanWorkHandlingResult handling = default;
				ApplyCompletedWork(ctx, actionType, in handling);
				return Success;
			}));
		}

		return node;
	}

	private static void ApplyCompletedWork(
		in BTContext ctx,
		WorkActionType actionType,
		in HumanWorkHandlingResult handling)
	{
		float baseFatigue = WorkPolicyService.GetWorkFatigue(ctx.Worker, actionType);
		if (ctx.Worker is not HumanWorker human)
		{
			ctx.Worker.AddFatigue(baseFatigue);
			return;
		}

		float actualFatigue = HumanIncident.CalculateActionFatigue(human, baseFatigue, in handling);
		human.AddFatigue(actualFatigue);
		HumanIncident.TryCreateIncident(human, actionType, in handling);
	}

	private static NodeState WaitForPendingIncident(in BTContext ctx)
		=> ctx.Worker is HumanWorker human && human.HasPendingIncident
			? Running
			: Success;

	// for incident
	protected static NodeState CheckWorkerIncident(in BTContext ctx)
	{
		if (TryGetPendingIncident(ctx.Worker, out HumanIncidentPayload payload) &&
			payload.ResponseType != HumanIncidentResponseType.None)
		{
			return Success;
		}

		return Failure;
	}

	protected static NodeState IsIncidentWorkMistake(in BTContext ctx)
	{
		if (TryGetPendingIncident(ctx.Worker, out HumanIncidentPayload payload) &&
			payload.ResponseType == HumanIncidentResponseType.WorkMistake)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.HandlingMistake);
			return Success;
		}

		return Failure;
	}

	protected static NodeState IsIncidentMinorInjury(in BTContext ctx)
	{
		return TryGetPendingIncident(ctx.Worker, out HumanIncidentPayload payload) &&
			payload.ResponseType == HumanIncidentResponseType.MinorInjury
			? Success
			: Failure;
	}

	protected static NodeState IsIncidentCollapse(in BTContext ctx)
	{
		if (TryGetPendingIncident(ctx.Worker, out HumanIncidentPayload payload) &&
			payload.ResponseType == HumanIncidentResponseType.AbortTask)
		{
			ctx.Worker.SetWorkerAction(WorkerStatusAction.Collapse);

			return Success;
		}

		return Failure;
	}

	protected static NodeState AbortTask(in BTContext ctx)
	{
		bool entered = ctx.Worker.EnterIncapacitatedState(WorkerOperationalState.Knockout);
		if (ctx.Worker is HumanWorker human)
			human.ClearPendingIncident();
		return entered ? Success : Failure;
	}

	protected static NodeState EndWorkerIncident(in BTContext ctx)
	{
		if (ctx.Worker is HumanWorker human)
		{
			human.ClearPendingIncident();
			if (human.CurrentTask == null && human.IsOperational && GameContext.HasInstance)
				GameContext.Instance.WorkerMgr.AddIdleWorker(human);
		}
		return Success;
	}

	private static bool TryGetPendingIncident(AIWorker worker, out HumanIncidentPayload payload)
	{
		if (worker is HumanWorker human)
			return human.TryGetPendingIncident(out payload);

		payload = null;
		return false;
	}

	protected static SequenceNode BuildHumanIncidentNode()
	{
		SequenceNode root = new SequenceNode();
		
		SequenceNode collapse = new SequenceNode();
		collapse.Add(new ActionNode(IsIncidentCollapse));
		collapse.Add(new ActionNode(AbortTask));

		SequenceNode mistake= new SequenceNode();
		mistake.Add(new ActionNode(IsIncidentWorkMistake));
		mistake.Add(new ActionNode((in BTContext ctx) =>
		{
			ctx.LocalBlackBoard.Set(WorkActionType.HandleMistake.ToString(), HumanIncident.GetMistakeCleanupSeconds());
			return Success;
		}));
		mistake.Add(new DoWorkNode(WorkActionType.HandleMistake));
		mistake.Add(new ActionNode(EndWorkerIncident));

		SequenceNode minorInjury = new();
		minorInjury.Add(new ActionNode(IsIncidentMinorInjury));
		minorInjury.Add(new ActionNode((in BTContext ctx) =>
		{
			ctx.LocalBlackBoard.Set(WorkActionType.HandleMistake.ToString(), HumanIncident.GetMistakeCleanupSeconds() * 1.5f);
			return Success;
		}));
		minorInjury.Add(new DoWorkNode(WorkActionType.HandleMistake));
		minorInjury.Add(new ActionNode(EndWorkerIncident));

		SelectorNode handleIncident = new SelectorNode();
		handleIncident.Add(collapse);
		handleIncident.Add(minorInjury);
		handleIncident.Add(mistake);

		root.Add(new ActionNode(CheckWorkerIncident));
		root.Add(handleIncident);

		return root;
	}

}
