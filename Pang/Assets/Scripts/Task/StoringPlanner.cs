using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class StoringPlanner : IItemTransferPlanner
{
	private static int jobID = 1;

	private readonly CapsuleBufferService capsuleBufferService;
	private CollectingPolicyType collectingPolicyType;
	private PlacingPolicyType placingPolicyType;
	private IPlacingPolicy placingPolicy;
	private float boxFillLimitPercent;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;
	public PlacingPolicyType PlacingPolicyType => placingPolicyType;

	public StoringPlanner(
		CapsuleBufferService capsuleBufferService,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest,
		PlacingPolicyType placingPolicyType = PlacingPolicyType.Nearest,
		float boxFillLimitPercent = 80.0f)
	{
		this.capsuleBufferService = capsuleBufferService;
		this.boxFillLimitPercent = boxFillLimitPercent;
		SetCollectingPolicy(collectingPolicyType);
		SetPlacingPolicy(placingPolicyType);
	}

	public void SetCollectingPolicy(CollectingPolicyType policyType)
	{
		collectingPolicyType = policyType;
	}

	public void SetPlacingPolicy(PlacingPolicyType policyType)
	{
		placingPolicyType = policyType;
		placingPolicy = PlacingPolicyFactory.Create(policyType);
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
		foreach (CapsuleBuffer buffer in EnumerateCollectBuffers(buildingId))
		{
			if (HasCollectableItem(buffer, buildingId))
				return true;
		}

		return false;
	}

	public void GetPendingDemand(uint buildingId, out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;

		foreach (CapsuleBuffer buffer in EnumerateCollectBuffers(buildingId))
		{
			if (CanProvideStoringItems(buffer, buildingId) == false)
				continue;

			foreach (var itemTotal in buffer.ItemTotals)
			{
				int quantity = GetNonWasteQuantity(buffer, itemTotal.Key);
				if (quantity <= 0)
					continue;

				++sourceCount;
				itemQuantity += quantity;
			}
		}
	}

	public bool BuildItemTransferTask(AIWorker preferredWorker, uint buildingId, out ItemTransferTask task)
	{
		task = null;
		if (HasPendingCollectWork(buildingId) == false)
			return false;

		ItemTransferJob job = new(
			this,
			TransferObjectType.Item,
			TransferObjectType.Item,
			buildingId,
			preferredWorker);
		task = new ItemTransferTask(WorkerTask.TaskType.Storing, job);
		return true;
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, out WorkLine line)
	{
		return TryGetCollectLine(worker, 0, out line) == WorkPlanResult.Issued;
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		return TryGetCollectLine(worker, buildingId, out line) == WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (worker == null || box == null)
			return WorkPlanResult.Waiting;

		if (HasReachedBoxFillLimit(box))
			return WorkPlanResult.SwitchPhase;

		CapsuleBuffer bestBuffer = null;
		uint bestItemId = 0;
		int bestQuantity = 0;
		int bestDistance = int.MaxValue;

		foreach (CapsuleBuffer buffer in EnumerateCollectBuffers(buildingId))
		{
			if (HasCollectableItem(buffer, buildingId) == false)
				continue;

			foreach (var itemTotal in buffer.ItemTotals)
			{
				uint itemId = itemTotal.Key;
				int available = ItemTransferUtility.GetMovableQuantity(
					buffer,
					box,
					itemId,
					itemTotal.Value,
					stack => stack.HasQuality(ItemQuality.Waste) == false);
				if (available <= 0)
					continue;

				int acceptable = box.GetAcceptableQuantity(itemId, available);
				if (acceptable <= 0)
					continue;

				if (InteractionPointSelector.TryGetInteractionPointInBuilding(
					buffer,
					InteractionKind.Pick,
					worker.GridPosition,
					worker.PrimaryBuildingId,
					out _,
					out int distance) == false)
				{
					continue;
				}

				int quantity = Mathf.Min(available, acceptable);
				if (IsBetterCollectCandidate(quantity, distance, bestQuantity, bestDistance) == false)
					continue;

				bestBuffer = buffer;
				bestItemId = itemId;
				bestQuantity = quantity;
				bestDistance = distance;
			}
		}

		if (bestBuffer != null && bestQuantity > 0)
		{
			line = new WorkLine(
				WorkLineAction.Pick,
				bestBuffer,
				bestBuffer,
				bestItemId,
				bestQuantity,
				excludedQuality: ItemQuality.Waste);
			return WorkPlanResult.Issued;
		}

		return box.Stacks.Count > 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;
	}

	private bool IsBetterCollectCandidate(int quantity, int distance, int bestQuantity, int bestDistance)
	{
		if (collectingPolicyType == CollectingPolicyType.LargestQuantityNearest)
			return quantity > bestQuantity || (quantity == bestQuantity && distance < bestDistance);

		return distance < bestDistance || (distance == bestDistance && quantity > bestQuantity);
	}

	public WorkPlanResult OnCollectLineCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (result.Kind == TransferResultKind.None)
			return WorkPlanResult.Waiting;

		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (HasReachedBoxFillLimit(box))
			return WorkPlanResult.SwitchPhase;

		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		return OnCollectLineCompleted(worker, line, result);
	}

	public bool TryDecideNextPlacingLine(AIWorker worker, out WorkLine line)
	{
		return TryGetPlaceLine(worker, 0, out line) == WorkPlanResult.Issued;
	}

	public bool TryDecideNextPlacingLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		return TryGetPlaceLine(worker, buildingId, out line) == WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, out WorkLine line)
	{
		return TryGetPlaceLine(worker, 0, out line);
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;

		if (worker == null || placingPolicy == null)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (box.Stacks.Count <= 0)
			return WorkPlanResult.SwitchPhase;

		ItemStack targetStack = box.Stacks[0];
		return TryGetPlaceLine(worker, buildingId, targetStack.ItemID, targetStack.Quantity, out line);
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine collectedLine, int remainingQuantity, out WorkLine line)
	{
		line = null;
		if (worker == null || collectedLine == null || remainingQuantity <= 0)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (box.ItemTotals.TryGetValue(collectedLine.ItemID, out int carriedQuantity) == false || carriedQuantity <= 0)
			return box.Stacks.Count <= 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;

		int quantity = Mathf.Min(remainingQuantity, carriedQuantity);
		return TryGetPlaceLine(worker, buildingId, collectedLine.ItemID, quantity, out line);
	}

	public WorkPlanResult OnPlaceLineCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (result.Kind == TransferResultKind.None)
			return WorkPlanResult.Waiting;

		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		return box.Stacks.Count <= 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result)
	{
		return OnPlaceLineCompleted(worker, placeLine, result);
	}

	private IEnumerable<CapsuleBuffer> EnumerateCollectBuffers(uint buildingId)
	{
		if (capsuleBufferService == null)
			yield break;

		foreach (CapsuleBuffer buffer in capsuleBufferService.GetBuffers(buildingId))
			yield return buffer;
	}

	private bool HasCollectableItem(CapsuleBuffer buffer, uint buildingId)
	{
		if (CanProvideStoringItems(buffer, buildingId) == false)
			return false;

		for (int i = 0; i < buffer.Stacks.Count; ++i)
		{
			ItemStack stack = buffer.Stacks[i];
			if (stack != null && stack.Quantity > 0 && stack.HasQuality(ItemQuality.Waste) == false)
				return true;
		}

		return false;
	}

	private bool CanProvideStoringItems(CapsuleBuffer buffer, uint buildingId)
	{
		if (buffer == null || buffer.CanProvideInboundItems() == false)
			return false;

		uint ownerBuildingId = buildingId;
		if (ownerBuildingId == 0)
			capsuleBufferService?.TryGetRegisteredBuildingId(buffer, out ownerBuildingId);

		BuildingManager buildingManager = GameContext.HasInstance
			? GameContext.Instance.BuildingMgr
			: null;
		if (ownerBuildingId != 0 &&
			buildingManager != null &&
			buildingManager.TryGetBuilding(ownerBuildingId, out Building building) &&
			building is StorageBuilding storageBuilding)
		{
			return storageBuilding.CanProvideStoringItems(buffer);
		}

		return true;
	}

	private static int GetNonWasteQuantity(CapsuleBuffer buffer, uint itemId)
	{
		if (buffer == null)
			return 0;

		int quantity = 0;
		for (int i = 0; i < buffer.Stacks.Count; ++i)
		{
			ItemStack stack = buffer.Stacks[i];
			if (stack != null &&
				stack.ItemID == itemId &&
				stack.Quantity > 0 &&
				stack.HasQuality(ItemQuality.Waste) == false)
			{
				quantity += stack.Quantity;
			}
		}

		return quantity;
	}

	private bool IsShelfInBuilding(ShelfBase shelf, uint buildingId)
	{
		if (buildingId == 0)
			return true;

		if (shelf == null || GridService == null)
			return false;

		GridCell cell = GridService.GetCell(shelf.GridPosition);
		return cell != null && cell.BuildingId == buildingId;
	}

	private WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, uint itemId, int quantity, out WorkLine line)
	{
		line = null;
		if (placingPolicy == null || worker == null || itemId == 0 || quantity <= 0)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		FacilityFilter filter = FacilityFilter.ForTransfer(box, itemId, quantity, worker: worker);
		if (placingPolicy.TryDecide(
			worker.GridPosition,
			worker.PrimaryBuildingId,
			itemId,
			quantity,
			shelf => IsShelfInBuilding(shelf, buildingId) && filter.MatchesCurrentRules(shelf),
			out PlaceDecision decision) == false ||
			decision.shelf == null ||
			decision.Quantity <= 0)
		{
			return WorkPlanResult.Waiting;
		}

		line = new WorkLine(
			WorkLineAction.Put,
			decision.shelf,
			decision.shelf,
			decision.ItemID,
			decision.Quantity);
		return WorkPlanResult.Issued;
	}

	private bool HasReachedBoxFillLimit(BoxBase box)
	{
		if (box == null || box.MaxSize <= 0.0f)
			return false;

		float filledPercent = (box.TotalSize / box.MaxSize) * 100.0f;
		return filledPercent >= boxFillLimitPercent;
	}
}
