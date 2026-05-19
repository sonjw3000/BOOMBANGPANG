using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct PlaceDecision
{
	public ShelfBase shelf;
	public uint ItemID;
	public int Quantity;
}

public interface IPlacingPolicy
{
	bool TryDecide(in int3 workerPos, BoxBase box, Predicate<ShelfBase> pred, out PlaceDecision decision);
}

public class NearestPlacingPolicy : IPlacingPolicy
{
	private ShelfStorageIndex StorageIndex => GameContext.Instance.StorageIndex;

	public bool TryDecide(in int3 workerPos, BoxBase box, Predicate<ShelfBase> pred, out PlaceDecision decision)
	{
		decision = default;

		if (box == null || box.Stacks.Count == 0)
		{
			Debug.LogWarning("Cannot decide placing target without a carried box.");
			return false;
		}

		ShelfBase best = null;
		int bestDist = int.MaxValue;
		ItemStack bestStack = box.Stacks[0];
		int quantity = 0;

		foreach (var shelf in StorageIndex.QueryPlaceCandidate(bestStack.ItemID, bestStack.Quantity))
		{
			if (pred != null && pred(shelf) == false)
			{
				continue;
			}

			int3 shelfInteraction = shelf.GetClosestInteractionPoint(InteractionKind.Put, workerPos);
			int dist =
				math.abs(workerPos.x - shelfInteraction.x) +
				math.abs(workerPos.y - shelfInteraction.y) +
				math.abs(workerPos.z - shelfInteraction.z);

			if (dist < bestDist)
			{
				bestDist = dist;
				best = shelf;
				quantity = bestStack.Quantity;
			}
		}

		if (best == null)
		{
			Debug.Log("No suitable shelf found for placing.");
			return false;
		}

		decision.shelf = best;
		decision.ItemID = bestStack.ItemID;
		decision.Quantity = quantity;
		return true;
	}
}

public class BelowAverageFilledNearestPlacingPolicy : IPlacingPolicy
{
	private ShelfStorageIndex StorageIndex => GameContext.Instance.StorageIndex;

	public bool TryDecide(in int3 workerPos, BoxBase box, Predicate<ShelfBase> pred, out PlaceDecision decision)
	{
		decision = default;

		if (box == null || box.Stacks.Count == 0)
		{
			Debug.LogWarning("Cannot decide placing target without a carried box.");
			return false;
		}

		ItemStack bestStack = box.Stacks[0];
		List<ShelfBase> candidates = new();
		float filledPercentSum = 0.0f;

		foreach (var shelf in StorageIndex.QueryPlaceCandidate(bestStack.ItemID, bestStack.Quantity))
		{
			if (pred != null && pred(shelf) == false)
			{
				continue;
			}

			candidates.Add(shelf);
			filledPercentSum += shelf.FilledPercent;
		}

		if (candidates.Count <= 0)
		{
			Debug.Log("No suitable shelf found for placing.");
			return false;
		}

		float averageFilledPercent = filledPercentSum / candidates.Count;
		ShelfBase best = null;
		int bestDist = int.MaxValue;

		for (int i = 0; i < candidates.Count; ++i)
		{
			ShelfBase shelf = candidates[i];
			if (shelf.FilledPercent > averageFilledPercent)
			{
				continue;
			}

			int3 shelfInteraction = shelf.GetClosestInteractionPoint(InteractionKind.Put, workerPos);
			int dist =
				math.abs(workerPos.x - shelfInteraction.x) +
				math.abs(workerPos.y - shelfInteraction.y) +
				math.abs(workerPos.z - shelfInteraction.z);

			if (dist < bestDist)
			{
				bestDist = dist;
				best = shelf;
			}
		}

		if (best == null)
		{
			Debug.Log("No suitable shelf found for placing.");
			return false;
		}

		decision.shelf = best;
		decision.ItemID = bestStack.ItemID;
		decision.Quantity = bestStack.Quantity;
		return true;
	}
}

