using System.Collections.Generic;
using UnityEngine;

public sealed class LaunchSortPlanner :
	IItemTransferPlanner,
	IItemTransferTaskInvalidationHandler,
	IItemTransferCollectGate
{
	private readonly uint buildingId;

	public uint BuildingId => buildingId;

	private CapsuleBufferService BufferService =>
		GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;

	public LaunchSortPlanner(uint buildingId)
	{
		this.buildingId = buildingId;
	}

	public bool HasSortableWork(AIWorker worker = null)
	{
		if (CanRunForBuilding() == false || BufferService == null)
			return false;

		foreach (CapsuleBuffer sourceBuffer in BufferService.GetBuffers(buildingId))
		{
			if (CanUseSource(sourceBuffer) == false ||
				TryGetManifest(sourceBuffer, out PickingManifest manifest) == false)
				continue;

			IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
			for (int lineIndex = 0; lineIndex < lines.Count; ++lineIndex)
			{
				PickingManifestLine manifestLine = lines[lineIndex];
				if (TryGetAvailablePackedQuantity(sourceBuffer, manifestLine, out int available) == false)
					continue;

				if (GetRejectedPackedQuantity(sourceBuffer, manifestLine.ItemId, available, excludeReserved: true) > 0)
					return true;

				if (TryFindOutboundBuffer(
					worker,
					sourceBuffer,
					manifestLine.OrderLine,
					manifestLine.ItemId,
					available,
					sourceBuffer.GridPosition,
					sourceBuffer,
					out _,
					out _))
				{
					return true;
				}
			}
		}

		return false;
	}

	public void GetPendingDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;
		if (CanRunForBuilding() == false || BufferService == null)
			return;

		foreach (CapsuleBuffer sourceBuffer in BufferService.GetBuffers(buildingId))
		{
			if (CanUseSource(sourceBuffer) == false ||
				TryGetManifest(sourceBuffer, out PickingManifest manifest) == false)
			{
				continue;
			}

			int sourceQuantity = 0;
			IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
			for (int lineIndex = 0; lineIndex < lines.Count; ++lineIndex)
			{
				PickingManifestLine manifestLine = lines[lineIndex];
				if (TryGetAvailablePackedQuantity(sourceBuffer, manifestLine, out int available) == false)
					continue;

				int rejected = GetRejectedPackedQuantity(
					sourceBuffer,
					manifestLine.ItemId,
					available,
					excludeReserved: true);
				if (rejected > 0)
				{
					sourceQuantity += rejected;
					continue;
				}

				if (TryFindOutboundBuffer(
					null,
					sourceBuffer,
					manifestLine.OrderLine,
					manifestLine.ItemId,
					available,
					sourceBuffer.GridPosition,
					sourceBuffer,
					out _,
					out int movable))
				{
					sourceQuantity += movable;
				}
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
		if (worker == null ||
			workerBox == null ||
			requestedBuildingId != buildingId ||
			CanRunForBuilding() == false ||
			BufferService == null)
			return WorkPlanResult.Waiting;

		CapsuleBuffer bestSource = null;
		PickingManifestLine bestManifestLine = null;
		int bestQuantity = 0;
		int bestDistance = int.MaxValue;

		foreach (CapsuleBuffer sourceBuffer in BufferService.GetBuffers(buildingId))
		{
			if (CanUseSource(sourceBuffer) == false ||
				TryGetManifest(sourceBuffer, out PickingManifest manifest) == false)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				sourceBuffer,
				InteractionKind.Pick,
				worker.GridPosition,
				buildingId,
				out _,
				out int sourceDistance) == false)
			{
				continue;
			}

			IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
			for (int lineIndex = 0; lineIndex < lines.Count; ++lineIndex)
			{
				PickingManifestLine manifestLine = lines[lineIndex];
				if (TryGetAvailablePackedQuantity(sourceBuffer, manifestLine, out int available) == false)
					continue;

				int rejected = GetRejectedPackedQuantity(sourceBuffer, manifestLine.ItemId, available, excludeReserved: true);
				int acceptable = workerBox.GetAcceptableQuantity(manifestLine.ItemId, available);
				int requested = rejected > 0
					? rejected
					: Mathf.Min(available, acceptable);
				if (requested <= 0)
					continue;

				int movable = requested;
				if (rejected <= 0 &&
					TryFindOutboundBuffer(
						worker,
						sourceBuffer,
						manifestLine.OrderLine,
						manifestLine.ItemId,
						requested,
						worker.GridPosition,
						sourceBuffer,
						out _,
						out movable) == false)
				{
					continue;
				}

				int quantity = Mathf.Min(requested, movable);
				if (quantity <= 0 ||
					(sourceDistance > bestDistance ||
					 (sourceDistance == bestDistance && quantity <= bestQuantity)))
				{
					continue;
				}

				bestSource = sourceBuffer;
				bestManifestLine = manifestLine;
				bestQuantity = quantity;
				bestDistance = sourceDistance;
			}
		}

		if (bestSource == null || bestManifestLine == null || bestQuantity <= 0)
			return WorkPlanResult.Completed;

		int reserved = bestSource.ReservePicking(bestManifestLine.ItemId, bestQuantity);
		if (reserved <= 0)
			return WorkPlanResult.Waiting;

		line = new WorkLine(
			WorkLineAction.Pick,
			bestSource,
			bestSource,
			bestManifestLine.ItemId,
			reserved,
			bestManifestLine.OrderLine,
			ItemStatus.Packed);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult EvaluateBeforeCollect(AIWorker worker, WorkLine line, out bool allowTransfer)
	{
		allowTransfer = true;
		if (line?.Container is not CapsuleBuffer sourceBuffer ||
			line.RelatedOrderLine == null ||
			GameContext.HasInstance == false ||
			GameContext.Instance.OBWorkflowSvc == null)
		{
			return WorkPlanResult.Issued;
		}

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		PickingManifestLine currentManifestLine = null;
		if (sourceBuffer.DockedCapsule == null ||
			outbound.TryGetPickingManifest(sourceBuffer.DockedCapsule, out PickingManifest sourceManifest) == false ||
			(currentManifestLine = sourceManifest.FindLine(line.RelatedOrderLine, line.ItemID)) == null ||
			currentManifestLine.PackedQuantity < line.Quantity)
		{
			if (line.Container is IItemPickReservable staleSource)
				staleSource.ReleaseReservedPick(line.ItemID, line.Quantity);

			allowTransfer = false;
			EvaluateWork();
			return WorkPlanResult.Completed;
		}

		int rejected = GetRejectedPackedQuantity(sourceBuffer, line.ItemID, line.Quantity, excludeReserved: false);
		if (rejected <= 0)
		{
			if (TryFindOutboundBuffer(
				worker,
				sourceBuffer,
				line.RelatedOrderLine,
				line.ItemID,
				line.Quantity,
				worker.GridPosition,
				sourceBuffer,
				out _,
				out _) == true)
			{
				return WorkPlanResult.Issued;
			}

			if (line.Container is IItemPickReservable unavailableSource)
				unavailableSource.ReleaseReservedPick(line.ItemID, line.Quantity);

			allowTransfer = false;
			EvaluateWork();
			return WorkPlanResult.Completed;
		}

		allowTransfer = false;
		int applied = outbound.RejectPackedCargo(
			sourceBuffer,
			line.RelatedOrderLine,
			line.ItemID,
			rejected);
		if (line.Container is IItemPickReservable reservable)
			reservable.ReleaseReservedPick(line.ItemID, line.Quantity);

		if (applied != rejected)
		{
			Debug.LogWarning(
				$"[LaunchSortPlanner] QC reject mismatch. item={line.ItemID}, requested={rejected}, applied={applied}");
		}

		EvaluateWork();
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		ReleaseUnmovedReservation(line, result);


		if (result.Moved <= 0)
		{
			EvaluateWork();
			return WorkPlanResult.Completed;
		}

		TransferPackedManifest(
			ResolveManifestBox(line.Container),
			worker?.CarryingAbility?.CarryingBox,
			line.RelatedOrderLine,
			line.ItemID,
			result.Moved);

		return WorkPlanResult.SwitchPhase;
	}

	public WorkPlanResult TryGetPlaceLine(
		AIWorker worker,
		uint requestedBuildingId,
		WorkLine collectedLine,
		int remainingQuantity,
		out WorkLine line)
	{
		line = null;
		BoxBase workerBox = worker?.CarryingAbility?.CarryingBox;
		if (worker == null ||
			workerBox == null ||
			collectedLine == null ||
			remainingQuantity <= 0 ||
			requestedBuildingId != buildingId)
			return WorkPlanResult.Waiting;

		OutboundWorkflowService outbound = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		if (outbound == null)
			return WorkPlanResult.Waiting;

		if (HasWasteItem(workerBox, collectedLine.ItemID))
		{
			return TryBuildWastePlaceLine(worker, workerBox, collectedLine.ItemID, remainingQuantity, out line)
				? WorkPlanResult.Issued
				: WorkPlanResult.Waiting;
		}

		if (collectedLine.RelatedOrderLine == null ||
			outbound.TryGetPickingManifest(workerBox, out PickingManifest manifest) == false ||
			manifest.FindLine(collectedLine.RelatedOrderLine, collectedLine.ItemID)?.PackedQuantity <= 0)
		{
			return WorkPlanResult.Completed;
		}

		int rejected = outbound.GetRejectedPackedQuantity(workerBox, collectedLine.ItemID, remainingQuantity);
		if (rejected > 0)
		{
			int applied = outbound.RejectPackedCargo(
				workerBox,
				collectedLine.RelatedOrderLine,
				collectedLine.ItemID,
				rejected);
			if (applied <= 0 ||
				TryBuildWastePlaceLine(worker, workerBox, collectedLine.ItemID, applied, out line) == false)
			{
				return WorkPlanResult.Waiting;
			}

			return WorkPlanResult.Issued;
		}

		if (TryFindOutboundBuffer(
			worker,
			workerBox,
			collectedLine.RelatedOrderLine,
			collectedLine.ItemID,
			remainingQuantity,
			worker.GridPosition,
			collectedLine.Container as CapsuleBuffer,
			out CapsuleBuffer targetBuffer,
			out int movable) == false)
		{
			return WorkPlanResult.Waiting;
		}

		line = new WorkLine(
			WorkLineAction.Put,
			targetBuffer,
			targetBuffer,
			collectedLine.ItemID,
			Mathf.Min(remainingQuantity, movable),
			collectedLine.RelatedOrderLine,
			ItemStatus.Packed);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(
		AIWorker worker,
		WorkLine collectedLine,
		WorkLine placeLine,
		ItemTransferResult result)
	{
		ItemTransferTask activeTask = worker?.CurrentTask as ItemTransferTask;
		CapsuleBuffer retainedTarget = placeLine?.Target as CapsuleBuffer;
		if (result.Moved <= 0)
		{
			activeTask?.ReleaseRetainedCapsuleOutput(retainedTarget);
			return WorkPlanResult.Waiting;
		}

		if (placeLine?.Target is WasteBinDock)
		{
			if (TryGetBuilding(out Building building))
				GameContext.Instance.WasteCollectionPlanner?.NotifyBuildingChanged(building);
			EvaluateWork();
			return WorkPlanResult.Issued;
		}

		if (placeLine?.Target is CapsuleBuffer targetBuffer)
		{
			TransferPackedManifest(
				worker?.CarryingAbility?.CarryingBox,
				targetBuffer.DockedCapsule,
				collectedLine?.RelatedOrderLine,
				placeLine.ItemID,
				result.Moved);
			if (GameContext.HasInstance)
				GameContext.Instance.CapsuleRelocateCoordinator.MarkDirty(targetBuffer);

			activeTask?.ReleaseRetainedCapsuleOutput(targetBuffer);
		}

		EvaluateWork();
		return WorkPlanResult.Issued;
	}

	private static bool HasWasteItem(BoxBase box, uint itemId)
	{
		if (box == null || itemId == 0)
			return false;

		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			ItemStack stack = box.Stacks[i];
			if (stack != null && stack.Quantity > 0 && stack.ItemID == itemId && stack.HasQuality(ItemQuality.Waste))
				return true;
		}

		return false;
	}

	private bool TryBuildWastePlaceLine(
		AIWorker worker,
		BoxBase workerBox,
		uint itemId,
		int requested,
		out WorkLine line)
	{
		line = null;
		if (worker == null ||
			workerBox == null ||
			buildingId == 0 ||
			itemId == 0 ||
			requested <= 0 ||
			BufferService == null)
			return false;

		ItemStack wasteStack = null;
		for (int i = workerBox.Stacks.Count - 1; i >= 0; --i)
		{
			ItemStack candidate = workerBox.Stacks[i];
			if (candidate != null &&
				candidate.Quantity > 0 &&
				candidate.ItemID == itemId &&
				candidate.HasQuality(ItemQuality.Waste))
			{
				wasteStack = candidate;
				break;
			}
		}
		if (wasteStack == null)
			return false;

		WasteBinDock bestTarget = null;
		int bestMovable = 0;
		int bestDistance = int.MaxValue;
		foreach (CapsuleBuffer buffer in BufferService.GetBuffers(buildingId))
		{
			if (buffer is not WasteBinDock candidate ||
				candidate.DockedCapsule is not WasteBin wasteBin ||
				wasteBin.IsFull)
			{
				continue;
			}

			int movable = ItemTransferUtility.GetMovableQuantity(
				workerBox,
				candidate,
				itemId,
				Mathf.Min(requested, wasteStack.Quantity),
				stack => stack.HasQuality(ItemQuality.Waste) && stack.Status == wasteStack.Status);
			if (movable <= 0 ||
				InteractionPointSelector.TryGetInteractionPointInBuilding(
					candidate,
					InteractionKind.Put,
					worker.GridPosition,
					buildingId,
					out _,
					out int distance) == false ||
				distance >= bestDistance)
			{
				continue;
			}

			bestTarget = candidate;
			bestMovable = movable;
			bestDistance = distance;
		}

		if (bestTarget == null || bestMovable <= 0)
			return false;

		line = new WorkLine(
			WorkLineAction.Put,
			bestTarget,
			bestTarget,
			itemId,
			bestMovable,
			requiredStatus: wasteStack.Status,
			requiredQuality: ItemQuality.Waste,
			consumeSourcePickReservation: false);
		return true;
	}

	public void OnTaskInvalidated(ItemTransferTask task)
	{
		if (task?.Phase == ItemTransferPhase.Collect &&
			task.CurrentLine is WorkLine line &&
			line.Container is IItemPickReservable reservable)
		{
			int remaining = Mathf.Max(0, line.Quantity - line.CompleteQuantity);
			if (remaining > 0)
				reservable.ReleaseReservedPick(line.ItemID, remaining);
		}

		EvaluateWork();
	}

	private bool TryGetAvailablePackedQuantity(
		CapsuleBuffer sourceBuffer,
		PickingManifestLine manifestLine,
		out int quantity)
	{
		quantity = 0;
		if (sourceBuffer == null ||
			manifestLine?.OrderLine == null ||
			manifestLine.PackedQuantity <= 0 ||
			sourceBuffer.CanProvideInboundItems() == false)
		{
			return false;
		}

		int available = GetAvailablePackedQuantity(sourceBuffer, manifestLine.ItemId);
		quantity = Mathf.Min(available, manifestLine.PackedQuantity);
		return quantity > 0;
	}

	private static int GetAvailablePackedQuantity(CapsuleBuffer sourceBuffer, uint itemId)
	{
		if (sourceBuffer == null || itemId == 0)
			return 0;

		int physical = 0;
		for (int i = 0; i < sourceBuffer.Stacks.Count; ++i)
		{
			ItemStack stack = sourceBuffer.Stacks[i];
			if (stack != null &&
				stack.ItemID == itemId &&
				stack.Quantity > 0 &&
				stack.HasStatus(ItemStatus.Packed))
			{
				physical += stack.Quantity;
			}
		}

		int reserved = sourceBuffer.ItemToBePicked.GetValueOrDefault(itemId);
		return Mathf.Max(0, physical - reserved);
	}

	private static int GetRejectedPackedQuantity(
		CapsuleBuffer sourceBuffer,
		uint itemId,
		int limit,
		bool excludeReserved)
	{
		OutboundWorkflowService outbound = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		if (sourceBuffer == null ||
			itemId == 0 ||
			limit <= 0 ||
			outbound == null)
		{
			return 0;
		}

		return outbound.GetRejectedPackedQuantity(sourceBuffer, itemId, limit, excludeReserved);
	}

	private bool TryFindOutboundBuffer(
		AIWorker worker,
		IItemContainer source,
		OrderLine orderLine,
		uint itemId,
		int requested,
		in Unity.Mathematics.int3 from,
		CapsuleBuffer excludedBuffer,
		out CapsuleBuffer buffer,
		out int movableQuantity)
	{
		buffer = null;
		movableQuantity = 0;
		if (buildingId == 0 ||
			source == null ||
			orderLine == null ||
			itemId == 0 ||
			requested <= 0 ||
			BufferService == null)
			return false;

		FacilityFilter filter = FacilityFilter.WithItemProcessStage(
			FacilityFilter.WithManifest(
				FacilityFilter.ForTransfer(
					source,
					itemId,
					requested,
					stack => stack.HasStatus(ItemStatus.Packed) &&
						stack.HasQuality(ItemQuality.Waste) == false,
					worker),
				FacilityManifestFilter.FromOrderLine(orderLine)),
			ItemProcessStage.Packed);
		FacilityFilter launchFilter = FacilityFilter.WithItemProcessStage(
			filter,
			ItemProcessStage.LaunchReady);
		CapsuleRelocateCoordinator coordinator = GameContext.Instance.CapsuleRelocateCoordinator;
		if (coordinator?.HasProjectedOutboundRoute(buildingId, filter) != true &&
			coordinator?.HasProjectedOutboundRoute(buildingId, launchFilter) != true)
		{
			return false;
		}

		int bestPriority = int.MinValue;
		int bestMovable = 0;
		int bestDistance = int.MaxValue;
		bool bestRetained = false;
		ItemTransferTask activeTask = worker?.CurrentTask as ItemTransferTask;
		foreach (CapsuleBuffer candidate in BufferService.GetBuffers(buildingId))
		{
			if (ReferenceEquals(candidate, excludedBuffer) ||
				IsCapsuleOutputCandidate(
					activeTask,
					candidate,
					filter) == false)
			{
				continue;
			}

			int candidateMovable = ItemTransferUtility.GetMovableQuantity(
				source,
				candidate,
				itemId,
				requested,
				stack => stack.HasStatus(ItemStatus.Packed) &&
					stack.HasQuality(ItemQuality.Waste) == false);
			if (candidateMovable <= 0)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				candidate,
				InteractionKind.Put,
				from,
				buildingId,
				out _,
				out int distance) == false)
			{
				continue;
			}

			bool retained = activeTask?.RetainsCapsuleOutput(candidate) == true;
			int priority = GetRulePriority(candidate);
			bool isBetter = buffer == null ||
				(retained && bestRetained == false) ||
				(retained == bestRetained && priority > bestPriority) ||
				(retained == bestRetained && priority == bestPriority && candidateMovable > bestMovable) ||
				(retained == bestRetained && priority == bestPriority && candidateMovable == bestMovable && distance < bestDistance);
			if (isBetter == false)
				continue;

			buffer = candidate;
			movableQuantity = candidateMovable;
			bestRetained = retained;
			bestPriority = priority;
			bestMovable = candidateMovable;
			bestDistance = distance;
		}

		return buffer != null && movableQuantity > 0;
	}

	private bool CanRunForBuilding()
	{
		return TryGetBuilding(out _);
	}

	private bool TryGetBuilding(out Building building)
	{
		building = null;
		return buildingId != 0 &&
			GameContext.HasInstance &&
			GameContext.Instance.BuildingMgr?.TryGetBuilding(buildingId, out building) == true &&
			building != null;
	}

	private bool CanUseSource(CapsuleBuffer sourceBuffer)
	{
		if (sourceBuffer?.DockedCapsule is not CargoCapsule capsule ||
			capsule.RouteKind != CargoRouteKind.Standard ||
			capsule.LogisticsState != CapsuleLogisticsState.Inside ||
			sourceBuffer.CanProvideInboundItems() == false ||
			BufferService?.IsExplicitRuleMatchedBuffer(
				sourceBuffer,
				capsule,
				ItemProcessStage.Packed,
				evaluateLaunchReadiness: false) != true ||
			GameContext.Instance.FacilityMgr?.IsInvalidating(sourceBuffer) == true ||
			GameContext.Instance.TaskMgr?.HasConflictingCapsuleContentDependency(
				sourceBuffer,
				WorkLineAction.Pick) == true ||
			GameContext.Instance.CapsuleRelocateCoordinator?.HasMatchedOutboundRoute(sourceBuffer) == true)
		{
			return false;
		}

		return IsDockAvailable(sourceBuffer);
	}

	private bool IsProjectedInputRuleMatchedBuffer(
		CapsuleBuffer buffer,
		FacilityFilter projectedInputFilter)
	{
		return IsCapsuleOutputCandidate(
			task: null,
			buffer: buffer,
			projectedInputFilter: projectedInputFilter,
			requireEmpty: true);
	}

	private bool IsCapsuleOutputCandidate(
		ItemTransferTask task,
		CapsuleBuffer buffer,
		FacilityFilter projectedInputFilter,
		bool requireEmpty = false)
	{
		if (buffer?.DockedCapsule is not CargoCapsule capsule ||
			capsule.RouteKind != CargoRouteKind.Standard ||
			buffer.CanReceiveOutboundItems() == false ||
			projectedInputFilter.ItemProcessStage != ItemProcessStage.Packed ||
			BufferService?.IsExplicitRuleMatchedBuffer(
				buffer,
				projectedInputFilter,
				ItemProcessStage.Packed) != true)
		{
			return false;
		}

		bool isEmptyInput =
			capsule.LogisticsState == CapsuleLogisticsState.Empty &&
			buffer.IsCapsuleEmpty();
		bool isSharedPackedInput =
			requireEmpty == false &&
			capsule.LogisticsState == CapsuleLogisticsState.Inside &&
			buffer.IsCapsuleEmpty() == false;
		if (isEmptyInput == false && isSharedPackedInput == false)
			return false;

		if (isSharedPackedInput &&
			BufferService.IsRuleMatchedBuffer(buffer, capsule, evaluateLaunchReadiness: false) == false)
		{
			return false;
		}

		if (GameContext.Instance.FacilityMgr?.IsInvalidating(buffer) == true ||
			GameContext.Instance.TaskMgr?.HasConflictingCapsuleContentDependency(
				buffer,
				WorkLineAction.Put) == true)
		{
			return false;
		}

		return IsDockAvailable(buffer);
	}

	private static bool IsDockAvailable(CapsuleDock dock)
	{
		CapsuleRelocateCoordinator coordinator = GameContext.HasInstance
			? GameContext.Instance.ExistingCapsuleRelocateCoordinator
			: null;
		return coordinator == null ||
			(coordinator.IsPlayerClaimed(dock) == false &&
			 coordinator.IsReserved(dock) == false &&
			 coordinator.IsRelocationSourceActive(dock) == false &&
			 coordinator.IsRelocationTargetActive(dock) == false);
	}

	private void EvaluateWork()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.QueueLaunchSortEvaluation(buildingId);
	}

	private static bool TryGetManifest(CapsuleBuffer sourceBuffer, out PickingManifest manifest)
	{
		manifest = null;
		return sourceBuffer != null &&
			sourceBuffer.CanProvideInboundItems() &&
			sourceBuffer.DockedCapsule != null &&
			GameContext.HasInstance &&
			GameContext.Instance.OBWorkflowSvc != null &&
			GameContext.Instance.OBWorkflowSvc.TryGetPickingManifest(sourceBuffer.DockedCapsule, out manifest);
	}

	private static int GetRulePriority(CapsuleBuffer buffer)
	{
		FacilityRuleManager manager = GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;
		return buffer != null &&
			manager != null &&
			manager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule != null
			? preset.Rule.Priority
			: 0;
	}

	private static void ReleaseUnmovedReservation(WorkLine line, ItemTransferResult result)
	{
		if (line?.Container is not IItemPickReservable reservable)
			return;

		int remaining = Mathf.Max(0, line.Quantity - result.Moved);
		if (remaining > 0)
			reservable.ReleaseReservedPick(line.ItemID, remaining);
	}

	private static void TransferPackedManifest(
		BoxBase source,
		BoxBase target,
		OrderLine orderLine,
		uint itemId,
		int quantity)
	{
		if (source == null || target == null || orderLine == null || quantity <= 0 || GameContext.HasInstance == false)
			return;

		int moved = GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.TransferPickingManifest(
				source,
				target,
				orderLine,
				itemId,
				quantity,
				true)
			: 0;
		if (moved != quantity)
		{
			Debug.LogWarning(
				$"[LaunchSortPlanner] Packed manifest transfer mismatch. item={itemId}, requested={quantity}, moved={moved}");
		}
	}

	private static BoxBase ResolveManifestBox(IItemContainer container)
	{
		return container switch
		{
			BoxBase box => box,
			CapsuleBuffer capsuleBuffer => capsuleBuffer.DockedCapsule,
			_ => null,
		};
	}
}
