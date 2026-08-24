using System;
using System.Collections.Generic;
using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class BuildingUIProvider : UIProvider<BuildingSelectionProxy>, ISelectionInspectorProvider
{
	private Building Building => currentTarget != null ? currentTarget.Building : null;
	private BuildingAddonService AddonService =>
		GameContext.HasInstance ? GameContext.Instance.BuildingAddonSvc : null;
	private OxygenService OxygenService =>
		GameContext.HasInstance ? GameContext.Instance.OxygenSvc : null;
	private WorkerManager WorkerManager =>
		GameContext.HasInstance ? GameContext.Instance.WorkerMgr : null;
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
		model.AddTab("Workforce", GetWorkforceVersion, BuildWorkforcePanel);
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
		model.AddAction("Work Monitor", OpenWorkMonitor, CanOpenWorkMonitor,
			tooltip: () => UITooltipContent.DescriptionOnly(
				"Work Monitor",
				"Open logistics demand and Task states for this building."));
		model.AddAction("Cycle Work Scope", CycleWorkScope, () => Building != null);
		model.AddAction("Toggle Threshold", ToggleThresholdOverride, CanControlCapsuleThreshold,
			tooltip: BuildThresholdTooltip);
		model.AddAction("Pending Demolition", MarkPendingDemolition, () => Building != null && Building.State != BuildingState.PendingDemolition, true);
		model.AddAction("Restore Active", RestoreActive, () => Building != null && Building.State != BuildingState.Active);
	}

	private int GetWorkforceVersion()
	{
		if (Building == null)
			return 0;

		unchecked
		{
			int version = (int)Building.RuntimeBuildingId;
			IReadOnlyList<WorkforceRole> roles = WorkforceRoleCatalog.GetRoles(Building.Type);
			for (int i = 0; i < roles.Count; ++i)
			{
				WorkforceRole role = roles[i];
				version = version * 31 + (int)role;
				if (WorkerManager?.TryGetWorkforceRoleSummary(
						Building.RuntimeBuildingId,
						role,
						out WorkforceRoleSummary summary) == true)
				{
					version = version * 31 + summary.FullCount;
					version = version * 31 + summary.PartialCount;
				}
			}

			return version;
		}
	}

	private SelectionDetailPanelModel BuildWorkforcePanel()
	{
		SelectionDetailPanelModel panel = new()
		{
			Title = "WORKFORCE",
			Summary = "Current operational workers by role",
		};
		if (Building == null)
			return panel;

		IReadOnlyList<WorkforceRole> roles = WorkforceRoleCatalog.GetRoles(Building.Type);
		for (int i = 0; i < roles.Count; ++i)
		{
			WorkforceRole role = roles[i];
			WorkforceRoleCatalog.TryGetDefinition(role, out WorkforceRoleDefinition definition);
			int operationalCount = 0;
			int partialCount = 0;
			if (WorkerManager?.TryGetWorkforceRoleSummary(
					Building.RuntimeBuildingId,
					role,
					out WorkforceRoleSummary summary) == true)
			{
				operationalCount = summary.OperationalCount;
				partialCount = summary.PartialCount;
			}

			panel.Rows.Add(new SelectionDetailRow
			{
				Primary = definition?.DisplayName ?? role.ToString(),
				Trailing = operationalCount.ToString(),
				Secondary = partialCount > 0
					? partialCount == 1
						? "1 worker is assigned to part of this role"
						: $"{partialCount} workers are assigned to part of this role"
					: string.Empty,
			});
		}

		return panel;
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
					if (addon.Definition != null)
					{
						version = version * 31 + (int)addon.Definition.AddonType;
						version = version * 31 +
							Mathf.RoundToInt(addon.Definition.MinimumTargetTemperatureCelsius * 10.0f);
						version = version * 31 +
							Mathf.RoundToInt(addon.Definition.MaximumTargetTemperatureCelsius * 10.0f);
						version = version * 31 +
							Mathf.RoundToInt(addon.Definition.TemperatureControlDegreesPerQuarterWeek * 100.0f);
						if (addon.Definition.AddonType == BuildingAddonType.TemperatureControl)
						{
							version = version * 31 +
								Mathf.RoundToInt(Building.TargetTemperatureCelsius * 10.0f);
							if (CanDisplayTemperature)
							{
								version = version * 31 +
									Mathf.RoundToInt(Building.AverageTemperatureCelsius * 10.0f);
							}
							TemperatureService temperatureService =
								GameContext.HasInstance ? GameContext.Instance.TemperatureSvc : null;
							version = version * 31 +
								(temperatureService?.IsTemperatureControlOperating(Building, addon) == true ? 1 : 0);
						}
					}
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

		if (AddonService != null &&
			AddonService.TryGetTargetTemperatureRange(
				Building,
				out float minimumTargetTemperature,
				out float maximumTargetTemperature))
		{
			panel.HasSlider = true;
			panel.SliderLabel =
				$"Target Temperature · Available " +
				$"{minimumTargetTemperature:0.#}–{maximumTargetTemperature:0.#} °C";
			panel.SliderValue = Building.TargetTemperatureCelsius;
			panel.SliderLowValue = minimumTargetTemperature;
			panel.SliderHighValue = maximumTargetTemperature;
			panel.SliderValueSuffix = " °C";
			panel.SliderChanged = SetTargetTemperature;
			panel.Rows.Add(new SelectionDetailRow
			{
				Primary = "Climate Control",
				Trailing =
					$"{minimumTargetTemperature:0.#}–{maximumTargetTemperature:0.#} °C",
				Secondary =
					$"Target {Building.TargetTemperatureCelsius:0.#} °C · " +
					(CanDisplayTemperature
						? $"Current {Building.AverageTemperatureCelsius:0.#} °C"
						: "Current temperature requires Temperature Monitoring"),
			});
		}

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
				bool isTemperatureControl =
					addon.Definition.AddonType == BuildingAddonType.TemperatureControl;
				string trailing = isTemperatureControl
					? FormatTemperatureRange(addon.Definition)
					: $"O₂ {addon.OxygenSupplyPerTick:0.##}/tick";
				string secondary = isTemperatureControl
					? BuildTemperatureControlSecondary(addon, operatingEfficiency)
					:
						$"Power {addon.PowerConsumption} · Operating {operatingEfficiency * 100.0f:0.#}% · " +
						$"HP {addon.Health:0.#}/{addon.MaxHealth:0.#} · Wear {addon.Wear * 100.0f:0.#}%";
				panel.Rows.Add(new SelectionDetailRow
				{
					Primary = addon.Definition.DisplayName,
					Trailing = trailing,
					Secondary = secondary,
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

	private void SetTargetTemperature(float value)
	{
		if (Building == null || AddonService == null)
			return;

		float targetTemperature = Mathf.Round(value);
		if (Mathf.Approximately(Building.TargetTemperatureCelsius, targetTemperature))
			return;

		if (AddonService.TrySetTargetTemperature(Building, targetTemperature, out string reason))
			addonActionMessage = string.Empty;
		else
			addonActionMessage =
				string.IsNullOrWhiteSpace(reason) ? "Target temperature could not be changed" : reason;

		addonActionVersion += 1;
	}

	private string BuildTemperatureControlSecondary(
		BuildingAddon addon,
		float operatingEfficiency)
	{
		BuildingAddonDefinition definition = addon.Definition;
		string status;
		if (addon.Health <= 0.0f)
		{
			status = "Out of Service";
		}
		else if (Building.PowerEfficiency <= 0.0f)
		{
			status = "No Power";
		}
		else
		{
			TemperatureService temperatureService =
				GameContext.HasInstance ? GameContext.Instance.TemperatureSvc : null;
			status =
				temperatureService != null &&
				temperatureService.IsTemperatureControlOperating(Building, addon)
					? "Operating"
					: "Standby";
		}

		return
			$"{BuildDirectionDescription(definition)} · " +
			$"Output {definition.TemperatureControlDegreesPerQuarterWeek:0.##} °C/quarter-week · {status} · " +
			$"Power {addon.PowerConsumption} · Efficiency {operatingEfficiency * 100.0f:0.#}% · " +
			$"HP {addon.Health:0.#}/{addon.MaxHealth:0.#} · Wear {addon.Wear * 100.0f:0.#}%";
	}

	private static string FormatTemperatureRange(BuildingAddonDefinition definition)
	{
		return
			$"{definition.MinimumTargetTemperatureCelsius:0.#}–" +
			$"{definition.MaximumTargetTemperatureCelsius:0.#} °C";
	}

	private static string BuildDirectionDescription(BuildingAddonDefinition definition)
	{
		if (definition.CanCool && definition.CanHeat)
			return "Cooling + Heating";
		if (definition.CanCool)
			return "Cooling";
		if (definition.CanHeat)
			return "Heating";
		return "No control";
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

	private bool CanOpenWorkMonitor()
	{
		Building building = Building;
		return building != null &&
			building.RuntimeBuildingId != 0 &&
			currentTarget?.BuildingManager?.TryGetBuilding(
				building.RuntimeBuildingId,
				out Building registeredBuilding) == true &&
			ReferenceEquals(building, registeredBuilding);
	}

	private void OpenWorkMonitor()
	{
		if (CanOpenWorkMonitor() == false)
			return;

		uint buildingId = Building.RuntimeBuildingId;
		GlobalStatusHud hud =
			UnityEngine.Object.FindAnyObjectByType<GlobalStatusHud>(FindObjectsInactive.Include);
		hud?.OpenWorkflowMonitor(buildingId);
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
		return HashCode.Combine(
			Building.WorkScope,
			Building.OverrideCapsuleThreshold,
			Mathf.RoundToInt(Building.CapsuleThresholdPercent),
			Building.SuitRemovalAllowed,
			Building.CanControlSuitRemoval(),
			Mathf.RoundToInt((OxygenService?.GetAverageOxygen(Building) ?? 0.0f) * 10.0f),
			OxygenService?.GetSuitlessHumanCount(Building) ?? 0,
			Mathf.RoundToInt((OxygenService?.GetNetOxygenPerTick(Building) ?? 0.0f) * 100.0f));
	}

	private SelectionDetailPanelModel BuildSettingsPanel()
	{
		bool thresholdUnlocked = CanControlCapsuleThreshold();
		SelectionDetailPanelModel panel = new()
		{
			Title = "SETTINGS",
			Summary = "Building operating policies",
		};
		if (Building == null) return panel;
		panel.Rows.Add(new SelectionDetailRow { Primary = "Work Scope", Secondary = WorkScopeDisplay });

		float averageOxygen = OxygenService?.GetAverageOxygen(Building) ?? GridCell.DefaultOxygen;
		int suitlessHumanCount = OxygenService?.GetSuitlessHumanCount(Building) ?? 0;
		float oxygenConsumption =
			suitlessHumanCount * (OxygenService?.HumanOxygenConsumptionPerTick ?? 0.0f);
		float oxygenSupply = OxygenService?.GetOxygenSupplyPerTick(Building) ?? 0.0f;
		float fireConsumption = OxygenService?.GetFireOxygenConsumptionPerTick(Building) ?? 0.0f;
		float netOxygen = oxygenSupply - oxygenConsumption - fireConsumption;
		panel.Rows.Add(new SelectionDetailRow
		{
			Primary = "Indoor O2",
			Trailing = $"{averageOxygen:0.#}%",
			Secondary =
				$"{suitlessHumanCount} suitless humans · Consumption {oxygenConsumption:0.##}/tick · " +
				$"Net {netOxygen:+0.##;-0.##;0}/tick",
		});

		bool suitPolicyUnlocked = Building.CanControlSuitRemoval();
		panel.HasToggle = true;
		panel.ToggleLabel = "Allow EVA Suit Removal";
		panel.ToggleValue = Building.SuitRemovalAllowed;
		panel.ToggleEnabled = suitPolicyUnlocked;
		panel.ToggleChanged = SetSuitRemovalAllowed;
		panel.ToggleTooltip = BuildSuitRemovalTooltip;
		float oxygenPerHuman = OxygenService?.HumanOxygenConsumptionPerTick ?? 1.0f;
		panel.ToggleDescription = suitPolicyUnlocked
			? Building.SuitRemovalAllowed
				? $"After airlock entry: 200% speed at 100 O2, 100% at 80 O2; {oxygenPerHuman:0.##} O2/tick each."
				: "Humans keep EVA suits on and do not consume building oxygen."
			: "Required research: Indoor Work Protocols";
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
	private UITooltipContent BuildSuitRemovalTooltip()
	{
		const string title = "Allow EVA Suit Removal";
		float oxygenPerHuman = OxygenService?.HumanOxygenConsumptionPerTick ?? 1.0f;
		string description =
			"Human workers remove suits only after completing outside-to-inside airlock transit. " +
			$"Work speed is 200% at 100 O2 and 100% at 80 O2. They consume {oxygenPerHuman:0.##} O2 per tick and take damage at critical O2.";
		return Building?.CanControlSuitRemoval() == true
			? UITooltipContent.DescriptionOnly(title, description)
			: UITooltipContent.Locked(title, description, "Required research: Indoor Work Protocols");
	}
	private void ToggleThresholdOverride() { if (SupportsCapsuleThreshold()) Building.TrySetOverrideCapsuleThreshold(Building.OverrideCapsuleThreshold == false); }
	private void SetThreshold(float value) { if (SupportsCapsuleThreshold()) Building.TrySetCapsuleThresholdPercent(value); }
	private void SetSuitRemovalAllowed(bool allowed)
	{
		if (Building != null)
			currentTarget?.BuildingManager?.TrySetSuitRemovalAllowed(Building, allowed);
	}
	private void MarkPendingDemolition() { if (Building != null) currentTarget?.BuildingManager?.SetBuildingState(Building, BuildingState.PendingDemolition); }
	private void RestoreActive() { if (Building != null) currentTarget?.BuildingManager?.SetBuildingState(Building, BuildingState.Active); }
}
