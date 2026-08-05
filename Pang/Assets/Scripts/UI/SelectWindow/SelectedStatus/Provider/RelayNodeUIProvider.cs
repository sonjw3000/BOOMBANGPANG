using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class RelayNodeUIProvider : UIProvider<RelayNode>, ISelectionInspectorProvider
{
	private RobotNavigationService Service => GameContext.HasInstance ? GameContext.Instance.RobotNavigationSvc : null;
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Relay Node";
	public override string Subtitle => "Relay Node";
	public override Sprite Icon => null;

	private string OwnerDisplay => currentTarget != null && currentTarget.OwnerHubId != 0 ? $"Hub #{currentTarget.OwnerHubId}" : "None";
	private string StatusDisplay
	{
		get
		{
			if (currentTarget == null)
				return "Unavailable";
			if (currentTarget.IsConnected)
				return "Connected";
			string reason = Service?.GetRelayOfflineReason(currentTarget);
			return string.IsNullOrWhiteSpace(reason) ? "Offline" : $"Offline · {reason}";
		}
	}
	private string RadiusDisplay => currentTarget != null ? currentTarget.CoverageRadius.ToString() : "0";
	private string HealthDisplay => currentTarget != null ? $"{currentTarget.Health:0.0}/{currentTarget.MaxHealth:0.0}" : "0/0";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Status", StatusDisplay));
		infoBlocks.Add(new KeyValueBlock("Owner", OwnerDisplay));
		infoBlocks.Add(new KeyValueBlock("Radius", RadiusDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;
		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StatusDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(OwnerDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(RadiusDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Status", () => StatusDisplay);
		model.AddOverview("Owner", () => OwnerDisplay);
		model.AddOverview("Coverage Radius", () => RadiusDisplay);
		model.AddOverview("Health", () => HealthDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}
}
