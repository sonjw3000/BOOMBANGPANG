using System.Collections.Generic;
using UnityEngine;

public sealed class PackingInputPlanner : IItemTransferPlanner
{
	private readonly PackingBuilding building;

	public PackingInputPlanner(PackingBuilding building)
	{
		this.building = building;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		if (building == null || worker?.CarryingAbility?.CarryingBox == null)
			return WorkPlanResult.Waiting;

		while (building.TryTakeDirtyItemContainer(out IItemContainer container))
		{
			if (TryBuildCollectLine(worker, container, out line))
				return WorkPlanResult.Issued;
		}

		return WorkPlanResult.Completed;
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
			return WorkPlanResult.Completed;

		TransferPickingManifest(line.Container, worker?.CarryingAbility?.CarryingBox, line.ItemID, result.Moved);
		return WorkPlanResult.SwitchPhase;
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
		return result.Moved > 0 ? WorkPlanResult.Completed : WorkPlanResult.Waiting;
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

			int acceptable = workerBox.GetAcceptableQuantity(manifestLine.ItemId, manifestLine.PackableQuantity);
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

	private static void ReleaseUnmovedReservation(WorkLine line, ItemTransferResult result)
	{
		if (line?.Container is not IItemPickReservable reservable)
			return;

		int remainingReservation = Mathf.Max(0, line.Quantity - result.Moved);
		if (remainingReservation > 0)
			reservable.ReleaseReservedPick(line.ItemID, remainingReservation);
	}

	private static void TransferPickingManifest(IItemContainer sourceContainer, BoxBase targetBox, uint itemId, int quantity)
	{
		if (quantity <= 0 || GameContext.HasInstance == false)
			return;

		BoxBase sourceBox = ResolveManifestBox(sourceContainer);
		if (sourceBox == null || targetBox == null || sourceBox.BoxId == targetBox.BoxId)
			return;

		GameContext.Instance.OBWorkflowSvc?.TransferPickingManifest(sourceBox, targetBox, itemId, quantity, false);
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
