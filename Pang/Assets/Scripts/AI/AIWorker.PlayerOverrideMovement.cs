using Assets.Scripts.AI.BT;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using OverridePhase = PlayerOverridePhase;

public abstract partial class AIWorker
{
	private bool hasPlayerOverrideMoveDestination;
	private int3 playerOverrideMoveDestination;

	public bool TryGetPlayerOverrideDestination(out int3 destination)
	{
		destination = playerOverrideMoveDestination;
		return IsPlayerOverride && hasPlayerOverrideMoveDestination;
	}

	internal bool TryRequestPlayerOverrideMove(in int3 destination, out string message)
	{
		message = string.Empty;
		if (IsPlayerOverride == false)
		{
			message = "The worker is not under player control.";
			return false;
		}

		if (IsOperational == false)
		{
			message = "The worker is not operational.";
			return false;
		}

		if (playerOverridePhase != OverridePhase.AwaitingCommand)
		{
			message = "Wait until the worker is ready for another command.";
			return false;
		}

		if (routeFinder == null)
		{
			message = "The worker has no route finder.";
			return false;
		}

		CancelPendingPlayerOverrideMove();
		playerOverrideMoveDestination = destination;
		hasPlayerOverrideMoveDestination = true;
		if (GridPosition.Equals(destination))
		{
			CompletePlayerOverrideMove();
			return true;
		}

		SetPlayerOverridePhase(OverridePhase.Moving);
		SetWorkerTarget(WorkerStatusTarget.None);
		SetWorkerAction(WorkerStatusAction.MovingTo);
		enabled = true;
		NotifyPlayerOverrideStateChanged();
		return true;
	}

	internal void PrepareForPlayerControlPreemption()
	{
		ReleaseActiveAirlockTransit(localBlackBoard);
		routeFinder?.CancelCurrentRoute();
		ClearTransitState(localBlackBoard);
	}

	private static NodeState RunPlayerOverrideMove(in BTContext ctx, AIWorker worker)
	{
		if (worker.hasPlayerOverrideMoveDestination == false)
		{
			worker.CompletePlayerOverrideMove();
			return NodeState.Running;
		}

		FindRoute route = worker.routeFinder;
		if (route == null)
		{
			worker.FailPlayerOverrideMove("The worker has no route finder.");
			return NodeState.Running;
		}

		bool hasTransit = ctx.LocalBlackBoard.TryGet(TransitAirlockKey, out Airlock airlock) && airlock != null;
		if (route.HasActiveGoal)
		{
			if (route.IsGoal)
			{
				route.ConsumeArrivedGoal();
				worker.ApplyCarriedMovementFatigue(route.ConsumeTravelledCells());
				if (hasTransit)
				{
					NodeState transitResult = RunPlayerOverrideAirlockTransit(ctx, worker, airlock);
					if (transitResult != NodeState.Success)
						return NodeState.Running;
				}
				else if (worker.GridPosition.Equals(worker.playerOverrideMoveDestination))
				{
					worker.CompletePlayerOverrideMove();
					return NodeState.Running;
				}
			}
			else
			{
				worker.SetWorkerAction(WorkerStatusAction.MovingTo);
				return NodeState.Running;
			}
		}

		if (route.CurrentMovementState == FindRoute.MovementState.Failed)
		{
			route.CancelCurrentRoute();
			ClearTransitState(ctx.LocalBlackBoard);
			worker.FailPlayerOverrideMove("No route is available for the player move command.");
			return NodeState.Running;
		}

		if (ctx.LocalBlackBoard.TryGet(TransitAirlockKey, out airlock) && airlock != null)
		{
			NodeState transitResult = RunPlayerOverrideAirlockTransit(ctx, worker, airlock);
			if (transitResult != NodeState.Success)
				return NodeState.Running;
		}

		if (worker.GridPosition.Equals(worker.playerOverrideMoveDestination))
		{
			worker.CompletePlayerOverrideMove();
			return NodeState.Running;
		}

		if (TryPlanPlayerOverrideMoveLeg(ctx, worker) == false)
			worker.FailPlayerOverrideMove("No airlock or route can reach the requested destination.");

		return NodeState.Running;
	}

