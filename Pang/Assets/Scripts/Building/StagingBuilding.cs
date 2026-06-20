using System.Collections.Generic;

public sealed class StagingBuilding : Building
{
	private readonly HashSet<InboundCargoPort> pendingInboundPorts = new();
	private readonly HashSet<InboundCargoPort> queuedInboundPorts = new();

	public IReadOnlyCollection<InboundCargoPort> PendingInboundPorts => pendingInboundPorts;
	private TaskManager TaskManager => GameContext.Instance.TaskMgr;
	private BoxPoolService CapsuleStorageService => GameContext.Instance.WMSys.BoxPoolService;

	public StagingBuilding(string displayName, List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Staging)
	{
	}

	protected override void OnInboundCargoDocked(InboundCargoPort cargoPort)
	{
		if (cargoPort != null)
		{
			pendingInboundPorts.Add(cargoPort);
			TryEnqueueInboundTask(cargoPort);
		}
	}

	protected override void OnInboundCargoUndocked(InboundCargoPort cargoPort)
	{
		if (cargoPort != null)
		{
			pendingInboundPorts.Remove(cargoPort);
			queuedInboundPorts.Remove(cargoPort);
		}
	}

	protected override void OnInboundCargoQuantityZero(InboundCargoPort cargoPort)
	{
		if (cargoPort != null)
		{
			pendingInboundPorts.Remove(cargoPort);
			queuedInboundPorts.Remove(cargoPort);
		}
	}

	private void TryEnqueueInboundTask(InboundCargoPort cargoPort)
	{
		if (cargoPort == null || queuedInboundPorts.Contains(cargoPort))
			return;

		if (ResolveCapsuleStorage(cargoPort) == null)
			return;

		TaskManager.EnqueueTask(new StagingIBTask(cargoPort, RuntimeBuildingId));
		queuedInboundPorts.Add(cargoPort);
	}

	private BoxPool ResolveCapsuleStorage(InboundCargoPort cargoPort)
	{
		return cargoPort == null || CapsuleStorageService == null
			? null
			: CapsuleStorageService.GetClosestAvailableTarget(RuntimeBuildingId, cargoPort.GridPosition, InteractionKind.Put);
	}
}
