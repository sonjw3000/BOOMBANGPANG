using UnityEngine;

public class CargoPortUIProvider : UIProvider<CargoPort>, IShelfBaseUIProvider
{
	Component IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Cargo Port";
	public override string Subtitle => currentTarget != null ? currentTarget.PortRoleLabel : "Unknown type";
	public override Sprite Icon => null;

	public string CapacityDisplay => currentTarget != null ? $"{currentTarget.MaxSize:0.0} units" : "0.0 units";
	public string CurrentSizeDisplay => currentTarget != null ? $"{currentTarget.TotalSize:0.0} units" : "0.0 units";
	public string FilledPercentDisplay => currentTarget != null ? $"{currentTarget.FilledPercent:0.0}%" : "0.0%";

	public ItemContainerDisplayInfo GetItemDisplay() => new()
	{
		ContainerName = "Docked Capsule",
		HasContainer = currentTarget?.DockedCapsule != null,
		Items = ItemContainerDisplayUtility.BuildItemRows(currentTarget?.DockedCapsule),
	};

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Capacity", CapacityDisplay));
		infoBlocks.Add(new KeyValueBlock("Current Size", CurrentSizeDisplay));
		infoBlocks.Add(new KeyValueBlock("Filled", FilledPercentDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(CapacityDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(CurrentSizeDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(FilledPercentDisplay);
	}
}
