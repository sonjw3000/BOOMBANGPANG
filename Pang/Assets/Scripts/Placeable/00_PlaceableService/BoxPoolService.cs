using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxPoolService : FacilityService<BoxPool>
{
	public IReadOnlyList<BoxPool> RegisteredBoxPools => CollectRegisteredBoxPools();


	public BoxPool GetClosestAvailableTarget(in int3 pos, InteractionKind interactionKind)
	{
		FacilityDistanceResolver distanceResolver = (BoxPool candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GridService,
				out _,
				out score);

		Predicate<BoxPool> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);
		return TryFindClosestFacility(pos, distanceResolver, out BoxPool target, predicate)
			? target
			: null;
	}

	public BoxPool GetClosestAvailableTarget(uint buildingId, in int3 pos, InteractionKind interactionKind)
	{
		if (buildingId == 0)
			return GetClosestAvailableTarget(pos, interactionKind);

		FacilityDistanceResolver distanceResolver = (BoxPool candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GridService,
				out _,
				out score);

		Predicate<BoxPool> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);
		return TryFindClosestFacility(buildingId, pos, distanceResolver, out BoxPool target, predicate)
			? target
			: null;
	}

	private IReadOnlyList<BoxPool> CollectRegisteredBoxPools()
	{
		List<BoxPool> result = new();
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out IReadOnlyList<BoxPool> facilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
			{
				BoxPool poolFacility = facilities[facilityIndex];
				if (poolFacility != null)
					result.Add(poolFacility);
			}
		}

		return result;
	}
}
