using System.Collections.Generic;
using UnityEngine;

public sealed class CollectPlanner<TRequestLine>
{
	private ICollectingPolicy<TRequestLine> collectingPolicy;
	private readonly ICollectSupplySource collectSupplySource;
	private readonly ICollectRequestSource<TRequestLine> collectRequestSource;

	public CollectPlanner(
		ICollectSupplySource supplier,
		ICollectRequestSource<TRequestLine> reqSource,
		ICollectingPolicy<TRequestLine> policy = null)
	{
		collectSupplySource = supplier;
		collectRequestSource = reqSource;
		collectingPolicy = policy ?? new NearestCollectingPolicy<TRequestLine>();
	}

	public void SetCollectingPolicy(ICollectingPolicy<TRequestLine> policy)
	{
		collectingPolicy = policy ?? new NearestCollectingPolicy<TRequestLine>();
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, out WorkLine line)
	{
		line = null;

		if (worker == null)
			return false;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
		{
			Debug.LogWarning("[CollectPlanner] Cannot allocate collect line without a carrying box.");
			return false;
		}

		List<CollectCandidate<TRequestLine>> candidates = BuildCandidates(box);
		while (candidates.Count > 0)
		{
			if (collectingPolicy.TryDecide(worker.GridPosition, candidates, out var decision) == false)
				return false;

			if (decision.Source == null || decision.Quantity <= 0)
			{
				RemoveCandidate(candidates, decision);
				continue;
			}

			int actualReserved = decision.Source.ReservePicking(decision.ItemId, decision.Quantity);
			if (actualReserved <= 0)
			{
				RemoveCandidate(candidates, decision);
				continue;
			}

			int actualAllocated = collectRequestSource.Allocate(decision.RequestLine, actualReserved);
			if (actualAllocated <= 0)
			{
				Debug.LogWarning($"[CollectPlanner] Request allocation rejected after reserving source. item={decision.ItemId}, reserved={actualReserved}");
				RemoveCandidate(candidates, decision);
				continue;
			}

			if (actualAllocated != actualReserved)
			{
				Debug.LogWarning($"[CollectPlanner] Reservation/allocation mismatch for item {decision.ItemId}. reserved={actualReserved}, allocated={actualAllocated}");
			}

			line = collectRequestSource.CreateWorkLine(decision.Source, decision.ItemId, actualAllocated, decision.RequestLine);
			return line != null;
		}

		return false;
	}

	private List<CollectCandidate<TRequestLine>> BuildCandidates(BoxBase box)
	{
		List<CollectCandidate<TRequestLine>> candidates = new();
		foreach (uint itemId in collectRequestSource.GetRequestedItemIds())
		{
			foreach (TRequestLine requestLine in collectRequestSource.GetRequestLines(itemId))
			{
				int allocatable = collectRequestSource.GetAllocatableQuantity(requestLine);
				if (allocatable <= 0)
					continue;

				int acceptable = box.GetAcceptableQuantity(itemId, allocatable);
				if (acceptable <= 0)
					continue;

				foreach (ShelfBase source in collectSupplySource.GetSources(itemId))
				{
					if (source == null)
						continue;

					int pickable = source.GetPickableQuantity(itemId);
					if (pickable <= 0)
						continue;

					int quantity = Mathf.Min(acceptable, pickable);
					if (quantity <= 0)
						continue;

					candidates.Add(new CollectCandidate<TRequestLine>(source, itemId, quantity, requestLine));
				}
			}
		}

		return candidates;
	}

	private static void RemoveCandidate(List<CollectCandidate<TRequestLine>> candidates, CollectCandidate<TRequestLine> decision)
	{
		for (int i = 0; i < candidates.Count; ++i)
		{
			CollectCandidate<TRequestLine> candidate = candidates[i];
			if (EqualityComparer<TRequestLine>.Default.Equals(candidate.RequestLine, decision.RequestLine) &&
				candidate.Source == decision.Source &&
				candidate.ItemId == decision.ItemId &&
				candidate.Quantity == decision.Quantity)
			{
				candidates.RemoveAt(i);
				return;
			}
		}
	}
}
