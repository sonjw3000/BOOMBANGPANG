using Unity.Mathematics;

public sealed partial class AirlockService : FacilityService<Airlock>
{
	protected override bool IsDestinationCandidate(
		Airlock facility,
		uint buildingId,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, zoneFilter)
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
