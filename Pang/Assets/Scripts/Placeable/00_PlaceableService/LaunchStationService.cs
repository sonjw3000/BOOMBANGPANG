using System;
using Unity.Mathematics;
using UnityEngine;

public class LaunchStationService : FacilityService<LaunchStation>
{
	protected override bool IsDestinationCandidate(
		LaunchStation facility,
		uint buildingId,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, facilityFilter)
			&& facility.IsInteractionAvailable(interactionKind);
	}
}
