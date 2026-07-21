public sealed class StorageBuilding : Building
{
	private PickingPlanner pickingPlanner;

	private ItemTransferTaskScheduler Scheduler => GameContext.Instance.ItemTransferTaskScheduler;

	public PickingPlanner PickingPlanner => pickingPlanner;

	public StorageBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Storage)
	{
		trackingItemStatus.Add(ItemStatus.Labeled);

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		pickingPlanner = new PickingPlanner(
			this,
			outbound != null ? outbound.PickingBoxFillLimitPercent : 80.0f,
			outbound != null ? outbound.PickingCollectingPolicyType : CollectingPolicyType.Nearest,
			outbound != null ? outbound.PickingPolicyType : PickingPolicyType.ManualShelfScan);
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

		int accepted = pickingPlanner.AcceptPickingRequest(orderLine, quantity, out firstRequest);

		if (accepted > 0)
			Scheduler?.MarkDirty(RuntimeBuildingId, ItemTransferScheduleMode.Picking);

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

	public bool HasPendingStoringRequest()
	{
		StoringPlanner storingPlanner = GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc?.StoringPlanner : null;
		return storingPlanner != null && storingPlanner.HasPendingCollectWork(RuntimeBuildingId);
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

	protected override void OnIBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		base.OnIBDockDocked(dock, capsule);

		if (dock is CapsuleBuffer capsuleBuffer)
			EvaluateStoringIngress(capsuleBuffer);
	}

	protected override void OnRegistered()
	{
		Scheduler?.Register(
			RuntimeBuildingId,
			ItemTransferScheduleMode.Picking,
			WorkerTask.TaskType.Picking,
			TryBuildPickingItemTransferTask);

		Scheduler?.Register(
			RuntimeBuildingId,
			ItemTransferScheduleMode.Storing,
			WorkerTask.TaskType.Storing,
			TryBuildStoringItemTransferTask);

		RefreshStoringIngress();
	}

	protected override void OnUnregistered()
	{
		Scheduler?.Unregister(RuntimeBuildingId, ItemTransferScheduleMode.Picking);
		Scheduler?.Unregister(RuntimeBuildingId, ItemTransferScheduleMode.Storing);
	}

	private ItemTransferScheduleResult TryBuildPickingItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (HasPendingPickingRequest() == false ||
			pickingPlanner.BuildItemTransferTask(request.Worker, out ItemTransferTask itemTransferTask) == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		task = itemTransferTask;
		return ItemTransferScheduleResult.Scheduled;
	}

	private ItemTransferScheduleResult TryBuildStoringItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		StoringPlanner storingPlanner = GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc?.StoringPlanner : null;
		if (storingPlanner == null ||
			HasPendingStoringRequest() == false ||
			storingPlanner.BuildItemTransferTask(request.Worker, RuntimeBuildingId, out ItemTransferTask itemTransferTask) == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		task = itemTransferTask;
		return ItemTransferScheduleResult.Scheduled;
	}

	private void EvaluateStoringIngress(CapsuleBuffer capsuleBuffer)
	{
		if (Scheduler == null || capsuleBuffer == null)
			return;

		if (CanBuildStoringTask(capsuleBuffer))
		{
			Scheduler.MarkDirty(RuntimeBuildingId, ItemTransferScheduleMode.Storing);
			return;
		}

		if (HasPendingStoringRequest() == false)
			Scheduler.ClearDirty(RuntimeBuildingId, ItemTransferScheduleMode.Storing);
	}

	private void RefreshStoringIngress()
	{
		for (int i = 0; i < OccupiedCapsuleBuffers.Count; ++i)
			EvaluateStoringIngress(OccupiedCapsuleBuffers[i]);
	}

	private static bool CanBuildStoringTask(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null &&
			capsuleBuffer.CanProvideInboundItems() &&
			capsuleBuffer.ItemTotals.Count > 0;
	}
}
