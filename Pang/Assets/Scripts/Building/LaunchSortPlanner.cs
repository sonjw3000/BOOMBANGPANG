using System.Collections.Generic;
using UnityEngine;

public sealed class LaunchSortPlanner : IItemTransferPlanner, IItemTransferTaskInvalidationHandler
{
	private readonly LaunchBuilding building;

	public LaunchSortPlanner(LaunchBuilding building)
	{
		this.building = building;
	}

	public bool HasSortableWork(AIWorker worker = null)
	{
		if (building == null || GameContext.HasInstance == false)
			return false;

		for (int i = 0; i < building.OccupiedCapsuleBuffers.Count; ++i)
		{
			CapsuleBuffer sourceBuffer = building.OccupiedCapsuleBuffers[i];
			if (TryGetManifest(sourceBuffer, out PickingManifest manifest) == false)
				continue;

			IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
			for (int lineIndex = 0; lineIndex < lines.Count; ++lineIndex)
			{
				PickingManifestLine manifestLine = lines[lineIndex];
				if (TryGetAvailablePackedQuantity(sourceBuffer, manifestLine, out int available) == false)
					continue;

				if (TryFindOutboundBuffer(
					worker,
					sourceBuffer,
					manifestLine.OrderLine,
					manifestLine.ItemId,
					available,
					sourceBuffer.GridPosition,
					out _,
					out _))
				{
					return true;
				}
			}
		}

		return false;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		BoxBase workerBox = worker?.CarryingAbility?.CarryingBox;
		if (worker == null || workerBox == null || building == null)
			return WorkPlanResult.Waiting;

		CapsuleBuffer bestSource = null;
		PickingManifestLine bestManifestLine = null;
		int bestQuantity = 0;
		int bestDistance = int.MaxValue;

		for (int i = 0; i < building.OccupiedCapsuleBuffers.Count; ++i)
		{
			CapsuleBuffer sourceBuffer = building.OccupiedCapsuleBuffers[i];
			if (TryGetManifest(sourceBuffer, out PickingManifest manifest) == false)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				sourceBuffer,
				InteractionKind.Pick,
				worker.GridPosition,
				building.RuntimeBuildingId,
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

				int acceptable = workerBox.GetAcceptableQuantity(manifestLine.ItemId, available);
				int requested = Mathf.Min(available, acceptable);
				if (requested <= 0 ||
					TryFindOutboundBuffer(
						worker,
						sourceBuffer,
						manifestLine.OrderLine,
						manifestLine.ItemId,
						requested,
						worker.GridPosition,
						out _,
						out int movable) == false)
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

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		ReleaseUnmovedReservation(line, result);


		if (result.Moved <= 0)
		{
			building?.EvaluateLaunchSortWork();
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
		uint buildingId,
		WorkLine collectedLine,
		int remainingQuantity,
		out WorkLine line)
	{
		line = null;
		BoxBase workerBox = worker?.CarryingAbility?.CarryingBox;
		if (worker == null || workerBox == null || collectedLine == null || remainingQuantity <= 0)
			return WorkPlanResult.Waiting;

		OutboundWorkflowService outbound = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		if (outbound == null ||
			outbound.TryGetPickingManifest(workerBox, out PickingManifest manifest) == false ||
			manifest.FindLine(collectedLine.RelatedOrderLine, collectedLine.ItemID)?.PackedQuantity <= 0)
		{
			return WorkPlanResult.Completed;
		}

		if (TryFindOutboundBuffer(
			worker,
			workerBox,
			collectedLine.RelatedOrderLine,
			collectedLine.ItemID,
			remainingQuantity,
			worker.GridPosition,
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
		if (result.Moved <= 0)
			return WorkPlanResult.Waiting;

		if (placeLine?.Target is CapsuleBuffer targetBuffer)
		{
			TransferPackedManifest(
				worker?.CarryingAbility?.CarryingBox,
				targetBuffer.DockedCapsule,
				collectedLine?.RelatedOrderLine,
				placeLine.ItemID,
				result.Moved);
		}

		building?.EvaluateLaunchSortWork();
		return WorkPlanResult.Issued;
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

		building?.EvaluateLaunchSortWork();
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

		int available = building.GetAvailableItemQuantity(
			sourceBuffer,
			manifestLine.ItemId,
			ItemStatus.Packed);
		quantity = Mathf.Min(available, manifestLine.PackedQuantity);
		return quantity > 0;
	}

	private bool TryFindOutboundBuffer(
		AIWorker worker,
		IItemContainer source,
		OrderLine orderLine,
		uint itemId,
		int requested,
		in Unity.Mathematics.int3 from,
		out CapsuleBuffer buffer,
		out int movableQuantity)
	{
		buffer = null;
		movableQuantity = 0;
		if (building == null || source == null || orderLine == null || itemId == 0 || requested <= 0)
			return false;

		FacilityFilter filter = FacilityFilter.WithManifest(
			FacilityFilter.ForTransfer(
				source,
				itemId,
				requested,
				stack => stack.HasStatus(ItemStatus.Packed),
				worker),
			FacilityManifestFilter.FromOrderLine(orderLine));

		int bestPriority = int.MinValue;
		int bestMovable = 0;
		int bestDistance = int.MaxValue;
		for (int i = 0; i < building.OccupiedCapsuleBuffers.Count; ++i)
		{
			CapsuleBuffer candidate = building.OccupiedCapsuleBuffers[i];
			if (candidate == null ||
				candidate.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId ||
				candidate.CanReceiveOutboundItems() == false ||
				filter.MatchesCurrentRules(candidate) == false)
			{
				continue;
			}

			int candidateMovable = ItemTransferUtility.GetMovableQuantity(
				source,
				candidate,
				itemId,
				requested,
				stack => stack.HasStatus(ItemStatus.Packed));
			if (candidateMovable <= 0)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				candidate,
				InteractionKind.Put,
				from,
				building.RuntimeBuildingId,
				out _,
				out int distance) == false)
			{
				continue;
			}

			int priority = GetRulePriority(candidate);
			bool isBetter = buffer == null ||
				priority > bestPriority ||
				(priority == bestPriority && candidateMovable > bestMovable) ||
				(priority == bestPriority && candidateMovable == bestMovable && distance < bestDistance);
			if (isBetter == false)
				continue;

			buffer = candidate;
			movableQuantity = candidateMovable;
			bestPriority = priority;
			bestMovable = candidateMovable;
			bestDistance = distance;
		}

		return buffer != null && movableQuantity > 0;
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
