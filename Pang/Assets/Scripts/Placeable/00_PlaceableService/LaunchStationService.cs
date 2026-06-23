using System;
using Unity.Mathematics;
using UnityEngine;

public class LaunchStationService : FacilityService<LaunchStation>
{
	protected override bool IsDestinationCandidate(
		LaunchStation facility,
		uint buildingId,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, zoneFilter)
			&& facility.IsInteractionAvailable(interactionKind);
	}
}
