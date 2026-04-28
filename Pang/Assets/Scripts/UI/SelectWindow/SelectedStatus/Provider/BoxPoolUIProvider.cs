using UnityEngine;

public class BoxPoolUIProvider : UIProvider<BoxPool>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Shelf";
	public override Sprite Icon => null; // Placeholder for shelf icon

	public int CurrentBoxCount => currentTarget != null ? currentTarget.CurrentBoxCount : 0;

	//public 
	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Current Size", $"{CurrentBoxCount} units"));
	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock).UpdateValue($"{CurrentBoxCount} units");
	}

}
