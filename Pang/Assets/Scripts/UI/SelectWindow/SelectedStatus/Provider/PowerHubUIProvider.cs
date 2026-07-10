using UnityEngine;

public sealed class PowerHubUIProvider : UIProvider<PowerHub>
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
}
