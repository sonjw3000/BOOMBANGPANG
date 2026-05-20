using UnityEngine;

public interface IShelfBaseUIProvider
{
	ShelfBase Target { get; }
	string Name { get; }
	string Subtitle { get; }
	string CapacityDisplay { get; }
	string CurrentSizeDisplay { get; }
	string FilledPercentDisplay { get; }
	ItemContainerDisplayInfo GetItemDisplay();
}

public abstract class ShelfBaseUIProviderBase<TShelf> : UIProvider<TShelf>, IShelfBaseUIProvider
	where TShelf : ShelfBase
{
	ShelfBase IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Shelf";
	public override Sprite Icon => null;

	public string CapacityDisplay => currentTarget != null ? $"{currentTarget.MaxSize:0.0} units" : "0.0 units";
	public string CurrentSizeDisplay => currentTarget != null ? $"{currentTarget.TotalSize:0.0} units" : "0.0 units";
	public string FilledPercentDisplay => currentTarget != null ? $"{currentTarget.FilledPercent:0.0}%" : "0.0%";

	public ItemContainerDisplayInfo GetItemDisplay() => new()
	{
		ContainerName = "Stored Items",
		HasContainer = currentTarget != null,
		Items = ItemContainerDisplayUtility.BuildItemRows(currentTarget),
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
