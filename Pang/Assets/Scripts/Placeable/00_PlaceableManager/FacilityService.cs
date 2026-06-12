using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

public class FacilityService<T> : MonoBehaviour where T : class, IFacility
{
	protected FacilityManager FacilityManager => GameContext.Instance.FacilityMgr;
	protected GridService GridService => GameContext.Instance.GridService;

	protected IReadOnlyList<T> BuildingFacilities(uint buildingId) => FacilityManager.GetFacilities<T>(buildingId);
	protected bool TryGetBuildingFacilities(uint buildingId, out IReadOnlyList<T> facilities) => FacilityManager.TryGetFacilities(buildingId, out facilities);

	private void Start()
	{
		FacilityManager.SubscribeFacilityRegister<T>(HandleFacilityRegistered, HandleFacilityUnregistered);
		RebuildRegisteredFacilities();
	}

	private void OnDestroy()
	{
		FacilityManager.UnsubscribeFacilityRegister<T>(HandleFacilityRegistered, HandleFacilityUnregistered);
	}

	protected bool TryGetBuildingId(IFacility facility, out uint buildingId)
	{
		if (facility == null)
		{
			buildingId = 0;
			return false;
		}

		return TryGetBuildingId(facility.GridPosition, out buildingId);
	}

	protected bool TryGetBuildingId(in int3 position, out uint buildingId)
	{
		GridCell cell = GridService?.GetCell(position);
		buildingId = cell != null ? cell.BuildingId : 0;
		return buildingId != 0;
	}

	private void RebuildRegisteredFacilities()
	{
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			uint buildingId = buildingIds[i];
			if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
				OnRegisterFacility(buildingId, facilities[facilityIndex]);
		}
	}

	private void HandleFacilityRegistered(uint buildingId, IFacility facility)
	{
		if (facility is T typedFacility)
			OnRegisterFacility(buildingId, typedFacility);
	}

	private void HandleFacilityUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is T typedFacility)
			OnUnregisterFacility(buildingId, typedFacility);
	}

	protected virtual void OnRegisterFacility(uint buildingId, T facility) { }
	protected virtual void OnUnregisterFacility(uint buildingId, T facility) { }
}
