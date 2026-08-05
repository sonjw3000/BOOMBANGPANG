using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class NavigationHubUIProvider : UIProvider<NavigationHub>, ISelectionInspectorProvider
{
	private RobotNavigationService Service => GameContext.HasInstance ? GameContext.Instance.RobotNavigationSvc : null;
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Navigation Hub";
	public override string Subtitle => "Navigation Hub";
	public override Sprite Icon => null;

	private string PowerDisplay => currentTarget == null
		? "Unavailable"
		: currentTarget.HasPower ? $"Powered · {currentTarget.PowerConsumption}" : $"Offline · needs {currentTarget.PowerConsumption}";
	private string ComputeDisplay => currentTarget != null
		? $"{currentTarget.AssignedCompute} assigned + {currentTarget.ReservedCompute} reserved / {currentTarget.ComputeCapacity}"
		: "0/0";
	private string RelayDisplay => currentTarget != null ? $"{currentTarget.ActiveRelayCount}/{currentTarget.RelayCapacity}" : "0/0";
	private string RobotDisplay => currentTarget != null && Service != null
		? Service.GetAllocatedRobotCount(currentTarget.RuntimeHubId).ToString()
		: "0";
	private string CoverageDisplay => currentTarget != null && Service != null
		? Service.GetCoverageCellCount(currentTarget.RuntimeHubId).ToString()
		: "0";
	private string StatusDisplay => currentTarget == null
		? "Unavailable"
		: currentTarget.IsComputeOverloaded ? "OVERLOADED" : currentTarget.IsOperational ? "Online" : "Offline";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Status", StatusDisplay));
		infoBlocks.Add(new KeyValueBlock("Compute", ComputeDisplay));
		infoBlocks.Add(new KeyValueBlock("Relays", RelayDisplay));
		infoBlocks.Add(new KeyValueBlock("Coverage", CoverageDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 4)
			return;
		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StatusDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(ComputeDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(RelayDisplay);
		(infoBlocks[3] as KeyValueBlock)?.UpdateValue(CoverageDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Status", () => StatusDisplay);
		model.AddOverview("Power", () => PowerDisplay);
		model.AddOverview("Compute", () => ComputeDisplay);
		model.AddOverview("Relays", () => RelayDisplay);
		model.AddOverview("Robots", () => RobotDisplay);
		model.AddOverview("Coverage Cells", () => CoverageDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}
}
