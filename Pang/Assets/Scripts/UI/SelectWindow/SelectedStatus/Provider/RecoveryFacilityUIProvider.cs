using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class ChargingFacilityUIProvider :
	UIProvider<ChargingFacility>,
	ISelectionInspectorProvider
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Charging Facility";
	public override string Subtitle => "Charging Facility";
	public override Sprite Icon => null;

	private string UsageDisplay => currentTarget != null
		? $"{currentTarget.ActiveUserCount} active · {currentTarget.ReservedCount}/{currentTarget.Capacity} reserved"
		: "0 active · 0/0 reserved";
	private string ChargingTypeDisplay => currentTarget != null ? currentTarget.ChargingType.ToString() : ChargingType.None.ToString();
	private string RecoveryRateDisplay => currentTarget != null ? $"{currentTarget.BaseRecoveryPerSecond:0.##}/s" : "0/s";
	private string PowerDisplay => currentTarget != null ? currentTarget.PowerConsumption.ToString() : "0";
	private string PowerEfficiencyDisplay => currentTarget != null
		? $"{currentTarget.GetPowerEfficiency() * 100.0f:0.0}%"
		: "0.0%";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Use", UsageDisplay));
		infoBlocks.Add(new KeyValueBlock("Charging Type", ChargingTypeDisplay));
		infoBlocks.Add(new KeyValueBlock("Power", PowerDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(UsageDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(ChargingTypeDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(PowerDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Use", () => UsageDisplay);
		model.AddOverview("Charging Type", () => ChargingTypeDisplay);
		model.AddOverview("Base Charge Rate", () => RecoveryRateDisplay);
		model.AddOverview("Power Demand", () => PowerDisplay);
		model.AddOverview("Power Efficiency", () => PowerEfficiencyDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}
}

public sealed class RestFacilityUIProvider :
	UIProvider<RestFacility>,
	ISelectionInspectorProvider
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Rest Facility";
	public override string Subtitle => "Rest Facility";
	public override Sprite Icon => null;

	private string UsageDisplay => currentTarget != null
		? $"{currentTarget.ActiveUserCount} active · {currentTarget.ReservedCount}/{currentTarget.Capacity} reserved"
		: "0 active · 0/0 reserved";
	private string RecoveryRateDisplay => currentTarget != null ? $"{currentTarget.BaseRecoveryPerSecond:0.##}/s" : "0/s";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Use", UsageDisplay));
		infoBlocks.Add(new KeyValueBlock("Rest Rate", RecoveryRateDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 2)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(UsageDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(RecoveryRateDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Use", () => UsageDisplay);
		model.AddOverview("Rest Rate", () => RecoveryRateDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}
}
