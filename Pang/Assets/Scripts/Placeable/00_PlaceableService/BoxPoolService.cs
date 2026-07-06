using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxPoolService : FacilityService<BoxPool>
{
	public IReadOnlyList<BoxPool> RegisteredBoxPools => CollectRegisteredBoxPools();

	protected override bool IsDestinationCandidate(
		BoxPool facility,
		uint buildingId,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, facilityFilter)
			&& facility.IsInteractionAvailable(interactionKind);
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
