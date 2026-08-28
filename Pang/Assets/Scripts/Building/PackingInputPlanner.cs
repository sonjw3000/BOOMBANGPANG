using System.Collections.Generic;
using UnityEngine;

public sealed class PackingInputPlanner : IItemTransferPlanner, IItemTransferTaskInvalidationHandler
{
	private readonly uint buildingId;

	public uint BuildingId => buildingId;

	private CapsuleBufferService BufferService =>
		GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;

	public PackingInputPlanner(uint buildingId)
	{
		this.buildingId = buildingId;
	}

	public bool HasAvailableWork(AIWorker worker = null)
	{
		if (buildingId == 0 || BufferService == null)
			return false;

		foreach (CapsuleBuffer buffer in BufferService.GetBuffers(buildingId))
		{
			if (CanUseSource(buffer) == false ||
				(worker != null && InteractionPointSelector.TryGetInteractionPointInBuilding(
					buffer,
					InteractionKind.Pick,
					worker.GridPosition,
					buildingId,
					out _,
					out _) == false))
			{
				continue;
			}

			if (TryFindAvailableManifestLine(buffer, worker?.CarryingAbility?.CarryingBox, out _, out _, out _))
				return true;
		}

		return false;
	}

	public void GetPendingDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;
		if (buildingId == 0 || BufferService == null)
			return;

