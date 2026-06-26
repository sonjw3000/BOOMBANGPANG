using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class StoringPlanner
{
	private static int jobID = 1;

	private readonly CapsuleBufferService capsuleBufferService;
	private CollectingPolicyType collectingPolicyType;
	private PlacingPolicyType placingPolicyType;
	private IPlacingPolicy placingPolicy;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;
	public PlacingPolicyType PlacingPolicyType => placingPolicyType;

	public StoringPlanner(
		CapsuleBufferService capsuleBufferService,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest,
		PlacingPolicyType placingPolicyType = PlacingPolicyType.BelowAverageFilledNearest)
	{
		this.capsuleBufferService = capsuleBufferService;
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

	public bool HasPendingCollectWork()
	{
		return HasPendingCollectWork(0);
	}

	public bool HasPendingCollectWork(uint buildingId)
	{
		foreach (CapsuleBuffer buffer in EnumerateCollectBuffers(buildingId))
		{
			if (HasCollectableItem(buffer))
				return true;
		}

		return false;
	}

	public bool BuildStoreTask(out StoringTask task)
	{
		return BuildStoreTask(0, out task);
	}

	public bool BuildStoreTask(uint buildingId, out StoringTask task)
	{
		task = null;
		if (HasPendingCollectWork(buildingId) == false)
			return false;

		WorkJob job = new(jobID++, new List<WorkLine>(), WorkOp.Storing);
		task = new StoringTask(job, buildingId);
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

		if (box.TotalSize >= box.MaxSize)
			return WorkPlanResult.SwitchPhase;

		CapsuleBuffer bestBuffer = null;
		uint bestItemId = 0;
		int bestQuantity = 0;
		int bestDistance = int.MaxValue;

		foreach (CapsuleBuffer buffer in EnumerateCollectBuffers(buildingId))
		{
			if (buffer == null || HasCollectableItem(buffer) == false)
				continue;

			foreach (var itemTotal in buffer.ItemTotals)
			{
				uint itemId = itemTotal.Key;
				int available = buffer.GetQuantity(itemId);
				if (available <= 0)
					continue;

				int acceptable = box.GetAcceptableQuantity(itemId, available);
				if (acceptable <= 0)
					continue;

				if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
					buffer,
					InteractionKind.Pick,
					worker.GridPosition,
					GameContext.Instance.GridService,
					out _,
					out int distance) == false)
				{
					continue;
				}

				int quantity = Mathf.Min(available, acceptable);
				if (distance >= bestDistance)
					continue;

				bestBuffer = buffer;
				bestItemId = itemId;
				bestQuantity = quantity;
				bestDistance = distance;
			}
		}

		if (bestBuffer != null && bestQuantity > 0)
		{
			line = new WorkLine(WorkLineAction.Pick, bestBuffer, bestBuffer, bestItemId, bestQuantity);
			return WorkPlanResult.Issued;
		}

		return box.Stacks.Count > 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;
	}

	public WorkPlanResult OnCollectLineCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (result.Kind == TransferResultKind.None)
			return WorkPlanResult.Waiting;

		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (box.TotalSize >= box.MaxSize)
			return WorkPlanResult.SwitchPhase;

		return WorkPlanResult.Issued;
	}

	public bool TryDecideNextPlacingLine(AIWorker worker, out WorkLine line)
	{
		return TryGetPlaceLine(worker, out line) == WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, out WorkLine line)
	{
		line = null;

		if (worker == null || placingPolicy == null)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (box.Stacks.Count <= 0)
			return WorkPlanResult.SwitchPhase;

		if (placingPolicy.TryDecide(worker.GridPosition, box, null, out var decision) == false)
			return WorkPlanResult.Waiting;

		if (decision.shelf == null || decision.Quantity <= 0)
			return WorkPlanResult.Waiting;

		line = new WorkLine(WorkLineAction.Put, decision.shelf, decision.shelf, decision.ItemID, decision.Quantity);
		return WorkPlanResult.Issued;
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

	public float GetCollectOutstandingTotalSize(uint buildingId, ItemDatabase itemDatabase)
	{
		if (itemDatabase == null)
			return 0.0f;

		float totalSize = 0.0f;
		foreach (CapsuleBuffer buffer in EnumerateCollectBuffers(buildingId))
		{
			if (buffer == null)
				continue;

			foreach (var itemTotal in buffer.ItemTotals)
			{
				if (itemTotal.Value <= 0)
					continue;

				totalSize += itemDatabase.GetItemSize(itemTotal.Key) * itemTotal.Value;
			}
		}

		return totalSize;
	}

	private IEnumerable<CapsuleBuffer> EnumerateCollectBuffers(uint buildingId)
	{
		if (capsuleBufferService == null)
			yield break;

		foreach (CapsuleBuffer buffer in capsuleBufferService.GetBuffers(buildingId))
			yield return buffer;
	}

	private static bool HasCollectableItem(CapsuleBuffer buffer)
	{
		return buffer != null && buffer.HasCapsule && buffer.IsCapsuleEmpty() == false && buffer.ItemTotals.Count > 0;
	}
}
