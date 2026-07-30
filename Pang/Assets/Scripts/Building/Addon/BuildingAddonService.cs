using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingAddonService
{
	private static readonly IReadOnlyList<BuildingAddonDefinition> EmptyDefinitions =
		Array.Empty<BuildingAddonDefinition>();

	private BuildingAddonCatalog catalog;
	private BuildingManager buildingManager;
	private EconomyService economyService;

	public event Action<Building, BuildingAddon> OnAddonInstalled;
	public event Action<Building, BuildingAddon> OnAddonRemoved;

	public IReadOnlyList<BuildingAddonDefinition> Definitions =>
		catalog != null ? catalog.Definitions : EmptyDefinitions;

	public void Initialize(
		BuildingAddonCatalog targetCatalog,
		BuildingManager targetBuildingManager,
		EconomyService targetEconomyService)
	{
		catalog = targetCatalog;
		buildingManager = targetBuildingManager;
		economyService = targetEconomyService;
	}

	public bool CanInstall(
		Building building,
		BuildingAddonDefinition definition,
		out string reason)
	{
		return CanInstall(building, definition, checkEconomy: true, out reason);
	}

	public bool TryInstall(
		Building building,
		BuildingAddonDefinition definition,
		out string reason)
	{
		if (CanInstall(building, definition, checkEconomy: true, out reason) == false)
			return false;

		BuildingAddon addon = CreateRuntimeAddon(definition);
		if (building.TryAddAddon(addon) == false)
		{
			reason = "The addon could not be installed.";
			return false;
		}

		economyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = -definition.Cost,
			reputationDelta = 0.0f,
			reason = EconomyTransaction.Reason.Place,
		});

		OnAddonInstalled?.Invoke(building, addon);
		reason = string.Empty;
		return true;
	}

	public bool TryRemove(Building building, BuildingAddon addon)
	{
		return TryRemove(building, addon, out _);
	}

	public bool TryRemove(Building building, BuildingAddon addon, out string reason)
	{
		if (IsRegisteredBuilding(building) == false)
		{
			reason = "The building is not registered.";
			return false;
		}

		if (addon == null || building.ContainsAddon(addon) == false)
		{
			reason = "The addon is not installed in this building.";
			return false;
		}

		if (building.TryRemoveAddon(addon) == false)
		{
			reason = "The addon could not be removed.";
			return false;
		}

		OnAddonRemoved?.Invoke(building, addon);
		reason = string.Empty;
		return true;
	}

	public void RestoreState(BuildingManagerSaveData data)
	{
		if (data?.Buildings == null)
			return;

		for (int buildingIndex = 0; buildingIndex < data.Buildings.Count; ++buildingIndex)
		{
			BuildingSaveData buildingData = data.Buildings[buildingIndex];
			if (buildingData == null ||
				buildingData.RuntimeBuildingId == 0 ||
				buildingData.Addons == null ||
				buildingManager == null ||
				buildingManager.TryGetBuilding(buildingData.RuntimeBuildingId, out Building building) == false)
			{
				continue;
			}

			for (int addonIndex = 0; addonIndex < buildingData.Addons.Count; ++addonIndex)
			{
				BuildingAddonSaveData addonData = buildingData.Addons[addonIndex];
				if (addonData == null)
					continue;

				BuildingAddonDefinition definition = catalog?.FindById(addonData.DefinitionId);
				if (definition == null)
				{
					Debug.LogWarning(
						$"[Save] Missing building addon definition {addonData.DefinitionId} for building {building.RuntimeBuildingId}.");
					continue;
				}

				if (CanInstall(building, definition, checkEconomy: false, out string reason) == false)
				{
					Debug.LogWarning(
						$"[Save] Could not restore building addon {definition.AddonId} for building {building.RuntimeBuildingId}: {reason}");
					continue;
				}

				BuildingAddon addon = CreateRuntimeAddon(definition);
				addon.RestoreHealth(addonData.Health);
				addon.SetWearFromSave(addonData.Wear);
				if (building.TryAddAddon(addon) == false)
					continue;

				OnAddonInstalled?.Invoke(building, addon);
			}
		}
	}

	internal void RemoveAll(Building building)
	{
		if (building == null || building.InstalledAddons.Count <= 0)
			return;

		List<BuildingAddon> addons = new(building.InstalledAddons);
		for (int i = addons.Count - 1; i >= 0; --i)
		{
			BuildingAddon addon = addons[i];
			if (building.TryRemoveAddon(addon))
				OnAddonRemoved?.Invoke(building, addon);
		}
	}

	private bool CanInstall(
		Building building,
		BuildingAddonDefinition definition,
		bool checkEconomy,
		out string reason)
	{
		if (IsRegisteredBuilding(building) == false)
		{
			reason = "The building is not registered.";
			return false;
		}

		if (definition == null ||
			string.IsNullOrWhiteSpace(definition.AddonId) ||
			catalog == null ||
			ReferenceEquals(catalog.FindById(definition.AddonId), definition) == false)
		{
			reason = "The addon definition is not registered.";
			return false;
		}

		if (definition.IsAllowedFor(building.Type) == false)
		{
			reason = "This addon cannot be installed in this building type.";
			return false;
		}

		if (building.AvailableAddonSlots <= 0)
		{
			reason = "No addon slots are available.";
			return false;
		}

		if (checkEconomy && (economyService == null || economyService.CanAfford(definition.Cost) == false))
		{
			reason = "Not enough money.";
			return false;
		}

		reason = string.Empty;
		return true;
	}

	private bool IsRegisteredBuilding(Building building)
	{
		return building != null &&
			buildingManager != null &&
			building.RuntimeBuildingId != 0 &&
			buildingManager.TryGetBuilding(building.RuntimeBuildingId, out Building registered) &&
			ReferenceEquals(building, registered);
	}

	private static BuildingAddon CreateRuntimeAddon(BuildingAddonDefinition definition)
	{
		return definition?.AddonType switch
		{
			BuildingAddonType.OxygenSupply => new OxygenSupplyBuildingAddon(definition),
			_ => new BuildingAddon(definition),
		};
	}
}
