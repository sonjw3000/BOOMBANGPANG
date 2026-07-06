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

		return AcceptPickingRequest(orderLine, quantity, out request) > 0;
	}

	public int AcceptPickingRequest(OrderLine orderLine, int quantity, out PickingRequest firstRequest)
	{
		firstRequest = null;
		if (RuntimeBuildingId == 0 || orderLine == null || quantity <= 0 || orderLine.CanAllocatePicking == false)
			return 0;

		OrderManager orderManager = GameContext.Instance.OrderMgr;
		if (orderManager == null)
			return 0;

		int remaining = UnityEngine.Mathf.Min(quantity, orderLine.GetPickingAllocatableQuantity());
		int accepted = 0;
		foreach (ShelfBase source in GameContext.Instance.StorageService.GetSources(RuntimeBuildingId, orderLine.ItemID))
		{
			if (source == null || remaining <= 0)
				continue;

			int reserved = source.ReservePicking(orderLine.ItemID, remaining);
			if (reserved <= 0)
				continue;

			int allocated = orderManager.AllocatePicking(orderLine, reserved);
			if (allocated <= 0)
			{
				source.ReleaseReservedPick(orderLine.ItemID, reserved);
				continue;
			}

			if (allocated < reserved)
			{
				source.ReleaseReservedPick(orderLine.ItemID, reserved - allocated);
				reserved = allocated;
			}

			if (pickingPlanner.AddReservedPickingRequest(orderLine, source, reserved, out PickingRequest request) == false)
			{
				source.ReleaseReservedPick(orderLine.ItemID, reserved);
				orderManager.ReleasePickingAllocation(orderLine, reserved);
				continue;
			}

			firstRequest ??= request;
			accepted += reserved;
			remaining -= reserved;
		}

		return accepted;
	}

	public int GetPickableQuantity(uint itemId)
	{
		if (RuntimeBuildingId == 0)
			return 0;

		int quantity = 0;
		foreach (ShelfBase source in GameContext.Instance.StorageService.GetSources(RuntimeBuildingId, itemId))
		{
			if (source != null)
				quantity += source.GetPickableQuantity(itemId);
		}

		return quantity;
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
