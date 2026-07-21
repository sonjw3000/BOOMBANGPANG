using System;
using System.Collections.Generic;
using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class BuildingUIProvider : UIProvider<BuildingSelectionProxy>, ISelectionInspectorProvider
{
	private Building Building => currentTarget != null ? currentTarget.Building : null;

	public override string Name => Building != null ? Building.DisplayName : "Unknown Building";
	public override string Subtitle => Building != null ? Building.Type.ToString() : "Unknown Building";
	public override Sprite Icon => null;

	public string StateDisplay => Building != null ? Building.State.ToString() : "Unknown";
	public string WorkScopeDisplay => Building != null ? BuildingWorkScopeUtility.ToDisplayString(Building.WorkScope) : "Unknown";
	public int CellCount => Building != null ? Building.OccupiedCells.Count : 0;
	public int FacilityCount => Building != null ? Building.OccupiedFacilities.Count : 0;
	public int CargoPortCount => Building != null ? Building.OccupiedCargoPorts.Count : 0;
	public string AverageTemperatureDisplay => Building != null ? $"{Building.AverageTemperatureCelsius:F1} °C" : "Unknown";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("State", StateDisplay));
		infoBlocks.Add(new KeyValueBlock("WorkScope", WorkScopeDisplay));
		infoBlocks.Add(new KeyValueBlock("Temperature", AverageTemperatureDisplay));
		infoBlocks.Add(new KeyValueBlock("Facilities", FacilityCount.ToString()));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 4)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StateDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(WorkScopeDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(AverageTemperatureDisplay);
		(infoBlocks[3] as KeyValueBlock)?.UpdateValue(FacilityCount.ToString());
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Facilities", GetFacilitiesVersion, BuildFacilitiesPanel);
		model.AddTab("Flow", GetFlowVersion, BuildFlowPanel);
		model.AddTab("Settings", GetSettingsVersion, BuildSettingsPanel);
		model.AddOverview("State", () => StateDisplay);
		model.AddOverview("Work Scope", () => WorkScopeDisplay);
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

	private bool SupportsCapsuleThreshold() => Building != null && (Building.Type == BuildingType.Storage || Building.Type == BuildingType.Packing);
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
