using System.Collections.Generic;
using Unity.Mathematics;

public abstract class RecoveryFacilityService<TFacility> : FacilityService<TFacility>
	where TFacility : RecoveryFacilityBase
{
	private readonly Dictionary<AIWorker, TFacility> reservations = new();
	private readonly List<AIWorker> workerBuffer = new();

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
		if (worker == null)
			return false;

		foreach (var buildingEntry in registeredFacilities)
		{
			List<TFacility> facilities = buildingEntry.Value;
			for (int i = 0; i < facilities.Count; ++i)
			{
				TFacility facility = facilities[i];
				if (facility != null &&
					FacilityManager.IsInvalidating(facility) == false &&
					facility.CanServe(worker))
				{
					return true;
				}
			}
		}

		return false;
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

		TFacility bestFacility = null;
		int bestScore = int.MaxValue;
		foreach (var buildingEntry in registeredFacilities)
		{
			List<TFacility> facilities = buildingEntry.Value;
			for (int i = 0; i < facilities.Count; ++i)
			{
				TFacility candidate = facilities[i];
				if (candidate == null ||
					FacilityManager.IsInvalidating(candidate) ||
					candidate.TryGetAvailableSlot(worker, worker.GridPosition, out _, out int score) == false ||
					score >= bestScore)
				{
					continue;
				}

				bestFacility = candidate;
				bestScore = score;
			}
		}

		if (bestFacility == null ||
			bestFacility.TryReserveSlot(worker, worker.GridPosition, out point) == false)
		{
			return false;
		}

		reservations[worker] = bestFacility;
		facility = bestFacility;
		return true;
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
