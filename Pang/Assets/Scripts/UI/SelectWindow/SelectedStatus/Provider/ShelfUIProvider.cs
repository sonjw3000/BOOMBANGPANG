using UnityEngine;

public sealed class ShelfUIProvider : UIProvider<Shelf>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Shelf";
	//public override Sprite Icon => currentTarget != null ? currentTarget.Icon : null;
	public override Sprite Icon => null; // Placeholder for shelf icon



	public float Capacity => currentTarget != null ? currentTarget.MaxSize : 0f;
	public float CurrentSize => currentTarget != null ? currentTarget.TotalSize : 0f;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Capacity", $"{Capacity} units"));
		infoBlocks.Add(new KeyValueBlock("Current Size", $"{CurrentSize} units"));
	}


}
