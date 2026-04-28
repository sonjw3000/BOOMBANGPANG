using UnityEngine;

public sealed class ShelfUIProvider : UIProvider<Shelf>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Shelf";
	public override Sprite Icon => null;

	public float Capacity => currentTarget != null ? currentTarget.MaxSize : 0f;
	public float CurrentSize => currentTarget != null ? currentTarget.TotalSize : 0f;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Capacity", $"{Capacity} units"));
		infoBlocks.Add(new KeyValueBlock("Current Size", $"{CurrentSize} units"));
	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock).UpdateValue($"{Capacity} units");
		(infoBlocks[1] as KeyValueBlock).UpdateValue($"{CurrentSize} units");
	}

}
