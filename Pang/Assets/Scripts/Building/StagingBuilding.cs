using System.Collections.Generic;

public sealed class StagingBuilding : Building
{
	private readonly HashSet<InboundCargoPort> pendingInboundPorts = new();

	public IReadOnlyCollection<InboundCargoPort> PendingInboundPorts => pendingInboundPorts;

	public StagingBuilding(string displayName, List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Staging)
	{
	}

	protected override void OnInboundCargoDocked(InboundCargoPort cargoPort)
	{
		if (cargoPort != null)
			pendingInboundPorts.Add(cargoPort);
	}

	protected override void OnInboundCargoUndocked(InboundCargoPort cargoPort)
	{
		if (cargoPort != null)
			pendingInboundPorts.Remove(cargoPort);
	}

	protected override void OnInboundCargoQuantityZero(InboundCargoPort cargoPort)
	{
		if (cargoPort != null)
			pendingInboundPorts.Remove(cargoPort);
	}
}
