using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class PickingTaskAllocator
{
	static protected int jobID = 1;
	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;

	static protected float MaxCarryWeight => GameContext.Instance.WMSys.BoxPoolMgr.ToteCapacity;

	protected OrderManager manager => GameContext.Instance.OrderMgr;
	protected ShelfStorageIndex itemInv => GameContext.Instance.StorageIndex;
	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	public abstract PickingTask BuildPickingTask();
}

public class TestingPickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{
		List<WorkLine> lines = new();
		float curWeight = 0f;

		foreach (uint itemId in manager.GetAllOrderedItemIDs())
		{
			if (itemInv.GetItemLocations(itemId, out List<ShelfBase> locations) == false || locations.Count <= 0)
			{
				Debug.Log($"Cannot find item location for item ID: {itemId}");
				continue;
			}

			float itemSize = itemDB.GetItemSize(itemId);
			if (itemSize <= 0.0f)
			{
				Debug.LogWarning($"[PickingAllocator] Invalid item size for item ID: {itemId}");
				continue;
			}

			foreach (OrderLine orderLine in manager.GetOrderLine(itemId))
			{
				int remainingNeeded = orderLine.GetPickingAllocatableQuantity();
				if (remainingNeeded <= 0)
					continue;

				int remainingCapacity = Mathf.FloorToInt((MaxCarryWeight - curWeight) / itemSize);
				if (remainingCapacity <= 0)
					break;

				int requestedQuantity = Mathf.Min(remainingNeeded, remainingCapacity);
				for (int i = 0; i < locations.Count && requestedQuantity > 0; ++i)
				{
					ShelfBase location = locations[i];
					if (location == null)
						continue;

					int pickable = location.GetPickableQuantity(itemId);
					if (pickable <= 0)
						continue;

					int reserveRequest = Mathf.Min(requestedQuantity, pickable);
					int actualReserved = location.ReservePicking(itemId, reserveRequest);
					if (actualReserved <= 0)
						continue;

					int actualAllocated = manager.AllocatePicking(orderLine, actualReserved);
					if (actualAllocated <= 0)
					{
						Debug.LogWarning($"[PickingAllocator] Allocated quantity rejected for item ID: {itemId}");
						continue;
					}

					if (actualAllocated != actualReserved)
					{
						Debug.LogWarning($"[PickingAllocator] Reservation/allocation mismatch for item ID: {itemId}. reserved={actualReserved}, allocated={actualAllocated}");
					}

					lines.Add(new WorkLine(location, itemId, actualAllocated, orderLine));
					curWeight += actualAllocated * itemSize;
					requestedQuantity -= actualAllocated;
				}

				if (curWeight >= MaxCarryWeight)
					break;
			}

			if (curWeight >= MaxCarryWeight)
				break;
		}

		manager.ClearEmptyQueues();

		if (lines.Count <= 0)
			return null;

		return new PickingTask(new WorkJob(jobID++, lines, WorkOp.Picking));
	}
}

public class BatchPickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{
		return null;
	}
}

public class ZonePickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{
		return null;
	}
}

public class WavePickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{
		return null;
	}
}