		foreach (CapsuleBuffer buffer in BufferService.GetBuffers(buildingId))
		{
			if (CanUseSource(buffer) == false ||
				TryGetManifest(buffer, out PickingManifest manifest) == false)
			{
				continue;
			}

			int sourceQuantity = 0;
			for (int i = 0; i < manifest.Lines.Count; ++i)
			{
				PickingManifestLine manifestLine = manifest.Lines[i];
				if (manifestLine == null || manifestLine.PackableQuantity <= 0)
					continue;

				int available = GetAvailableLabeledQuantity(buffer, manifestLine.ItemId);
				sourceQuantity += Mathf.Min(available, manifestLine.PackableQuantity);
			}

			if (sourceQuantity <= 0)
				continue;

			++sourceCount;
			itemQuantity += sourceQuantity;
		}
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint requestedBuildingId, out WorkLine line)
	{
		line = null;
		BoxBase workerBox = worker?.CarryingAbility?.CarryingBox;
		if (worker == null || workerBox == null || requestedBuildingId != buildingId || BufferService == null)
			return WorkPlanResult.Waiting;

		if (HasReachedBoxFillLimit(workerBox))
			return WorkPlanResult.SwitchPhase;

		CapsuleBuffer bestBuffer = null;
		PickingManifestLine bestManifestLine = null;
		int bestQuantity = 0;
		int bestDistance = int.MaxValue;
		foreach (CapsuleBuffer buffer in BufferService.GetBuffers(buildingId))
		{
			if (CanUseSource(buffer) == false ||
				InteractionPointSelector.TryGetInteractionPointInBuilding(
					buffer,
					InteractionKind.Pick,
					worker.GridPosition,
					buildingId,
					out _,
					out int distance) == false ||
				distance >= bestDistance ||
				TryFindAvailableManifestLine(buffer, workerBox, out PickingManifestLine manifestLine, out int quantity, out _) == false)
			{
				continue;
			}

			bestBuffer = buffer;
			bestManifestLine = manifestLine;
			bestQuantity = quantity;
			bestDistance = distance;
		}

		if (bestBuffer == null || bestManifestLine == null || bestQuantity <= 0)
			return workerBox.Stacks.Count > 0
				? WorkPlanResult.SwitchPhase
				: WorkPlanResult.Completed;

		int reserved = bestBuffer.ReservePicking(bestManifestLine.ItemId, bestQuantity);
		if (reserved <= 0)
			return WorkPlanResult.Waiting;

		line = new WorkLine(
			WorkLineAction.Pick,
			bestBuffer,
			bestBuffer,
			bestManifestLine.ItemId,
			reserved,
			bestManifestLine.OrderLine,
			ItemStatus.Labeled,
			excludedQuality: ItemQuality.Waste);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		ReleaseUnmovedReservation(line, result);

		if (result.Moved > 0)
		{
			TransferPickingManifest(
				line?.Container,
				worker?.CarryingAbility?.CarryingBox,
				line?.RelatedOrderLine,
				line != null ? line.ItemID : 0,
				result.Moved);
		}

		EvaluateWork();
		if (result.Moved <= 0)
		{
			return worker?.CarryingAbility?.CarryingBox?.Stacks.Count > 0
				? WorkPlanResult.SwitchPhase
				: WorkPlanResult.Completed;
		}

		return HasReachedBoxFillLimit(worker?.CarryingAbility?.CarryingBox)
			? WorkPlanResult.SwitchPhase
			: WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetPlaceLine(
		AIWorker worker,
		uint requestedBuildingId,
		WorkLine collectedLine,
		int remainingQuantity,
		out WorkLine line)
	{
		line = null;
		PackingStationService stationService = GameContext.HasInstance
			? GameContext.Instance.OBWorkflowSvc?.PackingStationService
			: null;
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		FacilityFilter filter = FacilityFilter.ForTransfer(
			box,
			collectedLine != null ? collectedLine.ItemID : 0,
			remainingQuantity,
			stack => collectedLine == null ||
				collectedLine.RequiredStatus.HasValue == false ||
				stack.HasStatus(collectedLine.RequiredStatus.Value),
			worker);
		if (stationService == null ||
			requestedBuildingId != buildingId ||
			stationService.TryClaimWaitingStation(buildingId, filter, out PackingStation station) == false)
		{
			return WorkPlanResult.Waiting;
		}

		line = new WorkLine(
			WorkLineAction.Put,
			station,
			station,
			collectedLine != null ? collectedLine.ItemID : 0,
			1);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(
		AIWorker worker,
		WorkLine collectedLine,
		WorkLine placeLine,
		ItemTransferResult result)
	{
		EvaluateWork();
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
		}
		else if (task.Phase == ItemTransferPhase.Place && line?.Target is PackingStation station)
		{
			station.SetIncomingRequestSuspended(false);
		}

		EvaluateWork();
	}

	private bool CanUseSource(CapsuleBuffer buffer)
	{
		if (buffer?.DockedCapsule is not CargoCapsule capsule ||
			GameContext.HasInstance == false ||
			capsule.RouteKind != CargoRouteKind.Standard ||
			capsule.LogisticsState != CapsuleLogisticsState.Inside ||
			buffer.CanProvideInboundItems() == false ||
			BufferService?.IsExplicitRuleMatchedBuffer(
				buffer,
				capsule,
				ItemProcessStage.Picked,
				evaluateLaunchReadiness: false) != true ||
			TryGetManifest(buffer, out PickingManifest manifest) == false ||
			manifest.IsEmpty)
		{
			return false;
		}

		FacilityManager facilityManager = GameContext.Instance.FacilityMgr;
		if (facilityManager?.IsInvalidating(buffer) == true ||
			GameContext.Instance.TaskMgr?.HasConflictingCapsuleContentDependency(
				buffer,
				WorkLineAction.Pick) == true)
		{
			return false;
		}

		CapsuleRelocateCoordinator coordinator = GameContext.Instance.ExistingCapsuleRelocateCoordinator;
		return coordinator == null ||
			(coordinator.IsPlayerClaimed(buffer) == false &&
			 coordinator.IsReserved(buffer) == false &&
			 coordinator.IsRelocationSourceActive(buffer) == false &&
			 coordinator.IsRelocationTargetActive(buffer) == false);
	}

	private static bool TryGetManifest(CapsuleBuffer buffer, out PickingManifest manifest)
	{
		manifest = null;
		return buffer?.DockedCapsule != null &&
			GameContext.HasInstance &&
			GameContext.Instance.OBWorkflowSvc?.TryGetPickingManifest(buffer.DockedCapsule, out manifest) == true;
	}

	private static bool TryFindAvailableManifestLine(
		CapsuleBuffer buffer,
		BoxBase targetBox,
		out PickingManifestLine manifestLine,
		out int quantity,
		out int available)
	{
		manifestLine = null;
		quantity = 0;
		available = 0;
		if (TryGetManifest(buffer, out PickingManifest manifest) == false)
			return false;

		for (int i = 0; i < manifest.Lines.Count; ++i)
		{
			PickingManifestLine candidate = manifest.Lines[i];
			if (candidate?.OrderLine == null || candidate.PackableQuantity <= 0)
				continue;

			available = GetAvailableLabeledQuantity(buffer, candidate.ItemId);
			int acceptable = targetBox != null
				? GetAcceptableQuantityWithinFillLimit(targetBox, candidate.ItemId, candidate.PackableQuantity)
				: candidate.PackableQuantity;
			quantity = Mathf.Min(available, candidate.PackableQuantity, acceptable);
			if (quantity <= 0)
				continue;

			manifestLine = candidate;
			return true;
		}

		return false;
	}

	private static int GetAvailableLabeledQuantity(CapsuleBuffer buffer, uint itemId)
	{
		if (buffer == null || itemId == 0)
			return 0;

		int physical = 0;
		for (int i = 0; i < buffer.Stacks.Count; ++i)
		{
			ItemStack stack = buffer.Stacks[i];
			if (stack != null &&
				stack.ItemID == itemId &&
				stack.Quantity > 0 &&
				stack.HasStatus(ItemStatus.Labeled) &&
				stack.HasQuality(ItemQuality.Waste) == false)
			{
				physical += stack.Quantity;
			}
		}

		int reserved = buffer.ItemToBePicked.GetValueOrDefault(itemId);
		return Mathf.Max(0, physical - reserved);
	}

	private void EvaluateWork()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.EvaluatePackingInputWork(buildingId);
	}

	private static bool HasReachedBoxFillLimit(BoxBase box)
	{
		if (box == null || box.MaxSize <= 0.0f)
			return false;

		return (box.TotalSize / box.MaxSize) * 100.0f >= GetBoxFillLimitPercent();
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

		float remainingSize = box.MaxSize * GetBoxFillLimitPercent() / 100.0f - box.TotalSize;
		if (remainingSize <= 0.0f)
			return 0;

		return Mathf.Min(acceptable, Mathf.FloorToInt((remainingSize / itemSize) + 0.0001f));
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

		int remaining = Mathf.Max(0, line.Quantity - result.Moved);
		if (remaining > 0)
			reservable.ReleaseReservedPick(line.ItemID, remaining);
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

		BoxBase sourceBox = sourceContainer switch
		{
			BoxBase box => box,
			CapsuleBuffer buffer => buffer.DockedCapsule,
			_ => null,
		};
		if (sourceBox == null ||
			targetBox == null ||
			PickingManifestKey.From(sourceBox) == PickingManifestKey.From(targetBox))
		{
			return;
		}

		GameContext.Instance.OBWorkflowSvc?.TransferPickingManifest(
			sourceBox,
			targetBox,
			orderLine,
			itemId,
			quantity,
			false);
	}
}
