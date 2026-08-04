using Unity.Mathematics;

public sealed partial class AirlockService : FacilityService<Airlock>
{
	public bool TryFindTransitDestination(
		uint buildingId,
		in int3 from,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter,
		bool includeBusy,
		out Airlock airlock)
	{
		if (includeBusy == false)
			return TryFindDestination(buildingId, from, interactionKind, facilityFilter, out airlock);

		bool TryScore(Airlock candidate, in int3 origin, out int score)
		{
			score = int.MaxValue;
			return candidate != null &&
				candidate.IsInteractionAvailable(interactionKind) &&
				facilityFilter.MatchesCurrentRules(candidate) &&
				InteractionPointSelector.TryGetInteractionPoint(
					candidate,
					interactionKind,
					origin,
					out _,
					out score);
		}

		return TryFindClosestFacility(buildingId, from, TryScore, out airlock);
	}

	protected override bool IsDestinationCandidate(
		Airlock facility,
		uint buildingId,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, facilityFilter)
			&& facility.IsAvailable
			&& facility.IsInteractionAvailable(interactionKind);
	}

	public bool TryReserve(Airlock airlock, AIWorker worker, AirlockDirection direction)
	{
		return airlock != null && airlock.TryReserve(worker, direction);
	}

	public bool TryBeginEntry(Airlock airlock, AIWorker worker)
	{
		return airlock != null && airlock.TryBeginEntry(worker);
	}

	public void Release(Airlock airlock, AIWorker worker)
	{
		airlock?.Release(worker);
	}
}
