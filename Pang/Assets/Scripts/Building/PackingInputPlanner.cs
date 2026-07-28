using System.Collections.Generic;
using UnityEngine;

public sealed class PackingInputPlanner : IItemTransferPlanner, IItemTransferTaskInvalidationHandler
{
	private readonly PackingBuilding building;

	public PackingInputPlanner(PackingBuilding building)
	{
		this.building = building;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		BoxBase workerBox = worker?.CarryingAbility?.CarryingBox;
		if (building == null || workerBox == null)
			return WorkPlanResult.Waiting;

		if (HasReachedBoxFillLimit(workerBox))
			return WorkPlanResult.SwitchPhase;

		List<IItemContainer> deferredContainers = null;
		while (building.TryTakeDirtyItemContainer(out IItemContainer container))
		{
			if (TryBuildCollectLine(worker, container, out line))
			{
				RestoreDeferredContainers(deferredContainers);
				return WorkPlanResult.Issued;
			}

			if (container is CapsuleBuffer buffer && building.CanBuildPackingInputTask(buffer))
			{
				deferredContainers ??= new List<IItemContainer>();
				deferredContainers.Add(container);
			}
		}

		RestoreDeferredContainers(deferredContainers);
		return workerBox.Stacks.Count > 0
			? WorkPlanResult.SwitchPhase
			: WorkPlanResult.Completed;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		ReleaseUnmovedReservation(line, result);

		if (line?.Container != null)
		{
			if (building.HasAvailablePackingInput(line.Container))
				building.MarkItemContainerDirty(line.Container);
			else
				building.ClearItemContainerDirty(line.Container);
		}

		if (result.Moved <= 0)
			return worker?.CarryingAbility?.CarryingBox?.Stacks.Count > 0
				? WorkPlanResult.SwitchPhase
				: WorkPlanResult.Completed;

		BoxBase workerBox = worker?.CarryingAbility?.CarryingBox;
		TransferPickingManifest(line.Container, workerBox, line.RelatedOrderLine, line.ItemID, result.Moved);
		return HasReachedBoxFillLimit(workerBox)
			? WorkPlanResult.SwitchPhase
			: WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine collectedLine, int remainingQuantity, out WorkLine line)
	{
		line = null;
		PackingStationService stationService = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc?.PackingStationService : null;
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		FacilityFilter filter = FacilityFilter.ForTransfer(
			box,
			collectedLine != null ? collectedLine.ItemID : 0,
			remainingQuantity,
			stack => collectedLine == null ||
				collectedLine.RequiredStatus.HasValue == false ||
				stack.HasStatus(collectedLine.RequiredStatus.Value),
			worker);
		if (stationService == null || stationService.TryClaimWaitingStation(buildingId, filter, out PackingStation station) == false)
			return WorkPlanResult.Waiting;

		line = new WorkLine(WorkLineAction.Put, station, station, collectedLine != null ? collectedLine.ItemID : 0, 1);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result)
	{
		if (result.Moved > 0)
			return WorkPlanResult.Completed;

		if (placeLine?.Target is PackingStation station)
			station.SetIncomingRequestSuspended(false);

		return WorkPlanResult.Waiting;
	}

	public void OnTaskInvalidated(ItemTransferTask task)
	{
		if (task == null)
			return;

		WorkLine line = task.CurrentLine;
		if (task.Phase == ItemTransferPhase.Collect && line?.Container is IItemPickReservable reservable)
		{
			int remaining = Mathf.Max(0, line.Quantity - line.CompleteQuantity);
			if (remaining > 0)
				reservable.ReleaseReservedPick(line.ItemID, remaining);

			FacilityManager facilityManager = GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;
			if (line.Container is IFacility facility && facilityManager?.IsInvalidating(facility) == true)
				building?.ClearItemContainerDirty(line.Container);
			else if (line.Container != null)
				building?.MarkItemContainerDirty(line.Container);
		}
		else if (task.Phase == ItemTransferPhase.Place && line?.Target is PackingStation station)
		{
			station.SetIncomingRequestSuspended(false);
		}
	}

