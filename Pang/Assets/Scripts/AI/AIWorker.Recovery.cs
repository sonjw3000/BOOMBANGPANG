using Unity.Mathematics;
using UnityEngine;

public abstract partial class AIWorker
{
	private IRecoveryFacility recoveryFacility;
	private int3 reservedRecoveryPoint;
	private bool isUsingRecoveryFacility;
	private bool recoveryRequestedBeforeNextTask;

	public bool IsRecovering => IsRecoveryReservationValid();

	internal bool TryCanBeginRecovery()
	{
		if (CurrentTask != null ||
			IsOperational == false ||
			HasRecoveryNeed() == false ||
			CanLeaveAssignedStationForRecovery() == false ||
			GameContext.HasInstance == false)
		{
			return false;
		}

		if (IsRecoveryReservationValid())
			return true;

		CancelRecovery(false);

		IRecoveryFacility facility;
		int3 point;
		if (this is RobotWorker)
		{
			if (GameContext.Instance.ChargingFacilitySvc.TryReserveDestination(
				this,
				out ChargingFacility chargingFacility,
				out point) == false)
			{
				return false;
			}

			facility = chargingFacility;
		}
		else if (this is HumanWorker)
		{
			if (GameContext.Instance.RestFacilitySvc.TryReserveDestination(
				this,
				out RestFacility restFacility,
				out point) == false)
			{
				return false;
			}

			facility = restFacility;
		}
		else
		{
			return false;
		}

		recoveryFacility = facility;
		reservedRecoveryPoint = point;
		isUsingRecoveryFacility = false;
		localBlackBoard.SetTargetBuilding(facility);
		WorkerMgr.RemoveIdleWorker(this);

		if (CurrentWorkingBuilding is PackingStation station)
		{
			station.CurrentPackingWorker = null;
			station.RefreshWaitingState();
			OnWorkingPointSet(null);
		}

		return true;
	}

	internal bool TryBeginRecoveryUse()
	{
		if (IsRecoveryReservationValid() == false ||
			recoveryFacility.TryGetReservedInteractionPoint(
				this,
				recoveryFacility.RecoveryInteractionKind,
				out int3 point) == false ||
			point.Equals(GridPosition) == false ||
			recoveryFacility.TryBeginUse(this) == false)
		{
			CancelRecovery(true);
			return false;
		}

		reservedRecoveryPoint = point;
		isUsingRecoveryFacility = true;
		OnWorkingPointSet(recoveryFacility);
		return true;
	}

	internal float GetEffectiveRecoveryPerSecond()
	{
		if (isUsingRecoveryFacility == false || IsRecoveryReservationValid() == false)
			return 0.0f;

		return recoveryFacility.GetEffectiveRecoveryPerSecond(this) *
			Mathf.Max(0.0f, GetRecoveryEfficiencyMultiplier());
	}

	internal bool IsRecoveryReservationValid()
	{
		return recoveryFacility is Component component &&
			component != null &&
			recoveryFacility.IsReservedBy(this);
	}

	internal void CompleteRecovery()
	{
		recoveryRequestedBeforeNextTask = false;
		CancelRecovery(true);
	}

	internal void RequestRecoveryBeforeNextTask()
	{
		recoveryRequestedBeforeNextTask = true;
		enabled = true;
	}

	internal void CancelRecovery(bool becomeIdle)
	{
		IRecoveryFacility facility = recoveryFacility;
		if (GameContext.HasInstance)
		{
			if (this is RobotWorker)
				GameContext.Instance.ChargingFacilitySvc?.ReleaseWorker(this);
			else if (this is HumanWorker)
				GameContext.Instance.RestFacilitySvc?.ReleaseWorker(this);
		}

		facility?.ReleaseWorker(this);
		recoveryFacility = null;
		reservedRecoveryPoint = default;
		isUsingRecoveryFacility = false;
		localBlackBoard.RemoveTargetBuilding();
		if (localBlackBoard.TryGet(TransitAirlockKey, out Airlock transitAirlock) &&
			transitAirlock != null &&
			GameContext.HasInstance)
		{
			AirlockService?.Release(transitAirlock, this);
		}
		ClearTransitState(localBlackBoard);

		if (routeFinder != null &&
			(routeFinder.HasActiveGoal || routeFinder.CurrentMovementState == FindRoute.MovementState.Failed))
		{
			routeFinder.CancelCurrentRoute();
		}

		if (ReferenceEquals(CurrentWorkingBuilding, facility))
			OnWorkingPointSet(null);

		SetWorkerTarget(WorkerStatusTarget.None);
		if (workerState.Action == WorkerStatusAction.Resting ||
			workerState.Action == WorkerStatusAction.Charging ||
			workerState.Action == WorkerStatusAction.WaitingForTargetBuilding)
		{
			SetWorkerAction(WorkerStatusAction.Idle);
		}

		if (becomeIdle &&
			GameContext.HasInstance &&
			IsOperational &&
			CurrentTask == null)
		{
			enabled = true;
			WorkerMgr.AddIdleWorker(this);
		}
	}

	internal bool ShouldPrioritizeRecovery()
	{
		if (IsRecoveryReservationValid())
			return true;

		if (CurrentTask != null ||
			IsOperational == false ||
			HasRecoveryNeed() == false ||
			CanLeaveAssignedStationForRecovery() == false ||
			GameContext.HasInstance == false)
		{
			return false;
		}

		if (this is RobotWorker)
			return GameContext.Instance.ChargingFacilitySvc.HasCompatibleFacility(this);

		if (this is HumanWorker)
			return GameContext.Instance.RestFacilitySvc.HasCompatibleFacility(this);

		return false;
	}

	private bool HasRecoveryNeed()
		=> recoveryRequestedBeforeNextTask || NeedsRecovery();
}
