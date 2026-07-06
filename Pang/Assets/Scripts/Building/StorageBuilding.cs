public sealed class StorageBuilding : Building
{
	private PickingPlanner pickingPlanner;

	public PickingPlanner PickingPlanner => pickingPlanner;

	public StorageBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Storage)
	{
		trackingItemStatus.Add(ItemStatus.Labeled);

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		pickingPlanner = new PickingPlanner(
			this,
			outbound != null ? outbound.PickingBoxFillLimitPercent : 80.0f,
			outbound != null ? outbound.PickingCollectingPolicyType : CollectingPolicyType.Nearest);
	}

	public bool TryAcceptPickingRequest(OrderLine orderLine, int quantity, out PickingRequest request)
	{
		request = null;
		if (RuntimeBuildingId == 0 || orderLine == null || quantity <= 0)
			return false;

		return pickingPlanner.TryAcceptPickingRequest(orderLine, quantity, out request);
	}

	public bool HasPendingPickingRequest()
	{
		return pickingPlanner.HasPendingCollect(RuntimeBuildingId);
	}

	public bool TryBuildPickingItemTransferTask(AIWorker worker, out ItemTransferTask task)
	{
		task = null;
		return pickingPlanner.BuildItemTransferTask(worker, out task);
	}

	protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null ||
			capsuleBuffer.DockState != CapsuleDockState.OBStandby ||
			capsuleBuffer.DockedCapsule == null ||
			(capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OBStandby &&
			 capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OB))
		{
			return false;
		}

        // todo
        // have to check items are fully labeled first

		float workflowThreshold = GameContext.HasInstance && GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.CargoPortThresholdPercent
			: CapsuleThresholdPercent;
		float threshold = OverrideCapsuleThreshold ? CapsuleThresholdPercent : workflowThreshold;
		return capsuleBuffer.FilledPercent >= threshold;
	}
}
