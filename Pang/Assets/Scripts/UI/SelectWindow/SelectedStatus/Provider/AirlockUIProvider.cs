using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class AirlockUIProvider : UIProvider<Airlock>, ISelectionInspectorProvider
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
	public string PositionDisplay => currentTarget != null ? currentTarget.GridPosition.ToString() : "(0,0,0)";
	public bool HasReservation => currentTarget != null && currentTarget.ReservedWorker != null;

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

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("State", () => StateDisplay);
		model.AddOverview("Reserved Worker", () => ReservedWorkerDisplay);
		model.AddOverview("Direction", () => DirectionDisplay);
		model.AddOverview("Entry Delay", () => DelayDisplay);
		model.AddOverview("Grid Position", () => PositionDisplay);
		model.AddAction("Release Reservation", ReleaseReservation, () => HasReservation);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}

	private void ReleaseReservation()
	{
		if (currentTarget == null || GameContext.HasInstance == false)
			return;

		GameContext.Instance.AirlockSvc.Release(currentTarget, null);
	}
}
