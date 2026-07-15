using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class PowerHubUIProvider : UIProvider<PowerHub>, ISelectionInspectorProvider
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Power Hub";
	public override string Subtitle => "PowerHub";
	public override Sprite Icon => null;

	private string PowerDisplay => currentTarget != null
		? $"{currentTarget.CurrentPowerUsage}/{currentTarget.PowerCapacity}"
		: "0/0";

	private string ConnectedBuildingDisplay => currentTarget != null
		? currentTarget.ConnectedBuildingCount.ToString()
		: "0";
	private string RemainingPowerDisplay => currentTarget != null
		? Mathf.Max(0, currentTarget.PowerCapacity - currentTarget.CurrentPowerUsage).ToString()
		: "0";
	private string EfficiencyDisplay => currentTarget != null ? $"{currentTarget.PowerEfficiency * 100.0f:0.0}%" : "0.0%";
	private string VendorDisplay => currentTarget != null && currentTarget.HasPower ? "Connected" : "Unavailable";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Power", PowerDisplay));
		infoBlocks.Add(new KeyValueBlock("Connected Buildings", ConnectedBuildingDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 2)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(PowerDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(ConnectedBuildingDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Power", () => PowerDisplay);
		model.AddOverview("Remaining", () => RemainingPowerDisplay);
		model.AddOverview("Efficiency", () => EfficiencyDisplay);
		model.AddOverview("Buildings", () => ConnectedBuildingDisplay);
		model.AddOverview("Vendor", () => VendorDisplay);
	}
}
