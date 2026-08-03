using System.Collections.Generic;
using UnityEngine;

public sealed partial class BuildingManager : MonoBehaviour
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

	public Building CreateBuilding(
		List<GridCell> ownedCells,
		BuildingType buildingType = BuildingType.Generic,
		string displayName = null,
		int addonSlotCapacity = 0)
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

		Building building = CreateBuildingInstance(resolvedName, ownedCells, buildingType, addonSlotCapacity);
		Register(building);

		for (int i = 0; i < ownedCells.Count; ++i)
			ownedCells[i].SetBuildingId(building.RuntimeBuildingId);

		return building;
	}

	public void Unregister(Building building)
	{
		if (building == null)
			return;

		if (GameContext.HasInstance)
			GameContext.Instance.BuildingAddonSvc?.RemoveAll(building);

		RemoveBuildingLinks(building);
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

	public void RefreshItemContainerState(IItemContainer container)
	{
		if (container == null)
			return;

		for (int i = 0; i < registeredBuildings.Count; ++i)
		{
			Building building = registeredBuildings[i];
			building?.ItemIndex.RefreshContainer(container);
		}
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

	public bool TrySetSuitRemovalAllowed(Building building, bool allowed)
	{
		if (building == null || registeredBuildings.Contains(building) == false)
			return false;

		if (allowed && building.CanControlSuitRemoval() == false)
			return false;

		building.SetSuitRemovalAllowed(allowed);
		if (allowed == false)
			ForceSuitsOnInsideHumans(building.RuntimeBuildingId);
		return true;
	}

	public void NormalizeResearchGatedPolicies()
	{
		for (int i = 0; i < registeredBuildings.Count; ++i)
		{
			Building building = registeredBuildings[i];
			if (building == null)
				continue;

			if (building.OverrideCapsuleThreshold && building.CanControlCapsuleThreshold() == false)
				building.SetOverrideCapsuleThreshold(false);

			if (building.SuitRemovalAllowed && building.CanControlSuitRemoval() == false)
			{
				building.SetSuitRemovalAllowed(false);
				ForceSuitsOnInsideHumans(building.RuntimeBuildingId);
			}
		}
	}

	private static void ForceSuitsOnInsideHumans(uint buildingId)
	{
		if (buildingId == 0 || GameContext.HasInstance == false)
			return;

		WorkerManager workerManager = GameContext.Instance.WorkerMgr;
		GridService gridService = GameContext.Instance.GridService;
		if (workerManager == null || gridService == null)
			return;

		IReadOnlyList<AIWorker> workers = workerManager.Workers;
		for (int i = 0; i < workers.Count; ++i)
		{
			if (workers[i] is not HumanWorker human)
				continue;

			GridCell cell = gridService.GetCell(human.GridPosition);
			if (cell != null && cell.BuildingId == buildingId)
				human.ForceSuitOn();
		}
	}

	public bool CanLinkBuildings(Building sourceBuilding, Building targetBuilding, out string reason)
	{
		reason = string.Empty;
		if (sourceBuilding == null || targetBuilding == null)
		{
			reason = "Both source and target buildings are required.";
			return false;
		}

		if (sourceBuilding == targetBuilding)
		{
			reason = "A building cannot connect to itself.";
			return false;
		}

		if (IsRegisteredBuilding(sourceBuilding) == false || IsRegisteredBuilding(targetBuilding) == false)
		{
			reason = "Both buildings must be registered before linking.";
			return false;
		}

		if (sourceBuilding.HasOutputBuilding(targetBuilding.RuntimeBuildingId))
		{
			reason = "That building link already exists.";
			return false;
		}

		if (targetBuilding.HasOutputBuilding(sourceBuilding.RuntimeBuildingId))
		{
			reason = "Reverse-direction building links are not allowed.";
			return false;
		}

		return true;
	}

	public bool TryLinkBuildings(Building sourceBuilding, Building targetBuilding)
	{
		if (CanLinkBuildings(sourceBuilding, targetBuilding, out _) == false)
			return false;

		if (sourceBuilding.AddOutputBuilding(targetBuilding.RuntimeBuildingId) == false)
			return false;

		if (targetBuilding.AddInputBuilding(sourceBuilding.RuntimeBuildingId))
			return true;

		sourceBuilding.RemoveOutputBuilding(targetBuilding.RuntimeBuildingId);
		return false;
	}

	public bool TryUnlinkBuildings(Building sourceBuilding, Building targetBuilding)
	{
		if (sourceBuilding == null || targetBuilding == null)
			return false;

		bool removedOutput = sourceBuilding.RemoveOutputBuilding(targetBuilding.RuntimeBuildingId);
		bool removedInput = targetBuilding.RemoveInputBuilding(sourceBuilding.RuntimeBuildingId);
		return removedOutput || removedInput;
	}

	public bool TryGetInputBuildings(Building building, List<Building> results)
	{
		return TryResolveConnectedBuildings(building?.InputBuildingIds, results);
	}

	public bool TryGetOutputBuildings(Building building, List<Building> results)
	{
		return TryResolveConnectedBuildings(building?.OutputBuildingIds, results);
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

	public Building RestoreBuilding(
		List<GridCell> ownedCells,
		uint runtimeBuildingId,
		BuildingType buildingType,
		string displayName,
		BuildingState state,
		BuildingWorkScope workScope,
		bool overrideCapsuleThreshold,
		float capsuleThresholdPercent,
		bool suitRemovalAllowed,
		int addonSlotCapacity = 0)
	{
		if (ownedCells == null || ownedCells.Count <= 0)
			return null;

		Building building = CreateBuildingInstance(displayName, ownedCells, buildingType, addonSlotCapacity);
		building.AssignRuntimeBuildingId(runtimeBuildingId);
		building.SetState(state);
		building.SetWorkScope(workScope);
		building.SetOverrideCapsuleThreshold(overrideCapsuleThreshold);
		building.SetCapsuleThresholdPercent(capsuleThresholdPercent);
		building.SetSuitRemovalAllowed(suitRemovalAllowed);
		Register(building);

		for (int i = 0; i < ownedCells.Count; ++i)
			ownedCells[i]?.SetBuildingId(building.RuntimeBuildingId);

		return building;
	}

	public void RestoreBuildingLinks(BuildingManagerSaveData data)
	{
		if (data?.Buildings == null)
			return;

		foreach (BuildingSaveData buildingData in data.Buildings)
		{
			if (buildingData == null ||
				buildingData.RuntimeBuildingId == 0 ||
				buildingData.OutputBuildingIds == null ||
				buildingData.OutputBuildingIds.Count <= 0 ||
				TryGetBuilding(buildingData.RuntimeBuildingId, out Building sourceBuilding) == false ||
				sourceBuilding == null)
			{
				continue;
			}

			for (int i = 0; i < buildingData.OutputBuildingIds.Count; ++i)
			{
				uint targetBuildingId = buildingData.OutputBuildingIds[i];
				if (TryGetBuilding(targetBuildingId, out Building targetBuilding) == false || targetBuilding == null)
					continue;

				TryLinkBuildings(sourceBuilding, targetBuilding);
			}
		}
	}

	private static Building CreateBuildingInstance(
		string displayName,
		List<GridCell> ownedCells,
		BuildingType buildingType,
		int addonSlotCapacity)
	{
		Building building = buildingType switch
		{
			BuildingType.Staging => new StagingBuilding(displayName, ownedCells),
			BuildingType.Storage => new StorageBuilding(displayName, ownedCells),
			BuildingType.Packing => new PackingBuilding(displayName, ownedCells),
			BuildingType.Launch => new LaunchBuilding(displayName, ownedCells),
			_ => new Building(displayName, ownedCells, buildingType),
		};

		building.SetAddonSlotCapacity(addonSlotCapacity);
		return building;
	}

	private bool IsRuntimeIdInUse(uint runtimeId, Building currentBuilding)
	{
		return buildingsById.TryGetValue(runtimeId, out var existing) && existing != null && existing != currentBuilding;
	}

	private bool IsRegisteredBuilding(Building building)
	{
		return building != null &&
			building.RuntimeBuildingId != 0 &&
			buildingsById.TryGetValue(building.RuntimeBuildingId, out Building registeredBuilding) &&
			registeredBuilding == building &&
			registeredBuildings.Contains(building);
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

	private bool TryResolveConnectedBuildings(IReadOnlyCollection<uint> buildingIds, List<Building> results)
	{
		results?.Clear();
		if (buildingIds == null || buildingIds.Count <= 0 || results == null)
			return false;

		foreach (uint buildingId in buildingIds)
		{
			if (TryGetBuilding(buildingId, out Building linkedBuilding) == false || linkedBuilding == null)
				continue;

			results.Add(linkedBuilding);
		}

		return results.Count > 0;
	}

	private void RemoveBuildingLinks(Building building)
	{
		if (building == null)
			return;

		List<uint> inputIds = building.InputBuildingIds.Count > 0 ? new List<uint>(building.InputBuildingIds) : null;
		List<uint> outputIds = building.OutputBuildingIds.Count > 0 ? new List<uint>(building.OutputBuildingIds) : null;

		if (inputIds != null)
		{
			for (int i = 0; i < inputIds.Count; ++i)
			{
				if (TryGetBuilding(inputIds[i], out Building sourceBuilding) && sourceBuilding != null)
					sourceBuilding.RemoveOutputBuilding(building.RuntimeBuildingId);
			}
		}

		if (outputIds != null)
		{
			for (int i = 0; i < outputIds.Count; ++i)
			{
				if (TryGetBuilding(outputIds[i], out Building targetBuilding) && targetBuilding != null)
					targetBuilding.RemoveInputBuilding(building.RuntimeBuildingId);
			}
		}

		building.ClearBuildingLinks();
	}
}
