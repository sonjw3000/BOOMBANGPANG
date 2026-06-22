using Unity.Mathematics;

public sealed partial class AirlockService : FacilityService<Airlock>
{
	public bool TryFindClosestAvailable(
		AIWorker worker,
		uint buildingId,
		out Airlock airlock)
	{
		airlock = null;
		if (worker == null || buildingId == 0)
			return false;

		return TryFindClosestFacility(
			buildingId,
			worker.GridPosition,
			ResolveEnterDistance,
			out airlock,
			facility => facility != null && facility.IsAvailable);
	}

	public bool TryReserveClosest(
		AIWorker worker,
		uint buildingId,
		AirlockDirection direction,
		out Airlock airlock)
	{
		airlock = null;
		if (worker == null || buildingId == 0)
			return false;

		if (TryFindClosestFacility(
			buildingId,
			worker.GridPosition,
			ResolveEnterDistance,
			out Airlock candidate,
			facility => facility != null && facility.IsAvailable) == false)
		{
			return false;
		}

		if (candidate.TryReserve(worker, direction) == false)
			return false;

		airlock = candidate;
		return true;
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

	private static bool ResolveEnterDistance(Airlock airlock, in int3 from, out int score)
	{
		score = 0;
		if (airlock == null || airlock.IsInteractionAvailable(InteractionKind.Enter) == false)
			return false;

		int3 point = airlock.GetClosestInteractionPoint(InteractionKind.Enter, from);
		score = math.abs(point.x - from.x) + math.abs(point.z - from.z);
		return true;
	}
}
