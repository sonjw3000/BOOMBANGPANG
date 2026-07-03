using System.Collections.Generic;

public sealed class PackingBuilding : Building
{
	private readonly PackingInputPlanner inputPlanner;
	private readonly PackingOutputPlanner outputPlanner;
	private readonly HashSet<PackingStation> dirtyOutputStations = new();

	private ItemTransferTaskScheduler Scheduler => GameContext.Instance.ItemTransferTaskScheduler;

	internal PackingInputPlanner InputPlanner => inputPlanner;
	internal PackingOutputPlanner OutputPlanner => outputPlanner;

	public PackingBuilding(string displayName, List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Packing)
	{
		trackingItemStatus.Add(ItemStatus.None);
		trackingItemStatus.Add(ItemStatus.Labeled);
		inputPlanner = new PackingInputPlanner(this);
		outputPlanner = new PackingOutputPlanner(this);
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
        // have to check items are fully packed first

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
			EvaluatePackingIngress(capsuleBuffer);
	}

	protected override void OnTrackedItemStatusAdded(uint itemId, ItemStatus status, IItemContainer container)
	{
		if (IsPackingInputStatus(status) && container is CapsuleBuffer capsuleBuffer)
			EvaluatePackingIngress(capsuleBuffer);
	}

	protected override void OnRegistered()
	{
		if (Scheduler == null)
			return;

		Scheduler.Register(
			RuntimeBuildingId,
			ItemTransferScheduleMode.PackingInput,
			WorkerTask.TaskType.Water,
			TryBuildItemTransferTask);

		Scheduler.Register(
			RuntimeBuildingId,
			ItemTransferScheduleMode.PackingOutput,
			WorkerTask.TaskType.Water,
			TryBuildItemTransferTask);

		RefreshPackingIngress();
	}

	protected override void OnUnregistered()
	{
		if (Scheduler == null)
			return;

		Scheduler.Unregister(RuntimeBuildingId, ItemTransferScheduleMode.PackingInput);
		Scheduler.Unregister(RuntimeBuildingId, ItemTransferScheduleMode.PackingOutput);
		dirtyOutputStations.Clear();
	}

	private void EvaluatePackingIngress(CapsuleBuffer capsuleBuffer)
	{
		if (Scheduler == null || capsuleBuffer == null)
			return;

		if (CanBuildWaterTaskRequest(capsuleBuffer) == false)
		{
			ClearItemContainerDirty(capsuleBuffer);
			if (dirtyItemStateContainers.Count <= 0)
				Scheduler.ClearDirty(RuntimeBuildingId, ItemTransferScheduleMode.PackingInput);
			return;
		}

		MarkItemContainerDirty(capsuleBuffer);
		Scheduler.MarkDirty(RuntimeBuildingId, ItemTransferScheduleMode.PackingInput);
	}

	private void RefreshPackingIngress()
	{
		for (int i = 0; i < OccupiedCapsuleBuffers.Count; ++i)
			EvaluatePackingIngress(OccupiedCapsuleBuffers[i]);
	}

	private ItemTransferScheduleResult TryBuildItemTransferTask(ItemTransferScheduleRequest request, out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		return request.Mode switch
		{
			ItemTransferScheduleMode.PackingInput => TryBuildPackingInputTask(request.Worker, out task),
			ItemTransferScheduleMode.PackingOutput => TryBuildPackingOutputTask(request.Worker, out task),
			_ => ItemTransferScheduleResult.NoWork,
		};
	}

	private ItemTransferScheduleResult TryBuildPackingInputTask(AIWorker worker, out WorkerTask task)
	{
		task = null;
		if (dirtyItemStateContainers.Count <= 0)
			return ItemTransferScheduleResult.NoWork;

		task = new ItemTransferTask(
			WorkerTask.TaskType.Water,
			new ItemTransferJob(
				inputPlanner,
				TransferObjectType.Item,
				TransferObjectType.Box,
				RuntimeBuildingId,
				worker));
		return ItemTransferScheduleResult.Scheduled;
	}

	private ItemTransferScheduleResult TryBuildPackingOutputTask(AIWorker worker, out WorkerTask task)
	{
		task = null;
		if (dirtyOutputStations.Count <= 0)
			return ItemTransferScheduleResult.NoWork;

		task = new ItemTransferTask(
			WorkerTask.TaskType.Water,
			new ItemTransferJob(
				outputPlanner,
				TransferObjectType.Box,
				TransferObjectType.Item,
				RuntimeBuildingId,
				worker));
		return ItemTransferScheduleResult.Scheduled;
	}

	internal bool CanBuildWaterTaskRequest(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null &&
			capsuleBuffer.CanProvideInboundItems() &&
			HasAvailablePackingInput(capsuleBuffer) &&
			GameContext.HasInstance &&
			GameContext.Instance.OBWorkflowSvc != null &&
			GameContext.Instance.OBWorkflowSvc.HasPackableManifest(capsuleBuffer.DockedCapsule);
	}

	internal bool CanBuildWaterTaskRequest(PackingStation packingStation)
	{
		return packingStation != null && packingStation.EndPackingBox != null;
	}

	internal bool HasAvailablePackingInput(IItemContainer container)
	{
		return HasAvailableItemStatus(container, ItemStatus.Labeled) ||
			HasAvailableItemStatus(container, ItemStatus.None);
	}

	internal bool TryFindAvailablePackingInput(
		IItemContainer container,
		uint itemId,
		out ItemStatus status,
		out int quantity)
	{
		if (TryFindAvailablePackingInput(container, itemId, ItemStatus.Labeled, out quantity))
		{
			status = ItemStatus.Labeled;
			return true;
		}

		if (TryFindAvailablePackingInput(container, itemId, ItemStatus.None, out quantity))
		{
			status = ItemStatus.None;
			return true;
		}

		status = ItemStatus.None;
		quantity = 0;
		return false;
	}

	private bool TryFindAvailablePackingInput(IItemContainer container, uint itemId, ItemStatus status, out int quantity)
	{
		quantity = GetAvailableItemQuantity(container, itemId, status);
		return quantity > 0;
	}

	private static bool IsPackingInputStatus(ItemStatus status)
	{
		return status == ItemStatus.None || status == ItemStatus.Labeled;
	}

	internal bool TryTakeDirtyPackingOutputStation(out PackingStation station)
	{
		foreach (PackingStation candidate in dirtyOutputStations)
		{
			station = candidate;
			dirtyOutputStations.Remove(candidate);
			return true;
		}

		station = null;
		return false;
	}

	internal void MarkPackingOutputDirty(PackingStation station)
	{
		if (station == null)
			return;

		dirtyOutputStations.Add(station);
		Scheduler?.MarkDirty(RuntimeBuildingId, ItemTransferScheduleMode.PackingOutput);
	}

	internal void ClearPackingOutputDirty(PackingStation station)
	{
		if (station != null)
			dirtyOutputStations.Remove(station);

		if (dirtyOutputStations.Count <= 0)
			Scheduler?.ClearDirty(RuntimeBuildingId, ItemTransferScheduleMode.PackingOutput);
	}

}
