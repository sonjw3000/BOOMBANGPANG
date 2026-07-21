using System;
using System.Collections.Generic;
using Unity.Mathematics;

public struct PlaceDecision
{
	public ShelfBase shelf;
	public uint ItemID;
	public int Quantity;
}

public interface IPlacingPolicy
{
	bool TryDecide(
		in int3 workerPos,
		uint workerBuildingId,
		uint itemId,
		int requestedQuantity,
		Predicate<ShelfBase> pred,
		out PlaceDecision decision);
}

public class NearestPlacingPolicy : IPlacingPolicy
{
	private ShelfStorageService StorageService => GameContext.Instance.StorageService;

	public bool TryDecide(
		in int3 workerPos,
		uint workerBuildingId,
		uint itemId,
		int requestedQuantity,
		Predicate<ShelfBase> pred,
		out PlaceDecision decision)
	{
		decision = default;

		if (itemId == 0 || requestedQuantity <= 0)
			return false;

		ShelfBase best = null;
		int bestDist = int.MaxValue;
		int bestQuantity = 0;

		foreach (ShelfBase shelf in StorageService.QueryPlaceCandidate(itemId, 1))
		{
			if (shelf == null || (pred != null && pred(shelf) == false))
				continue;

			int acceptable = shelf.GetAcceptableQuantity(itemId, requestedQuantity);
			if (acceptable <= 0)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				shelf,
				InteractionKind.Put,
				workerPos,
				workerBuildingId,
				out _,
				out int dist) == false)
				continue;

			if (dist < bestDist)
			{
				bestDist = dist;
				best = shelf;
				bestQuantity = acceptable;
			}
		}

		if (best == null || bestQuantity <= 0)
			return false;

		decision.shelf = best;
		decision.ItemID = itemId;
		decision.Quantity = bestQuantity;
		return true;
	}
}

public class BelowAverageFilledNearestPlacingPolicy : IPlacingPolicy
{
	private ShelfStorageService StorageService => GameContext.Instance.StorageService;

	public bool TryDecide(
		in int3 workerPos,
		uint workerBuildingId,
		uint itemId,
		int requestedQuantity,
		Predicate<ShelfBase> pred,
		out PlaceDecision decision)
	{
		decision = default;

		if (itemId == 0 || requestedQuantity <= 0)
			return false;

		List<ShelfBase> candidates = new();
		float filledPercentSum = 0.0f;

		foreach (ShelfBase shelf in StorageService.QueryPlaceCandidate(itemId, 1))
		{
			if (shelf == null || (pred != null && pred(shelf) == false))
				continue;

			if (shelf.GetAcceptableQuantity(itemId, requestedQuantity) <= 0)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				shelf,
				InteractionKind.Put,
				workerPos,
				workerBuildingId,
				out _,
				out _) == false)
				continue;

			candidates.Add(shelf);
			filledPercentSum += shelf.FilledPercent;
		}

		if (candidates.Count <= 0)
			return false;

		float averageFilledPercent = filledPercentSum / candidates.Count;
		ShelfBase best = null;
		int bestDist = int.MaxValue;
		int bestQuantity = 0;

		for (int i = 0; i < candidates.Count; ++i)
		{
			ShelfBase shelf = candidates[i];
			if (shelf.FilledPercent > averageFilledPercent)
			{
				continue;
			}

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				shelf,
				InteractionKind.Put,
				workerPos,
				workerBuildingId,
				out _,
				out int dist) == false)
				continue;

			if (dist < bestDist)
			{
				bestDist = dist;
				best = shelf;
				bestQuantity = shelf.GetAcceptableQuantity(itemId, requestedQuantity);
			}
		}

		if (best == null || bestQuantity <= 0)
			return false;

		decision.shelf = best;
		decision.ItemID = itemId;
		decision.Quantity = bestQuantity;
		return true;
	}
}
