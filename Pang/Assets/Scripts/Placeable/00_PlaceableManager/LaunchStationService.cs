using System;
using Unity.Mathematics;
using UnityEngine;

public class LaunchStationService : FacilityService<LaunchStation>
{
	public LaunchStation GetClosestAvailableTarget(in int3 pos, InteractionKind interactionKind)
	{
		FacilityDistanceResolver distanceResolver = (LaunchStation candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<LaunchStation> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);

		if (TryGetBuildingId(pos, out uint buildingId) &&
			TryFindClosestFacility(buildingId, pos, distanceResolver, out LaunchStation target, predicate))
		{
			return target;
		}

		if (TryFindClosestFacility(pos, distanceResolver, out LaunchStation globalTarget, predicate))
			return globalTarget;

		return null;
	}

	public LaunchStation GetClosestAvailableTarget(uint buildingId, in int3 pos, InteractionKind interactionKind)
	{
		FacilityDistanceResolver distanceResolver = (LaunchStation candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<LaunchStation> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);
		return TryFindClosestFacility(buildingId, pos, distanceResolver, out LaunchStation target, predicate)
			? target
			: null;
	}
}
