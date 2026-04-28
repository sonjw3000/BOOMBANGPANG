
using UnityEngine;
using static UnityEditor.Experimental.GraphView.Port;

public class CargoPortUIProvider : UIProvider<CargoPort>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown CargoPort";
	public override Sprite Icon => null; // Placeholder for shelf icon

	public float FilledPercent => currentTarget != null ? currentTarget.FilledPercent : 0.0f;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("", $"{FilledPercent}% / 100% Filled"));

	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock).UpdateValue($"{FilledPercent}% / 100% Filled");
	}
}
