using System;
using System.Collections.Generic;
using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class BuildingUIProvider : UIProvider<BuildingSelectionProxy>, ISelectionInspectorProvider
{
	private Building Building => currentTarget != null ? currentTarget.Building : null;
	private BuildingAddonService AddonService =>
		GameContext.HasInstance ? GameContext.Instance.BuildingAddonSvc : null;
	private string addonActionMessage = string.Empty;
	private int addonActionVersion;

	public override string Name => Building != null ? Building.DisplayName : "Unknown Building";
	public override string Subtitle => Building != null ? Building.Type.ToString() : "Unknown Building";
	public override Sprite Icon => null;

	public string StateDisplay => Building != null ? Building.State.ToString() : "Unknown";
	public string WorkScopeDisplay => Building != null ? BuildingWorkScopeUtility.ToDisplayString(Building.WorkScope) : "Unknown";
	public int CellCount => Building != null ? Building.OccupiedCells.Count : 0;
	public int FacilityCount => Building != null ? Building.OccupiedFacilities.Count : 0;
	public int CargoPortCount => Building != null ? Building.OccupiedCargoPorts.Count : 0;
	public string AverageTemperatureDisplay => Building != null ? $"{Building.AverageTemperatureCelsius:F1} °C" : "Unknown";
	private bool CanDisplayTemperature =>
		GameContext.HasInstance &&
		GameContext.Instance.ResearchService?.IsResearched(ResearchIds.TemperatureMonitoring) == true;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("State", StateDisplay));
		infoBlocks.Add(new KeyValueBlock("WorkScope", WorkScopeDisplay));
		if (CanDisplayTemperature)
			infoBlocks.Add(new KeyValueBlock("Temperature", AverageTemperatureDisplay));
		infoBlocks.Add(new KeyValueBlock("Facilities", FacilityCount.ToString()));
	}

	public override void OnUpdate()
	{
		int requiredCount = CanDisplayTemperature ? 4 : 3;
		if (infoBlocks.Count < requiredCount)
			return;

		int index = 0;
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(StateDisplay);
		(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(WorkScopeDisplay);
		if (CanDisplayTemperature)
			(infoBlocks[index++] as KeyValueBlock)?.UpdateValue(AverageTemperatureDisplay);
		(infoBlocks[index] as KeyValueBlock)?.UpdateValue(FacilityCount.ToString());
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Facilities", GetFacilitiesVersion, BuildFacilitiesPanel);
		model.AddTab("Addons", GetAddonsVersion, BuildAddonsPanel);
		model.AddTab("Flow", GetFlowVersion, BuildFlowPanel);
		model.AddTab("Settings", GetSettingsVersion, BuildSettingsPanel);
		model.AddOverview("State", () => StateDisplay);
		model.AddOverview("Work Scope", () => WorkScopeDisplay);
		if (CanDisplayTemperature)
			model.AddOverview("Temperature", () => AverageTemperatureDisplay);
		model.AddOverview("Cells", () => CellCount.ToString());
		model.AddOverview("Facilities", () => FacilityCount.ToString());
		model.AddOverview("Cargo Ports", () => CargoPortCount.ToString());
		model.AddAction("Cycle Work Scope", CycleWorkScope, () => Building != null);
		model.AddAction("Toggle Threshold", ToggleThresholdOverride, CanControlCapsuleThreshold,
			tooltip: BuildThresholdTooltip);
		model.AddAction("Pending Demolition", MarkPendingDemolition, () => Building != null && Building.State != BuildingState.PendingDemolition, true);
		model.AddAction("Restore Active", RestoreActive, () => Building != null && Building.State != BuildingState.Active);
	}

	private int GetFacilitiesVersion()
	{
		unchecked
		{
			int version = FacilityCount;
			if (Building?.OccupiedFacilities == null) return version;
			for (int i = 0; i < Building.OccupiedFacilities.Count; ++i)
				version = version * 31 + (Building.OccupiedFacilities[i]?.GetType().GetHashCode() ?? 0);
			return version;
		}
	}

	private SelectionDetailPanelModel BuildFacilitiesPanel()
	{
		SelectionDetailPanelModel panel = new() { Title = "FACILITIES", Summary = $"{FacilityCount} facilities · {CargoPortCount} cargo ports" };
		if (Building?.OccupiedFacilities == null) return panel;
		SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
		for (int i = 0; i < Building.OccupiedFacilities.Count; ++i)
		{
			IFacility facility = Building.OccupiedFacilities[i];
			if (facility == null) continue;
			string typeName = facility.GetType().Name;
			counts[typeName] = counts.TryGetValue(typeName, out int count) ? count + 1 : 1;
		}
		foreach (KeyValuePair<string, int> pair in counts)
			panel.Rows.Add(new SelectionDetailRow { Primary = pair.Key, Trailing = $"×{pair.Value}", Secondary = "Installed in this building" });
		return panel;
	}

	private int GetAddonsVersion()
	{
		if (Building == null)
			return addonActionVersion;

		unchecked
		{
			int version = HashCode.Combine(
				Building.State,
				Building.AddonSlotCapacity,
				Building.AvailableAddonSlots,
				Mathf.RoundToInt(Building.PowerEfficiency * 1000.0f),
				addonActionVersion);
			IReadOnlyList<BuildingAddon> installedAddons = Building.InstalledAddons;
			if (installedAddons != null)
			{
				for (int i = 0; i < installedAddons.Count; ++i)
				{
					BuildingAddon addon = installedAddons[i];
					if (addon == null)
						continue;

					version = version * 31 + GetStableHash(addon.Definition?.AddonId);
					version = version * 31 + Mathf.RoundToInt(addon.Health * 10.0f);
					version = version * 31 + Mathf.RoundToInt(addon.MaxHealth * 10.0f);
					version = version * 31 + Mathf.RoundToInt(addon.Wear * 1000.0f);
					version = version * 31 + Mathf.RoundToInt(addon.WearEfficiency * 1000.0f);
					version = version * 31 + addon.PowerConsumption;
					version = version * 31 + Mathf.RoundToInt(addon.OxygenSupplyPerTick * 100.0f);
				}
			}

			return version;
		}
	}

	private SelectionDetailPanelModel BuildAddonsPanel()
	{
		int installedCount = Building?.InstalledAddons?.Count ?? 0;
		int slotCapacity = Building?.AddonSlotCapacity ?? 0;
		SelectionDetailPanelModel panel = new()
		{
			Title = "ADDONS",
			Summary = Building == null
				? "Building unavailable."
				: $"{installedCount} / {slotCapacity} slots used · {Building.AvailableAddonSlots} free",
		};

		if (string.IsNullOrWhiteSpace(addonActionMessage) == false)
			panel.Summary += $" · {addonActionMessage}";

		if (Building == null)
			return panel;

		IReadOnlyList<BuildingAddon> installedAddons = Building.InstalledAddons;
		if (installedAddons != null)
		{
			for (int i = 0; i < installedAddons.Count; ++i)
			{
				BuildingAddon addon = installedAddons[i];
				if (addon?.Definition == null)
					continue;

				BuildingAddon boundAddon = addon;
				float operatingEfficiency =
					FacilityEfficiency.GetOperatingEfficiency(Building, addon, addon);
				panel.Rows.Add(new SelectionDetailRow
				{
					Primary = addon.Definition.DisplayName,
					Trailing = $"O₂ {addon.OxygenSupplyPerTick:0.##}/tick",
					Secondary =
						$"Power {addon.PowerConsumption} · Operating {operatingEfficiency * 100.0f:0.#}% · " +
						$"HP {addon.Health:0.#}/{addon.MaxHealth:0.#} · Wear {addon.Wear * 100.0f:0.#}%",
					Thumbnail = addon.Definition.Icon,
					IsSlot = true,
					ActionLabel = "Remove",
					Action = () => RemoveAddon(boundAddon),
					CanExecute = () => Building != null && AddonService != null,
					IsDangerous = true,
					DisabledReason = AddonService == null ? "Addon service is unavailable." : string.Empty,
				});
			}
		}

		for (int slotIndex = installedCount; slotIndex < slotCapacity; ++slotIndex)
		{
			panel.Rows.Add(new SelectionDetailRow
			{
				Primary = "Empty Slot",
				Secondary = "Select + to browse the add-on catalog.",
				IsSlot = true,
				IsEmptySlot = true,
				ActionLabel = "Add Add-on",
				Action = OpenAddonCatalog,
				CanExecute = () => Building != null && Building.AvailableAddonSlots > 0,
				DisabledReason = string.Empty,
			});
		}

		return panel;
	}

	private void OpenAddonCatalog()
	{
		if (Building == null)
			return;

		GlobalStatusHud hud =
			UnityEngine.Object.FindAnyObjectByType<GlobalStatusHud>(FindObjectsInactive.Include);
		if (hud != null && hud.OpenBuildingAddonCatalog(Building))
			addonActionMessage = string.Empty;
		else
			addonActionMessage = "Add-on catalog is unavailable";
		addonActionVersion += 1;
	}

	private void RemoveAddon(BuildingAddon addon)
	{
		if (Building == null || AddonService == null || addon == null)
			return;

		string displayName = addon.Definition != null ? addon.Definition.DisplayName : "Addon";
		if (AddonService.TryRemove(Building, addon, out string reason))
			addonActionMessage = $"{displayName} removed";
		else
			addonActionMessage = string.IsNullOrWhiteSpace(reason) ? "Removal failed" : reason;

		addonActionVersion += 1;
	}

	private static int GetStableHash(string value)
	{
		if (string.IsNullOrEmpty(value))
			return 0;

		unchecked
		{
			int hash = 17;
			for (int i = 0; i < value.Length; ++i)
				hash = hash * 31 + value[i];
			return hash;
		}
	}

	private int GetFlowVersion()
	{
		unchecked
		{
			int version = 17;
			if (Building?.InputBuildingIds != null)
				foreach (uint buildingId in Building.InputBuildingIds) version = version * 31 + (int)buildingId;
			if (Building?.OutputBuildingIds != null)
				foreach (uint buildingId in Building.OutputBuildingIds) version = version * 31 + (int)buildingId;
			return version;
		}
	}

	private SelectionDetailPanelModel BuildFlowPanel()
	{
		SelectionDetailPanelModel panel = new() { Title = "FLOW", Summary = "Connections are managed in Build → Routing." };
		AddConnectedBuildings(panel, true);
		AddConnectedBuildings(panel, false);
		return panel;
	}

	private void AddConnectedBuildings(SelectionDetailPanelModel panel, bool inputs)
	{
		BuildingManager manager = currentTarget?.BuildingManager;
		List<Building> connected = new();
		bool found = inputs ? manager?.TryGetInputBuildings(Building, connected) == true : manager?.TryGetOutputBuildings(Building, connected) == true;
		if (found == false)
		{
			panel.Rows.Add(new SelectionDetailRow { Primary = inputs ? "Input" : "Output", Secondary = "None" });
			return;
		}
		for (int i = 0; i < connected.Count; ++i)
		{
			Building linked = connected[i];
			if (linked != null)
				panel.Rows.Add(new SelectionDetailRow { Primary = inputs ? "Input" : "Output", Trailing = linked.Type.ToString(), Secondary = linked.DisplayName });
		}
	}

	private int GetSettingsVersion()
	{
		if (Building == null) return 0;
		return HashCode.Combine(Building.WorkScope, Building.OverrideCapsuleThreshold, Mathf.RoundToInt(Building.CapsuleThresholdPercent));
	}

	private SelectionDetailPanelModel BuildSettingsPanel()
	{
		bool thresholdUnlocked = CanControlCapsuleThreshold();
		SelectionDetailPanelModel panel = new()
		{
			Title = "SETTINGS",
			Summary = SupportsCapsuleThreshold()
				? thresholdUnlocked ? "Outbound capsule release settings" : "Threshold control requires Workflow Policy Optimization."
				: "No capsule threshold setting for this building type."
		};
		if (Building == null) return panel;
		panel.Rows.Add(new SelectionDetailRow { Primary = "Work Scope", Secondary = WorkScopeDisplay });
		if (SupportsCapsuleThreshold())
		{
			panel.Rows.Add(new SelectionDetailRow { Primary = "Threshold Override", Secondary = Building.OverrideCapsuleThreshold ? "Enabled" : "Disabled" });
			panel.Rows.Add(new SelectionDetailRow { Primary = "Capsule Threshold", Secondary = $"{Mathf.RoundToInt(Building.CapsuleThresholdPercent)}%" });
			panel.HasSlider = true;
			panel.SliderLabel = "Capsule Threshold";
			panel.SliderValue = Building.CapsuleThresholdPercent;
			panel.SliderLowValue = 0.0f;
			panel.SliderHighValue = 100.0f;
			panel.SliderEnabled = thresholdUnlocked && Building.OverrideCapsuleThreshold;
			panel.SliderChanged = SetThreshold;
			panel.SliderTooltip = BuildThresholdTooltip;
		}
		return panel;
	}

	private void CycleWorkScope()
	{
		if (Building == null) return;
		int enumCount = Enum.GetValues(typeof(BuildingWorkScope)).Length;
		BuildingWorkScope next = (BuildingWorkScope)((((int)Building.WorkScope) + 1) % enumCount);
		currentTarget?.BuildingManager?.SetBuildingWorkScope(Building, next);
	}

	private bool SupportsCapsuleThreshold() =>
		Building != null &&
		(Building.Type == BuildingType.Storage ||
		 Building.Type == BuildingType.Packing ||
		 Building.Type == BuildingType.Launch);
	private bool CanControlCapsuleThreshold() => SupportsCapsuleThreshold() && Building.CanControlCapsuleThreshold();
	private UITooltipContent BuildThresholdTooltip()
	{
		const string title = "Building capsule threshold";
		const string description = "Override the global outbound release threshold for this building.";
		return CanControlCapsuleThreshold()
			? UITooltipContent.DescriptionOnly(title, description)
			: UITooltipContent.Locked(title, description,
				"Required research: Workflow Policy Optimization");
	}
	private void ToggleThresholdOverride() { if (SupportsCapsuleThreshold()) Building.TrySetOverrideCapsuleThreshold(Building.OverrideCapsuleThreshold == false); }
	private void SetThreshold(float value) { if (SupportsCapsuleThreshold()) Building.TrySetCapsuleThresholdPercent(value); }
	private void MarkPendingDemolition() { if (Building != null) currentTarget?.BuildingManager?.SetBuildingState(Building, BuildingState.PendingDemolition); }
	private void RestoreActive() { if (Building != null) currentTarget?.BuildingManager?.SetBuildingState(Building, BuildingState.Active); }
}
