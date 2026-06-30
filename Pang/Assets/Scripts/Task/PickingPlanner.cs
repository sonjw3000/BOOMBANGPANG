using System.Collections.Generic;
using System;
using UnityEngine;

public sealed class PickingPlanner
{
	private static int jobID = 1;

	private readonly CollectPlanner<OrderLine> collectPlanner;
	private readonly ICollectSupplySource collectSupplySource;
	private readonly ICollectRequestSource<OrderLine> collectRequestSource;
	private readonly CapsuleBufferService capsuleBufferService;
	private CollectingPolicyType collectingPolicyType;
	private float boxFillLimitPercent;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;

	public PickingPlanner(
		ICollectSupplySource collectSupplySource,
		ICollectRequestSource<OrderLine> collectRequestSource,
		CapsuleBufferService capsuleBufferService,
		float boxFillLimitPercent,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest)
	{
		this.collectSupplySource = collectSupplySource;
		this.collectRequestSource = collectRequestSource;
		this.capsuleBufferService = capsuleBufferService;
		this.boxFillLimitPercent = boxFillLimitPercent;
		collectPlanner = new CollectPlanner<OrderLine>(
			collectSupplySource,
			collectRequestSource,
			CollectingPolicyFactory.Create<OrderLine>(collectingPolicyType));
		SetCollectingPolicy(collectingPolicyType);
	}

	public void SetCollectingPolicy(CollectingPolicyType policyType)
	{
		collectingPolicyType = policyType;
		collectPlanner.SetCollectingPolicy(CollectingPolicyFactory.Create<OrderLine>(policyType));
	}

	public void SetBoxFillLimitPercent(float value)
	{
		boxFillLimitPercent = value;
	}

	public bool HasPendingCollectWork()
	{
		return HasPendingCollectWork(0);
	}

	public bool HasPendingCollectWork(uint buildingId)
	{
		foreach (uint itemId in collectRequestSource.GetRequestedItemIds())
		{
			foreach (OrderLine requestLine in collectRequestSource.GetRequestLines(itemId))
			{
				if (collectRequestSource.GetAllocatableQuantity(requestLine) <= 0)
					continue;

				IEnumerable<ShelfBase> sources = buildingId != 0
					? collectSupplySource.GetSources(buildingId, itemId)
					: collectSupplySource.GetSources(itemId);

				foreach (ShelfBase _ in sources)
					return true;
			}
		}

		return false;
	}

	public bool BuildPickingTask(out PickingTask task)
	{
		return BuildPickingTask(0, out task);
	}

	public bool BuildPickingTask(uint buildingId, out PickingTask task)
	{
		task = null;
		if (HasPendingCollectWork(buildingId) == false)
			return false;

		WorkJob job = new(jobID++, new List<WorkLine>(), WorkOp.Picking);
		task = new PickingTask(job, buildingId);
		return true;
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, out WorkLine line)
	{
		return TryAllocateNextCollectLine(worker, 0, out line);
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		return TryGetCollectLine(worker, buildingId, out line) == WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		if (worker == null)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (HasReachedBoxFillLimit(box))
			return WorkPlanResult.SwitchPhase;

		if (collectPlanner.TryAllocateNextCollectLine(worker, buildingId, out line))
			return WorkPlanResult.Issued;

		return box.Stacks.Count > 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine pickedLine, out WorkLine line)
	{
		line = null;
		if (worker == null || pickedLine == null || pickedLine.Quantity <= 0)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		OutboundWorkflowService outboundWorkflowService = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		if (outboundWorkflowService == null ||
			outboundWorkflowService.GetPackableManifestQuantity(box, pickedLine.RelatedOrderLine, pickedLine.ItemID) < pickedLine.Quantity ||
			ItemTransferUtility.GetMovableQuantity(box, box, pickedLine.ItemID, pickedLine.Quantity) < pickedLine.Quantity)
		{
			return WorkPlanResult.Completed;
		}

		CapsuleBuffer bestBuffer = null;
		int bestDistance = int.MaxValue;
		foreach (CapsuleBuffer buffer in EnumeratePlaceBuffers(buildingId))
		{
			if (buffer == null)
				continue;

			int movable = ItemTransferUtility.GetMovableQuantity(box, buffer, pickedLine.ItemID, pickedLine.Quantity);
			if (movable < pickedLine.Quantity)
				continue;

			if (InteractionPointSelector.TryGetInteractionPoint(
				buffer,
				InteractionKind.Put,
				worker.GridPosition,
				out _,
				out int distance) == false)
			{
				continue;
			}

			if (distance >= bestDistance)
				continue;

			bestBuffer = buffer;
			bestDistance = distance;
		}

		if (bestBuffer == null)
			return WorkPlanResult.Waiting;

		line = new WorkLine(WorkLineAction.Put, bestBuffer, bestBuffer, pickedLine.ItemID, pickedLine.Quantity, pickedLine.RelatedOrderLine);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceLineCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (result.Kind == TransferResultKind.None)
			return WorkPlanResult.Waiting;

		return WorkPlanResult.Issued;
	}

	public float GetPickableOutstandingTotalSize(uint buildingId, ItemDatabase itemDatabase)
	{
		if (itemDatabase == null)
			return 0.0f;

		float totalSize = 0.0f;
		foreach (uint itemId in collectRequestSource.GetRequestedItemIds())
		{
			int allocatable = GetAllocatableQuantity(itemId);
			if (allocatable <= 0)
				continue;

			int pickable = GetBuildingPickableQuantity(buildingId, itemId);
			int quantity = Mathf.Min(allocatable, pickable);
			if (quantity <= 0)
				continue;

			totalSize += itemDatabase.GetItemSize(itemId) * quantity;
		}

		return totalSize;
	}

	private bool HasReachedBoxFillLimit(BoxBase box)
	{
		if (box == null || box.MaxSize <= 0.0f)
			return false;

		float filledPercent = (box.TotalSize / box.MaxSize) * 100.0f;
		return filledPercent >= boxFillLimitPercent;
	}

	private int GetAllocatableQuantity(uint itemId)
	{
		int quantity = 0;
		foreach (OrderLine requestLine in collectRequestSource.GetRequestLines(itemId))
			quantity += collectRequestSource.GetAllocatableQuantity(requestLine);

		return quantity;
	}

	private int GetBuildingPickableQuantity(uint buildingId, uint itemId)
	{
		int quantity = 0;
		IEnumerable<ShelfBase> sources = buildingId != 0
			? collectSupplySource.GetSources(buildingId, itemId)
			: collectSupplySource.GetSources(itemId);

		foreach (ShelfBase source in sources)
		{
			if (source != null)
				quantity += source.GetPickableQuantity(itemId);
		}

		return quantity;
	}

	private IEnumerable<CapsuleBuffer> EnumeratePlaceBuffers(uint buildingId)
	{
		if (capsuleBufferService == null)
			yield break;

		foreach (CapsuleBuffer buffer in capsuleBufferService.GetBuffers(buildingId))
			{
				if (buffer != null && buffer.CanReceiveOutboundItems())
					yield return buffer;
			}
	}
}
