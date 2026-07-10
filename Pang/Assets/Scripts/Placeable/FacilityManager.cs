using UnityEngine;
using System.Collections.Generic;
using System;

public class BuildingFacilityIndex
{
	private readonly List<IFacility> facilities = new();
	private readonly Dictionary<Type, List<IFacility>> buildingFacilityMap = new();

	public IReadOnlyList<IFacility> Facilities => facilities;
	public IReadOnlyDictionary<Type, List<IFacility>> BuildingFacilityMap => buildingFacilityMap;

	public bool RegisterFacility(IFacility facility)
	{
		if (facility == null || facilities.Contains(facility))
			return false;

		facilities.Add(facility);

		Type facilityType = facility.GetType();
		if (!buildingFacilityMap.TryGetValue(facilityType, out List<IFacility> facilityList))
		{
			facilityList = new List<IFacility>();
			buildingFacilityMap[facilityType] = facilityList;
		}
		facilityList.Add(facility);
		return true;
	}

	public bool UnregisterFacility(IFacility facility)
	{
		if (facility == null)
			return false;

		if (facilities.Remove(facility) == false)
			return false;

		Type facilityType = facility.GetType();
		if (buildingFacilityMap.TryGetValue(facilityType, out List<IFacility> facilityList) == false)
			return false;

		facilityList.Remove(facility);
		if (facilityList.Count == 0)
			buildingFacilityMap.Remove(facilityType);					

		return true;
	}
}

// facility를 보유
public partial class FacilityManager : MonoBehaviour
{
	// if uid == 0, outside of buildingß
	private readonly Dictionary<uint, BuildingFacilityIndex> buildingFacilities = new();
	private readonly Dictionary<IFacility, uint> facilityBuildingIds = new();

	
	public delegate void OnRegisterFacilityCallback(uint buildingId, IFacility facility);
	public delegate void OnUnregisterFacilityCallback(uint buildingId, IFacility facility);

	private readonly Dictionary<Type, OnRegisterFacilityCallback> registerCallbacks = new();
	private readonly Dictionary<Type, OnUnregisterFacilityCallback> unregisterCallbacks = new();

	public void SubscribeFacilityRegister<T>(OnRegisterFacilityCallback registerCallback, OnUnregisterFacilityCallback unregisterCallback) where T : IFacility
	{
		if (registerCallback != null)
		{
			if (registerCallbacks.ContainsKey(typeof(T)) == false)
				registerCallbacks[typeof(T)] = null;
			registerCallbacks[typeof(T)] += registerCallback;
		}

		if (unregisterCallback != null)
		{
			if (unregisterCallbacks.ContainsKey(typeof(T)) == false)
				unregisterCallbacks[typeof(T)] = null;
			unregisterCallbacks[typeof(T)] += unregisterCallback;
		}
	}

	public void UnsubscribeFacilityRegister<T>(OnRegisterFacilityCallback registerCallback, OnUnregisterFacilityCallback unregisterCallback) where T : IFacility
	{
		if (registerCallback != null)
		{
			if (registerCallbacks.ContainsKey(typeof(T)) == false)
				registerCallbacks[typeof(T)] = null;
			registerCallbacks[typeof(T)] -= registerCallback;
		}

		if (unregisterCallback != null)
		{
			if (unregisterCallbacks.ContainsKey(typeof(T)) == false)
				unregisterCallbacks[typeof(T)] = null;
			unregisterCallbacks[typeof(T)] -= unregisterCallback;
		}
	}

	public void RegisterFacility(uint buildingId, IFacility facility)
	{
		if (facility == null)
			return;

		if (buildingFacilities.TryGetValue(buildingId, out BuildingFacilityIndex facilityIndex) == false)
		{
			facilityIndex = new BuildingFacilityIndex();
			buildingFacilities[buildingId] = facilityIndex;
		}

		if (facilityIndex.RegisterFacility(facility) == false)
			return;

		facilityBuildingIds[facility] = buildingId;

		Type runtimeType = facility.GetType();

		foreach (var (subscribedType, callback) in registerCallbacks)
			if (subscribedType.IsAssignableFrom(runtimeType))
				callback?.Invoke(buildingId, facility);
	}

	public void UnregisterFacility(uint buildingId, IFacility facility)
	{
		if (facility == null)
			return;

		if (buildingFacilities.TryGetValue(buildingId, out BuildingFacilityIndex facilityIndex) == false)
			return;

		if (facilityIndex.UnregisterFacility(facility) == false)
			return;

		facilityBuildingIds.Remove(facility);

		Type runtimeType = facility.GetType();

		foreach (var (subscribedType, callback) in unregisterCallbacks)
			if (subscribedType.IsAssignableFrom(runtimeType))
				callback?.Invoke(buildingId, facility);

		if (facilityIndex.Facilities.Count == 0)
			buildingFacilities.Remove(buildingId);
	}

	public IReadOnlyList<uint> GetBuildingIds()
	{
		if (buildingFacilities.Count <= 0)
			return Array.Empty<uint>();

		List<uint> buildingIds = new(buildingFacilities.Count);
		foreach (var buildingId in buildingFacilities.Keys)
			buildingIds.Add(buildingId);

		return buildingIds;
	}

	public bool TryGetBuildingId(IFacility facility, out uint buildingId)
	{
		if (facility == null)
		{
			buildingId = 0;
			return false;
		}

		return facilityBuildingIds.TryGetValue(facility, out buildingId);
	}

	public IReadOnlyList<T> GetFacilities<T>(uint buildingId) where T : class, IFacility
	{
		TryGetFacilities(buildingId, out IReadOnlyList<T> facilities);
		return facilities;
	}

	public bool TryGetFacilities<T>(uint buildingId, out IReadOnlyList<T> facilities) where T : class, IFacility
	{
		List<T> typedFacilities = new();
		if (buildingFacilities.TryGetValue(buildingId, out BuildingFacilityIndex facilityIndex) == false)
		{
			facilities = typedFacilities;
			return false;
		}

		Type targetType = typeof(T);
		foreach (var kvp in facilityIndex.BuildingFacilityMap)
		{
			if (targetType.IsAssignableFrom(kvp.Key))
			{
				for (int i = 0; i < kvp.Value.Count; ++i)
				{
					if (kvp.Value[i] is T facility)
						typedFacilities.Add(facility);
				}
			}
		}

		facilities = typedFacilities;
		return typedFacilities.Count > 0;
	}

}