	private static NodeState RunPlayerOverrideAirlockTransit(
		in BTContext ctx,
		AIWorker worker,
		Airlock airlock)
	{
		NodeState result = TryUseTransitAirlockIfNeeded(ctx);
		if (result == NodeState.Success)
		{
			worker.SetPlayerOverrideMovePhase(OverridePhase.Moving);
			return result;
		}

		bool started = ctx.LocalBlackBoard.TryGet(TransitStartedKey, out bool transitStarted) && transitStarted;
		worker.SetPlayerOverrideMovePhase(
			started || airlock.ReservedWorker == worker
				? OverridePhase.UsingAirlock
				: OverridePhase.WaitingForAirlock);
		return result;
	}

	private static bool TryPlanPlayerOverrideMoveLeg(in BTContext ctx, AIWorker worker)
	{
		int3 destination = worker.playerOverrideMoveDestination;
		bool sameRegion = GridService.IsSameRegion(worker.GridPosition, destination);
		TryGetBuildingId(worker.GridPosition, out uint currentBuildingId);
		TryGetBuildingId(destination, out uint targetBuildingId);

		if (sameRegion)
			return BeginPlayerOverrideRoute(worker, destination, WorkerStatusTarget.None);

		if (currentBuildingId != 0)
		{
			return TryRouteToAirlock(
				ctx,
				currentBuildingId,
				AirlockDirection.InsideToOutside,
				includeBusy: true);
		}

		if (targetBuildingId != 0)
		{
			return TryRouteToAirlock(
				ctx,
				targetBuildingId,
				AirlockDirection.OutsideToInside,
				includeBusy: true);
		}

		return false;
	}

	private static bool BeginPlayerOverrideRoute(
		AIWorker worker,
		in int3 destination,
		WorkerStatusTarget target)
	{
		worker.SetPlayerOverrideMovePhase(OverridePhase.Moving);
		worker.SetWorkerTarget(target);
		worker.SetWorkerAction(WorkerStatusAction.MovingTo);
		worker.routeFinder.enabled = true;
		return worker.routeFinder.SetGoalPosition(destination);
	}

	private void SetPlayerOverrideMovePhase(PlayerOverridePhase phase)
	{
		if (playerOverridePhase == phase)
			return;

		SetPlayerOverridePhase(phase);
		NotifyPlayerOverrideStateChanged();
	}

	private void CompletePlayerOverrideMove()
	{
		hasPlayerOverrideMoveDestination = false;
		ClearTransitState(playerOverrideBlackBoard);
		SetPlayerOverridePhase(OverridePhase.AwaitingCommand);
		SetWorkerTarget(WorkerStatusTarget.None);
		SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
		enabled = true;
		NotifyPlayerOverrideStateChanged();
	}

	private void FailPlayerOverrideMove(string message)
	{
		if (string.IsNullOrWhiteSpace(message) == false)
		{
			Debug.LogWarning($"[PlayerOverride] {message}", this);
			if (GameContext.HasInstance)
				GameContext.Instance.HudEventManager?.Publish(HudEventType.Warning, message, this);
		}

		CompletePlayerOverrideMove();
	}

	private void CancelPendingPlayerOverrideMove()
	{
		ReleaseActiveAirlockTransit(playerOverrideBlackBoard);
		routeFinder?.CancelCurrentRoute();
		ClearTransitState(playerOverrideBlackBoard);
		hasPlayerOverrideMoveDestination = false;
	}

	private void ReleaseActiveAirlockTransit(BlackBoard blackBoard)
	{
		if (blackBoard == null || blackBoard.TryGet(TransitAirlockKey, out Airlock airlock) == false || airlock == null)
			return;

		if (GameContext.HasInstance && AirlockService != null)
			AirlockService.Release(airlock, this);
		else
			airlock.Release(this);
	}
}
