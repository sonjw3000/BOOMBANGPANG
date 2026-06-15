using UnityEngine;

public sealed class AirlockUIProvider : UIProvider<Airlock>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Airlock";
	public override string Subtitle => "Airlock";
	public override Sprite Icon => null;

	public string StateDisplay => currentTarget != null ? currentTarget.State.ToString() : "Unknown";
	public string ReservedWorkerDisplay => currentTarget != null && currentTarget.ReservedWorker != null
		? currentTarget.ReservedWorker.name
		: "None";
	public string DirectionDisplay => currentTarget != null ? currentTarget.ReservedDirection.ToString() : "-";
	public string DelayDisplay => currentTarget != null ? $"{currentTarget.EntryDelaySeconds:0.0}s" : "0.0s";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("State", StateDisplay));
		infoBlocks.Add(new KeyValueBlock("Reserved Worker", ReservedWorkerDisplay));
		infoBlocks.Add(new KeyValueBlock("Entry Delay", DelayDisplay));
	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StateDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(ReservedWorkerDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(DelayDisplay);
	}
}
