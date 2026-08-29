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
	private float GlobalCapsuleThresholdPercent =>
		GameContext.HasInstance && GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.CargoPortThresholdPercent
			: 80.0f;
	private string addonActionMessage = string.Empty;
	private int addonActionVersion;
	private uint settingsDraftBuildingId;
	private bool settingsDraftActive;
	private BuildingWorkScope settingsWorkScopeDraft;
	private bool settingsThresholdOverrideDraft;
	private float settingsThresholdPercentDraft;
	private bool settingsSuitRemovalDraft;
	private string settingsActionMessage = string.Empty;
	private int settingsDraftVersion;

	public override string Name => Building != null ? Building.DisplayName : "Unknown Building";
	public override string Subtitle => Building != null ? "Building" : "Unknown Building";
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
		model.AddTabAction("Settings", "Settings", BeginSettingsEdit, () => Building != null,
			tooltip: () => UITooltipContent.DescriptionOnly(
				"Building settings",
				"Configure work scope, capsule threshold, and indoor policy."));
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
			IReadOnlyList<WorkforceRole> roles = WorkforceRoleCatalog.GetRoles(Building.RuntimeBuildingId);
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

		IReadOnlyList<WorkforceRole> roles = WorkforceRoleCatalog.GetRoles(Building.RuntimeBuildingId);
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
				panel.Rows.Add(new SelectionDetailRow { Primary = inputs ? "Input" : "Output", Secondary = linked.DisplayName });
		}
	}

	private int GetSettingsVersion()
	{
		if (Building == null) return 0;
		return HashCode.Combine(
			Building.WorkScope,
			HashCode.Combine(Building.OverrideCapsuleThreshold, settingsDraftVersion),
			Mathf.RoundToInt(Building.CapsuleThresholdPercent),
			Building.SuitRemovalAllowed,
			Building.CanControlSuitRemoval(),
			Mathf.RoundToInt((OxygenService?.GetAverageOxygen(Building) ?? 0.0f) * 10.0f),
			OxygenService?.GetSuitlessHumanCount(Building) ?? 0,
			Mathf.RoundToInt((OxygenService?.GetNetOxygenPerTick(Building) ?? 0.0f) * 100.0f));
	}

	private SelectionDetailPanelModel BuildSettingsPanel()
	{
		EnsureSettingsDraft();
		bool thresholdUnlocked = CanControlCapsuleThreshold();
		SelectionDetailPanelModel panel = new()
		{
			Title = "SETTINGS",
			Summary = "Review and apply building operating policies",
			PreferredWidth = 420.0f,
			PreferredHeight = 500.0f,
		};
		if (Building == null) return panel;

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

		if (SupportsCapsuleThreshold())
		{
			float effectiveThreshold = settingsThresholdOverrideDraft
				? settingsThresholdPercentDraft
				: GlobalCapsuleThresholdPercent;
			panel.Rows.Add(new SelectionDetailRow
			{
				Primary = "Effective Threshold",
				Trailing = $"{Mathf.RoundToInt(effectiveThreshold)}%",
				Secondary = settingsThresholdOverrideDraft ? "Building override" : "Global workflow threshold",
			});
			panel.HasSlider = true;
			panel.SliderLabel = "Capsule Threshold";
			panel.SliderValue = settingsThresholdPercentDraft;
			panel.SliderLowValue = 0.0f;
			panel.SliderHighValue = 100.0f;
			panel.SliderEnabled = thresholdUnlocked && settingsThresholdOverrideDraft;
			panel.SliderChanged = SetDraftThreshold;
			panel.SliderTooltip = BuildThresholdTooltip;
		}

		BuildingWorkScope[] workScopes = (BuildingWorkScope[])Enum.GetValues(typeof(BuildingWorkScope));
		List<string> workScopeChoices = new(workScopes.Length);
		for (int i = 0; i < workScopes.Length; ++i)
			workScopeChoices.Add(BuildingWorkScopeUtility.ToDisplayString(workScopes[i]));

		SelectionDetailEditorModel editor = new()
		{
			Message = BuildSettingsMessage(thresholdUnlocked),
			DropdownLabel = "Work Scope",
			DropdownChoices = workScopeChoices,
			DropdownIndex = Mathf.Max(0, Array.IndexOf(workScopes, settingsWorkScopeDraft)),
			DropdownChanged = index => SetDraftWorkScope(workScopes, index),
			ToggleLabel = "Policies",
			PrimaryActionLabel = "Apply",
			PrimaryAction = ApplySettings,
			SecondaryActionLabel = "Cancel",
			SecondaryAction = CancelSettingsEdit,
		};
		editor.Toggles.Add(new SelectionDetailToggleModel
		{
			Label = "Use Building Threshold",
			Value = settingsThresholdOverrideDraft,
			Enabled = thresholdUnlocked,
			Changed = SetDraftThresholdOverride,
		});
		editor.Toggles.Add(new SelectionDetailToggleModel
		{
			Label = "Allow EVA Suit Removal",
			Value = settingsSuitRemovalDraft,
			Enabled = Building.CanControlSuitRemoval(),
			Changed = SetDraftSuitRemoval,
		});
		panel.Editor = editor;
		return panel;
	}

	private bool SupportsCapsuleThreshold() => Building != null;
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
	private void BeginSettingsEdit()
	{
		if (Building == null)
			return;

		settingsDraftBuildingId = Building.RuntimeBuildingId;
		settingsDraftActive = true;
		settingsWorkScopeDraft = Building.WorkScope;
		settingsThresholdOverrideDraft = Building.OverrideCapsuleThreshold;
		settingsThresholdPercentDraft = Building.CapsuleThresholdPercent;
		settingsSuitRemovalDraft = Building.SuitRemovalAllowed;
		settingsActionMessage = string.Empty;
		++settingsDraftVersion;
	}

	private void EnsureSettingsDraft()
	{
		if (Building != null &&
			(settingsDraftActive == false || settingsDraftBuildingId != Building.RuntimeBuildingId))
		{
			BeginSettingsEdit();
		}
	}

	private string BuildSettingsMessage(bool thresholdUnlocked)
	{
		float effectiveThreshold = settingsThresholdOverrideDraft
			? settingsThresholdPercentDraft
			: GlobalCapsuleThresholdPercent;
		string message =
			$"Effective threshold {Mathf.RoundToInt(effectiveThreshold)}% · " +
			$"Global {Mathf.RoundToInt(GlobalCapsuleThresholdPercent)}%. " +
			"Changes are committed together with Apply.";
		if (thresholdUnlocked == false)
			message += " Threshold override requires Workflow Policy Optimization.";
		if (Building?.CanControlSuitRemoval() != true)
			message += " Suit removal requires Indoor Work Protocols.";
		if (string.IsNullOrWhiteSpace(settingsActionMessage) == false)
			message = settingsActionMessage + "\n" + message;
		return message;
	}

	private void SetDraftWorkScope(BuildingWorkScope[] choices, int index)
	{
		if (choices == null || index < 0 || index >= choices.Length)
			return;
		settingsWorkScopeDraft = choices[index];
		++settingsDraftVersion;
	}

	private void SetDraftThresholdOverride(bool value)
	{
		settingsThresholdOverrideDraft = value;
		++settingsDraftVersion;
	}

	private void SetDraftThreshold(float value)
	{
		settingsThresholdPercentDraft = Mathf.Clamp(value, 0.0f, 100.0f);
		++settingsDraftVersion;
	}

	private void SetDraftSuitRemoval(bool value)
	{
		settingsSuitRemovalDraft = value;
		++settingsDraftVersion;
	}

	private void ApplySettings()
	{
		Building building = Building;
		BuildingManager manager = currentTarget?.BuildingManager;
		if (building == null || manager == null)
		{
			settingsActionMessage = "Building settings are unavailable.";
			++settingsDraftVersion;
			return;
		}

		bool applied = manager.SetBuildingWorkScope(building, settingsWorkScopeDraft);
		if (building.OverrideCapsuleThreshold != settingsThresholdOverrideDraft)
			applied &= building.TrySetOverrideCapsuleThreshold(settingsThresholdOverrideDraft);
		if (settingsThresholdOverrideDraft)
			applied &= building.TrySetCapsuleThresholdPercent(settingsThresholdPercentDraft);
		applied &= manager.TrySetSuitRemovalAllowed(building, settingsSuitRemovalDraft);

		BeginSettingsEdit();
		settingsActionMessage = applied ? "Settings applied." : "Some settings could not be applied.";
		++settingsDraftVersion;
	}

	private void CancelSettingsEdit()
	{
		BeginSettingsEdit();
		settingsActionMessage = "Changes discarded.";
		++settingsDraftVersion;
	}
	private void MarkPendingDemolition() { if (Building != null) currentTarget?.BuildingManager?.SetBuildingState(Building, BuildingState.PendingDemolition); }
	private void RestoreActive() { if (Building != null) currentTarget?.BuildingManager?.SetBuildingState(Building, BuildingState.Active); }
}
