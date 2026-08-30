using System.Collections.Generic;
using Unity.Mathematics;

public abstract class RecoveryFacilityService<TFacility> : FacilityService<TFacility>
	where TFacility : RecoveryFacilityBase
{
	private readonly Dictionary<AIWorker, TFacility> reservations = new();
	private readonly List<AIWorker> workerBuffer = new();
	protected virtual bool AllowUnassignedWorkerGlobalSearch => false;

	protected override void OnEnable()
	{
		base.OnEnable();
	}

	protected override void Start()
	{
		base.Start();
	}

	protected override void OnDisable()
	{
		base.OnDisable();
	}

	public bool HasCompatibleFacility(AIWorker worker)
	{
		return TryFindAvailableFacility(worker, out _);
	}

	public bool TryReserveDestination(
		AIWorker worker,
		out TFacility facility,
		out int3 point)
	{
		facility = null;
		point = default;
		if (worker == null)
			return false;

		if (reservations.TryGetValue(worker, out TFacility reservedFacility))
		{
			if (reservedFacility != null &&
				reservedFacility.IsReservedBy(worker) &&
				reservedFacility.TryGetReservedInteractionPoint(
					worker,
					reservedFacility.RecoveryInteractionKind,
					out point))
			{
				facility = reservedFacility;
				return true;
			}

			reservations.Remove(worker);
		}

		if (TryFindAvailableFacility(worker, out TFacility bestFacility) == false ||
			bestFacility.TryReserveSlot(worker, worker.GridPosition, out point) == false)
		{
			return false;
		}

		reservations[worker] = bestFacility;
		facility = bestFacility;
		return true;
	}

	private bool TryFindAvailableFacility(AIWorker worker, out TFacility facility)
	{
		facility = null;
		if (worker == null || FacilityManager == null)
			return false;

		FacilityFilter facilityFilter = FacilityFilter.ForWorker(worker);

		bool TryScore(TFacility candidate, in int3 origin, out int score)
		{
			score = int.MaxValue;
			return candidate != null &&
				FacilityManager.IsInvalidating(candidate) == false &&
				facilityFilter.MatchesCurrentRules(candidate) &&
				candidate.TryGetAvailableSlot(worker, origin, out _, out score);
		}

		if (worker.PrimaryBuildingId != 0)
		{
			return TryFindClosestFacility(
				worker.PrimaryBuildingId,
				worker.GridPosition,
				TryScore,
				out facility);
		}

		if (AllowUnassignedWorkerGlobalSearch == false)
			return false;

		return TryFindClosestFacility(
			worker.GridPosition,
			TryScore,
			out facility);
	}

	public void ReleaseWorker(AIWorker worker)
	{
		if (worker == null)
			return;

		if (reservations.TryGetValue(worker, out TFacility facility))
		{
			facility?.ReleaseWorker(worker);
			reservations.Remove(worker);
		}
	}

	public void ResetRuntimeState()
	{
		workerBuffer.Clear();
		workerBuffer.AddRange(reservations.Keys);
		for (int i = 0; i < workerBuffer.Count; ++i)
		{
			AIWorker worker = workerBuffer[i];
			if (worker != null)
				worker.CancelRecovery(false);
			else
				reservations.Remove(worker);
		}

		workerBuffer.Clear();
		reservations.Clear();
		registeredFacilities.Clear();
	}

	protected override void OnUnregisterFacility(uint buildingId, TFacility facility)
	{
		base.OnUnregisterFacility(buildingId, facility);
		if (facility == null)
			return;

		workerBuffer.Clear();
		foreach (var pair in reservations)
		{
			if (pair.Value == facility)
				workerBuffer.Add(pair.Key);
		}

		for (int i = 0; i < workerBuffer.Count; ++i)
		{
			AIWorker worker = workerBuffer[i];
			if (worker != null)
				worker.CancelRecovery(true);
			else
				reservations.Remove(worker);
		}

		workerBuffer.Clear();
	}
}
