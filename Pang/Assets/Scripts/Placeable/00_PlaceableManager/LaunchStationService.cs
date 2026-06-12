using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceClassName: "LaunchStationManager")]
public class LaunchStationService : FacilityService<LaunchStation>
{
	public LaunchStation GetClosestAvailableTarget(in int3 pos, InteractionKind interactionKind)
	{
		if (TryGetBuildingId(pos, out uint buildingId) &&
			TryGetBuildingFacilities(buildingId, out var facilities) &&
			facilities.Count > 0)
		{
			LaunchStation target = GetClosestAvailableTarget(facilities, pos, interactionKind);
			if (target != null)
				return target;
		}

		return GetClosestAvailableTarget(GetAllFacilities(), pos, interactionKind);
	}

	public LaunchStation GetClosestAvailableTarget(uint buildingId, in int3 pos, InteractionKind interactionKind)
	{
		if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
			return null;

		return GetClosestAvailableTarget(facilities, pos, interactionKind);
	}

	private IReadOnlyList<LaunchStation> GetAllFacilities()
	{
		List<LaunchStation> facilities = new();
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out var buildingFacilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < buildingFacilities.Count; ++facilityIndex)
				facilities.Add(buildingFacilities[facilityIndex]);
		}

		return facilities;
	}

	private static LaunchStation GetClosestAvailableTarget(
		IReadOnlyList<LaunchStation> facilities,
		in int3 pos,
		InteractionKind interactionKind)
	{
		LaunchStation target = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < facilities.Count; ++i)
		{
			LaunchStation candidate = facilities[i];
			if (candidate == null || candidate.IsInteractionAvailable(interactionKind) == false)
				continue;

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				pos,
				GameContext.Instance.GridService,
				out _,
				out int sum) == false)
			{
				continue;
			}

			if (posPowMin > sum)
			{
				posPowMin = sum;
				target = candidate;
			}
		}

		return target;
	}
}
