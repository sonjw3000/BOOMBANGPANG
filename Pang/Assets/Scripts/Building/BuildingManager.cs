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

	public Building CreateBuilding(List<GridCell> ownedCells, BuildingType buildingType = BuildingType.Generic, string displayName = null)
	{
		if (ownedCells == null || ownedCells.Count <= 0)
			return null;

		for (int i = 0; i < ownedCells.Count; ++i)
		{
			GridCell cell = ownedCells[i];
			if (cell == null || cell.BuildingId != 0)
				return null;
		}

		string resolvedName = string.IsNullOrWhiteSpace(displayName)
			? BuildDefaultBuildingName(buildingType)
			: displayName;

		Building building = new(resolvedName, ownedCells, buildingType);
		Register(building);

		for (int i = 0; i < ownedCells.Count; ++i)
			ownedCells[i].SetBuildingId(building.RuntimeBuildingId);

		return building;
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

	public bool TryGetBuilding(GridCell cell, out Building building)
	{
		if (cell == null)
		{
			building = null;
			return false;
		}

		return TryGetBuilding(cell.BuildingId, out building);
	}

	public bool TryRegisterFacility(uint runtimeBuildingId, IFacility facility)
	{
		if (facility == null || TryGetBuilding(runtimeBuildingId, out var building) == false)
			return false;

		return building.RegisterFacility(facility);
	}

	public bool TryUnregisterFacility(uint runtimeBuildingId, IFacility facility)
	{
		if (facility == null || TryGetBuilding(runtimeBuildingId, out var building) == false)
			return false;

		return building.UnregisterFacility(facility);
	}

	public bool SetBuildingState(Building building, BuildingState newState)
	{
		if (building == null || registeredBuildings.Contains(building) == false)
			return false;

		building.SetState(newState);
		return true;
	}

	public bool SetBuildingWorkScope(Building building, BuildingWorkScope newWorkScope)
	{
		if (building == null || registeredBuildings.Contains(building) == false)
			return false;

		building.SetWorkScope(newWorkScope);
		return true;
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

	public void ResetRuntimeState()
	{
		registeredBuildings.Clear();
		buildingsById.Clear();
		nextRuntimeBuildingId = 1;
	}

	public BuildingManagerSaveData CaptureState()
	{
		BuildingManagerSaveData data = new();
		foreach (Building building in registeredBuildings)
		{
			if (building == null)
				continue;

			data.Buildings.Add(new BuildingSaveData
			{
				RuntimeBuildingId = building.RuntimeBuildingId,
				Name = building.DisplayName,
				Type = building.Type,
				State = building.State,
				WorkScope = building.WorkScope,
			});
		}

		return data;
	}

	public Building RestoreBuilding(
		List<GridCell> ownedCells,
		uint runtimeBuildingId,
		BuildingType buildingType,
		string displayName,
		BuildingState state,
		BuildingWorkScope workScope)
	{
		if (ownedCells == null || ownedCells.Count <= 0)
			return null;

		Building building = new(displayName, ownedCells, buildingType);
		building.AssignRuntimeBuildingId(runtimeBuildingId);
		building.SetState(state);
		building.SetWorkScope(workScope);
		Register(building);

		for (int i = 0; i < ownedCells.Count; ++i)
			ownedCells[i]?.SetBuildingId(building.RuntimeBuildingId);

		return building;
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

	private string BuildDefaultBuildingName(BuildingType buildingType)
	{
		string baseName = buildingType == BuildingType.Generic ? "Building" : $"{buildingType} Building";
		int suffix = 1;
		string candidate = baseName;

		while (registeredBuildings.Exists(building => building != null && building.DisplayName == candidate))
		{
			suffix += 1;
			candidate = $"{baseName} {suffix}";
		}

		return candidate;
	}
}