	private bool TryBuildCollectLine(AIWorker worker, IItemContainer container, out WorkLine line)
	{
		line = null;
		if (container == null || container is not IGridPlaceable target || container is not IItemPickReservable reservable)
			return false;

		BoxBase sourceBox = ResolveManifestBox(container);
		BoxBase workerBox = worker.CarryingAbility.CarryingBox;
		if (sourceBox == null || workerBox == null || GameContext.HasInstance == false)
			return false;

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		if (outbound == null || outbound.TryGetPickingManifest(sourceBox, out PickingManifest manifest) == false)
			return false;

		IReadOnlyList<PickingManifestLine> manifestLines = manifest.Lines;
		for (int i = 0; i < manifestLines.Count; ++i)
		{
			PickingManifestLine manifestLine = manifestLines[i];
			if (manifestLine == null || manifestLine.PackableQuantity <= 0)
				continue;

			if (building.TryFindAvailablePackingInput(container, manifestLine.ItemId, out ItemStatus sourceStatus, out int available) == false)
				continue;

			int acceptable = GetAcceptableQuantityWithinFillLimit(
				workerBox,
				manifestLine.ItemId,
				manifestLine.PackableQuantity);
			int quantity = Mathf.Min(available, manifestLine.PackableQuantity, acceptable);
			if (quantity <= 0)
				continue;

			int reserved = reservable.ReservePicking(manifestLine.ItemId, quantity);
			if (reserved <= 0)
				continue;

			line = new WorkLine(
				WorkLineAction.Pick,
				container,
				target,
				manifestLine.ItemId,
				reserved,
				manifestLine.OrderLine,
				sourceStatus);

			if (building.HasAvailablePackingInput(container))
				building.MarkItemContainerDirty(container);
			else
				building.ClearItemContainerDirty(container);

			return true;
		}

		return false;
	}

	private void RestoreDeferredContainers(List<IItemContainer> containers)
	{
		if (containers == null)
			return;

		for (int i = 0; i < containers.Count; ++i)
			building.MarkItemContainerDirty(containers[i]);
	}

	private static bool HasReachedBoxFillLimit(BoxBase box)
	{
		if (box == null || box.MaxSize <= 0.0f)
			return false;

		float filledPercent = (box.TotalSize / box.MaxSize) * 100.0f;
		return filledPercent >= GetBoxFillLimitPercent();
	}

	private static int GetAcceptableQuantityWithinFillLimit(BoxBase box, uint itemId, int requested)
	{
		if (box == null || requested <= 0)
			return 0;

		int acceptable = box.GetAcceptableQuantity(itemId, requested);
		if (acceptable <= 0 || box.MaxSize <= 0.0f)
			return acceptable;

		ItemDatabase itemDatabase = GameContext.HasInstance ? GameContext.Instance.ItemDB : null;
		float itemSize = itemDatabase != null ? itemDatabase.GetItemSize(itemId) : 0.0f;
		if (itemSize <= 0.0f)
			return 0;

		float fillLimit = box.MaxSize * GetBoxFillLimitPercent() / 100.0f;
		float remainingSize = fillLimit - box.TotalSize;
		if (remainingSize <= 0.0f)
			return 0;

		int fillLimitAcceptable = Mathf.FloorToInt((remainingSize / itemSize) + 0.0001f);
		return Mathf.Min(acceptable, fillLimitAcceptable);
	}

	private static float GetBoxFillLimitPercent()
	{
		OutboundWorkflowService outbound = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		return Mathf.Clamp(outbound != null ? outbound.PickingBoxFillLimitPercent : 80.0f, 1.0f, 100.0f);
	}

	private static void ReleaseUnmovedReservation(WorkLine line, ItemTransferResult result)
	{
		if (line?.Container is not IItemPickReservable reservable)
			return;

		int remainingReservation = Mathf.Max(0, line.Quantity - result.Moved);
		if (remainingReservation > 0)
			reservable.ReleaseReservedPick(line.ItemID, remainingReservation);
	}

	private static void TransferPickingManifest(
		IItemContainer sourceContainer,
		BoxBase targetBox,
		OrderLine orderLine,
		uint itemId,
		int quantity)
	{
		if (quantity <= 0 || GameContext.HasInstance == false)
			return;

		BoxBase sourceBox = ResolveManifestBox(sourceContainer);
		if (sourceBox == null ||
			targetBox == null ||
			PickingManifestKey.From(sourceBox) == PickingManifestKey.From(targetBox))
			return;

		GameContext.Instance.OBWorkflowSvc?.TransferPickingManifest(
			sourceBox,
			targetBox,
			orderLine,
			itemId,
			quantity,
			false);
	}

	private static BoxBase ResolveManifestBox(IItemContainer container)
	{
		return container switch
		{
			BoxBase box => box,
			CapsuleBuffer buffer => buffer.DockedCapsule,
			_ => null,
		};
	}
}
