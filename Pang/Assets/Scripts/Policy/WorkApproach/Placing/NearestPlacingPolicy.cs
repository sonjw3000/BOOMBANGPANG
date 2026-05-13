using UnityEngine;
using Unity.Mathematics;

public class NearestPlacingPolicy : IPlacingPolicy
{
	private ShelfStorageIndex StorageIndex => GameContext.Instance.StorageIndex;
	//private readonly ShelfStorageIndex 

	public bool TryDecide(in int3 workerPos, BoxBase box, out PlaceDecision decision)
	{
		decision = default;

		if (box == null || box.Stacks.Count == 0)
		{
			Debug.LogWarning("Cannot decide placing target without a carried box.");
			return false;
		}

		ShelfBase best = null;
		int bestDist = int.MaxValue;

		// todo
		// 가장 많은 용량을 먼저 해치우고싶은데
		ItemStack bestStack = box.Stacks[0];

		int quantity = 0;

		foreach (var shelf in StorageIndex.QueryPlaceCandidate(bestStack.ItemID, bestStack.Quantity))
		{
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
			// todo
			// 여분 shelf가 없다는 것을 유저에게 알려주어야 함
			Debug.Log("No suitable shelf found for placing.");
			return false;
		}

		decision.shelf = best;
		decision.ItemID = bestStack.ItemID;
		decision.Quantity = quantity;

		//Debug.Log($"bsetShelfPos: {best.GridPosition}, ItemID: {bestStack.ItemID}, Quantity: {quantity}");

		return best != null;
	}
}
