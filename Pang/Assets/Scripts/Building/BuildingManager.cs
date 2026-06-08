using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingManager : MonoBehaviour
{
	[SerializeField] private List<Building> registeredBuildings = new();
	[SerializeField] [HideInInspector] private uint nextRuntimeBuildingId = 1;

	private readonly Dictionary<uint, Building> buildingsById = new();

	public IReadOnlyList<Building> RegisteredBuildings => registeredBuildings;

	private void Awake()
	{
		RebuildLookup();
	}

	public void Register(Building building)
	{
		if (building == null)
			return;

		if (registeredBuildings.Contains(building) == false)
			registeredBuildings.Add(building);

		uint runtimeId = building.RuntimeBuildingId;
		if (runtimeId == 0 || IsRuntimeIdInUse(runtimeId, building))
		{
			runtimeId = AllocateRuntimeBuildingId();
			building.AssignRuntimeBuildingId(runtimeId);
		}

		buildingsById[runtimeId] = building;
		building.SetRegistered(true);
	}

	public void Unregister(Building building)
	{
		if (building == null)
			return;

		registeredBuildings.Remove(building);
		if (building.RuntimeBuildingId != 0)
			buildingsById.Remove(building.RuntimeBuildingId);

		building.SetRegistered(false);
	}

	public bool TryGetBuilding(uint runtimeBuildingId, out Building building)
	{
		if (runtimeBuildingId == 0)
		{
			building = null;
			return false;
		}

		return buildingsById.TryGetValue(runtimeBuildingId, out building) && building != null;
	}

	public void RebuildLookup()
	{
		buildingsById.Clear();
		registeredBuildings.RemoveAll(building => building == null);

		foreach (var building in registeredBuildings)
		{
			if (building == null)
				continue;

			uint runtimeId = building.RuntimeBuildingId;
			if (runtimeId == 0 || buildingsById.ContainsKey(runtimeId))
			{
				runtimeId = AllocateRuntimeBuildingId();
				building.AssignRuntimeBuildingId(runtimeId);
			}

			buildingsById[runtimeId] = building;
			building.SetRegistered(true);
		}
	}

	private bool IsRuntimeIdInUse(uint runtimeId, Building currentBuilding)
	{
		return buildingsById.TryGetValue(runtimeId, out var existing) && existing != null && existing != currentBuilding;
	}

	private uint AllocateRuntimeBuildingId()
	{
		if (nextRuntimeBuildingId == 0)
			nextRuntimeBuildingId = 1;

		while (buildingsById.ContainsKey(nextRuntimeBuildingId))
			nextRuntimeBuildingId += 1;

		uint allocatedId = nextRuntimeBuildingId;
		nextRuntimeBuildingId += 1;
		return allocatedId;
	}
}
