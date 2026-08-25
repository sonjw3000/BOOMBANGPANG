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
	private ResearchService researchService;

	public event Action<Building, BuildingAddon> OnAddonInstalled;
	public event Action<Building, BuildingAddon> OnAddonRemoved;
	public event Action<Building, float, float> OnTargetTemperatureChanged;

	public IReadOnlyList<BuildingAddonDefinition> Definitions =>
		catalog != null ? catalog.Definitions : EmptyDefinitions;

	public void Initialize(
		BuildingAddonCatalog targetCatalog,
		BuildingManager targetBuildingManager,
		EconomyService targetEconomyService,
		ResearchService targetResearchService)
	{
		catalog = targetCatalog;
		buildingManager = targetBuildingManager;
		economyService = targetEconomyService;
		researchService = targetResearchService;
	}

	public bool CanInstall(
		Building building,
		BuildingAddonDefinition definition,
		out string reason)
	{
		return CanInstall(building, definition, checkPurchaseRequirements: true, out reason);
	}

	public bool TryInstall(
		Building building,
		BuildingAddonDefinition definition,
		out string reason)
	{
		if (CanInstall(building, definition, checkPurchaseRequirements: true, out reason) == false)
			return false;

		BuildingAddon addon = CreateRuntimeAddon(definition);
		if (building.TryAddAddon(addon) == false)
		{
			reason = "The addon could not be installed.";
			return false;
		}

		ClampTargetTemperatureToSupportedRange(building);
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

		ClampTargetTemperatureToSupportedRange(building);
		OnAddonRemoved?.Invoke(building, addon);
		reason = string.Empty;
		return true;
	}

	public bool TrySetTargetTemperature(Building building, float targetTemperatureCelsius)
	{
		return TrySetTargetTemperature(building, targetTemperatureCelsius, out _);
	}

	public bool TrySetTargetTemperature(
		Building building,
		float targetTemperatureCelsius,
		out string reason)
	{
		if (IsRegisteredBuilding(building) == false)
		{
			reason = "The building is not registered.";
			return false;
		}

		if (float.IsNaN(targetTemperatureCelsius) || float.IsInfinity(targetTemperatureCelsius))
		{
			reason = "The target temperature is invalid.";
			return false;
		}

		List<TemperatureRange> ranges = GetSupportedTemperatureRanges(building);
		if (ranges.Count <= 0)
		{
			reason = "No installed addon supports temperature control.";
			return false;
		}

		if (IsTemperatureSupported(targetTemperatureCelsius, ranges) == false)
		{
			reason = "The installed addons do not support this target temperature.";
			return false;
		}

		ApplyTargetTemperature(building, targetTemperatureCelsius);
		reason = string.Empty;
		return true;
	}

	public bool TryGetTargetTemperatureRange(
		Building building,
		out float minimumTemperatureCelsius,
		out float maximumTemperatureCelsius)
	{
		List<TemperatureRange> ranges = GetSupportedTemperatureRanges(building);
		if (ranges.Count <= 0)
		{
			minimumTemperatureCelsius = GridCell.DefaultTemperatureCelsius;
			maximumTemperatureCelsius = GridCell.DefaultTemperatureCelsius;
			return false;
		}

		TemperatureRange selected = FindNearestRange(building.TargetTemperatureCelsius, ranges);
		minimumTemperatureCelsius = selected.Minimum;
		maximumTemperatureCelsius = selected.Maximum;
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
				buildingManager == null ||
				buildingManager.TryGetBuilding(buildingData.RuntimeBuildingId, out Building building) == false)
			{
				continue;
			}

			if (buildingData.Addons != null)
			{
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

					if (CanInstall(building, definition, checkPurchaseRequirements: false, out string reason) == false)
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

			RestoreTargetTemperature(building, buildingData.TargetTemperatureCelsius);
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
			{
				ClampTargetTemperatureToSupportedRange(building);
				OnAddonRemoved?.Invoke(building, addon);
			}
		}
	}

	private bool CanInstall(
		Building building,
		BuildingAddonDefinition definition,
		bool checkPurchaseRequirements,
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

		if (building.AvailableAddonSlots <= 0)
		{
			reason = "No addon slots are available.";
			return false;
		}

		if (checkPurchaseRequirements &&
			definition.RequiresResearch &&
			(researchService == null || researchService.IsResearched(definition.RequiredResearchUid) == false))
		{
			reason = $"Requires research: {definition.RequiredResearchUid}.";
			return false;
		}

		if (checkPurchaseRequirements &&
			(economyService == null || economyService.CanAfford(definition.Cost) == false))
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
			BuildingAddonType.TemperatureControl => new TemperatureControlBuildingAddon(definition),
			_ => new BuildingAddon(definition),
		};
	}

	private void RestoreTargetTemperature(Building building, float savedTargetTemperatureCelsius)
	{
		float target = float.IsNaN(savedTargetTemperatureCelsius) ||
			float.IsInfinity(savedTargetTemperatureCelsius)
			? GridCell.DefaultTemperatureCelsius
			: savedTargetTemperatureCelsius;

		List<TemperatureRange> ranges = GetSupportedTemperatureRanges(building);
		if (ranges.Count > 0)
			target = FindNearestSupportedTemperature(target, ranges);
		else
			target = GridCell.DefaultTemperatureCelsius;

		ApplyTargetTemperature(building, target);
	}

	private void ClampTargetTemperatureToSupportedRange(Building building)
	{
		if (building == null)
			return;

		List<TemperatureRange> ranges = GetSupportedTemperatureRanges(building);
		float target = ranges.Count > 0
			? FindNearestSupportedTemperature(building.TargetTemperatureCelsius, ranges)
			: GridCell.DefaultTemperatureCelsius;
		ApplyTargetTemperature(building, target);
	}

	private void ApplyTargetTemperature(Building building, float targetTemperatureCelsius)
	{
		if (building == null)
			return;

		float previous = building.TargetTemperatureCelsius;
		if (Mathf.Approximately(previous, targetTemperatureCelsius))
			return;

		building.SetTargetTemperatureCelsius(targetTemperatureCelsius);
		OnTargetTemperatureChanged?.Invoke(building, previous, targetTemperatureCelsius);
	}

	private static List<TemperatureRange> GetSupportedTemperatureRanges(Building building)
	{
		List<TemperatureRange> ranges = new();
		if (building == null)
			return ranges;

		IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
		for (int i = 0; i < addons.Count; ++i)
		{
			if (addons[i] is not TemperatureControlBuildingAddon addon ||
				(addon.CanCool == false && addon.CanHeat == false))
			{
				continue;
			}

			ranges.Add(new TemperatureRange(
				addon.MinimumTargetTemperatureCelsius,
				addon.MaximumTargetTemperatureCelsius));
		}

		if (ranges.Count <= 1)
			return ranges;

		ranges.Sort((left, right) => left.Minimum.CompareTo(right.Minimum));
		List<TemperatureRange> merged = new();
		TemperatureRange current = ranges[0];
		for (int i = 1; i < ranges.Count; ++i)
		{
			TemperatureRange next = ranges[i];
			if (next.Minimum <= current.Maximum || Mathf.Approximately(next.Minimum, current.Maximum))
			{
				current = new TemperatureRange(
					current.Minimum,
					Mathf.Max(current.Maximum, next.Maximum));
				continue;
			}

			merged.Add(current);
			current = next;
		}

		merged.Add(current);
		return merged;
	}

	private static bool IsTemperatureSupported(float temperatureCelsius, List<TemperatureRange> ranges)
	{
		for (int i = 0; i < ranges.Count; ++i)
		{
			TemperatureRange range = ranges[i];
			if (temperatureCelsius >= range.Minimum &&
				temperatureCelsius <= range.Maximum)
			{
				return true;
			}
		}

		return false;
	}

	private static float FindNearestSupportedTemperature(
		float temperatureCelsius,
		List<TemperatureRange> ranges)
	{
		TemperatureRange nearest = FindNearestRange(temperatureCelsius, ranges);
		return Mathf.Clamp(temperatureCelsius, nearest.Minimum, nearest.Maximum);
	}

	private static TemperatureRange FindNearestRange(
		float temperatureCelsius,
		List<TemperatureRange> ranges)
	{
		TemperatureRange nearest = ranges[0];
		float nearestValue = Mathf.Clamp(temperatureCelsius, nearest.Minimum, nearest.Maximum);
		float nearestDistance = Mathf.Abs(temperatureCelsius - nearestValue);

		for (int i = 1; i < ranges.Count; ++i)
		{
			TemperatureRange candidate = ranges[i];
			float candidateValue = Mathf.Clamp(temperatureCelsius, candidate.Minimum, candidate.Maximum);
			float candidateDistance = Mathf.Abs(temperatureCelsius - candidateValue);
			if (candidateDistance >= nearestDistance)
				continue;

			nearest = candidate;
			nearestDistance = candidateDistance;
		}

		return nearest;
	}

	private readonly struct TemperatureRange
	{
		public readonly float Minimum;
		public readonly float Maximum;

		public TemperatureRange(float minimum, float maximum)
		{
			Minimum = Mathf.Min(minimum, maximum);
			Maximum = Mathf.Max(minimum, maximum);
		}
	}
}
