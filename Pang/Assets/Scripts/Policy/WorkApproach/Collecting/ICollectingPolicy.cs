using System.Collections.Generic;
using Unity.Mathematics;

public readonly struct CollectCandidate<TRequestLine>
{
	public readonly ShelfBase Source;
	public readonly uint ItemId;
	public readonly int Quantity;
	public readonly TRequestLine RequestLine;

	public CollectCandidate(ShelfBase source, uint itemId, int quantity, TRequestLine requestLine)
	{
		Source = source;
		ItemId = itemId;
		Quantity = quantity;
		RequestLine = requestLine;
	}
}

public interface ICollectingPolicy<TRequestLine>
{
	bool TryDecide(in int3 workerPos, IReadOnlyList<CollectCandidate<TRequestLine>> candidates, out CollectCandidate<TRequestLine> decision);
}

public sealed class NearestCollectingPolicy<TRequestLine> : ICollectingPolicy<TRequestLine>
{
	public bool TryDecide(in int3 workerPos, IReadOnlyList<CollectCandidate<TRequestLine>> candidates, out CollectCandidate<TRequestLine> decision)
	{
		decision = default;
		if (candidates == null || candidates.Count <= 0)
			return false;

		int bestDist = int.MaxValue;
		int bestIndex = -1;

		for (int i = 0; i < candidates.Count; ++i)
		{
			ShelfBase source = candidates[i].Source;
			if (source == null)
				continue;

			int3 interactionPos = source.GetClosestInteractionPoint(InteractionKind.Pick, workerPos);
			int dist =
				math.abs(workerPos.x - interactionPos.x) +
				math.abs(workerPos.y - interactionPos.y) +
				math.abs(workerPos.z - interactionPos.z);

			if (dist < bestDist)
			{
				bestDist = dist;
				bestIndex = i;
			}
		}

		if (bestIndex < 0)
			return false;

		decision = candidates[bestIndex];
		return true;
	}
}

public sealed class LargestQuantityNearestCollectingPolicy<TRequestLine> : ICollectingPolicy<TRequestLine>
{
	public bool TryDecide(in int3 workerPos, IReadOnlyList<CollectCandidate<TRequestLine>> candidates, out CollectCandidate<TRequestLine> decision)
	{
		decision = default;
		if (candidates == null || candidates.Count <= 0)
			return false;

		int bestQuantity = -1;
		int bestDist = int.MaxValue;
		int bestIndex = -1;

		for (int i = 0; i < candidates.Count; ++i)
		{
			ShelfBase source = candidates[i].Source;
			if (source == null)
				continue;

			int quantity = candidates[i].Quantity;
			int3 interactionPos = source.GetClosestInteractionPoint(InteractionKind.Pick, workerPos);
			int dist =
				math.abs(workerPos.x - interactionPos.x) +
				math.abs(workerPos.y - interactionPos.y) +
				math.abs(workerPos.z - interactionPos.z);

			if (quantity > bestQuantity || (quantity == bestQuantity && dist < bestDist))
			{
				bestQuantity = quantity;
				bestDist = dist;
				bestIndex = i;
			}
		}

		if (bestIndex < 0)
			return false;

		decision = candidates[bestIndex];
		return true;
	}
}
