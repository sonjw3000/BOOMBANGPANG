

// 전략들
// 1. cargo친화
//		하나의 cargo의 물품을 최대한 담아서 shelf에 저장
// 2. 물품 친화
//		cargo들을 돌며 최대한 같은 종류의 물품을 박스에 담아 shelf에 저장
// 3. 클러스터링 (구현 난이도 UP, 매우 후순위)
//		특정 아이템을 기준으로 가까운 shelf에 위치한 아이템들을 최대한 모아서 이동

using System.Collections.Generic;
using UnityEngine;


public abstract class StoringPlanner
{
	static protected int jobID = 1;

	protected InboundWorkflowManager IBManager => GameContext.Instance.IBWorkflowMgr;
	static protected float MaxCarryWeight => GameContext.Instance.WMSys.BoxPoolMgr.ToteCapacity;
	static protected float MaximumBoxPercentage => 80.0f;//GameContext.Instance.IBWorkflowMgr.maxBoxPercentage;
	static protected float MaximumBoxWeight => MaxCarryWeight * MaximumBoxPercentage / 100.0f;
	static protected ItemDatabase ItemDB => GameContext.Instance.ItemDB;

	// event handlers
	public virtual void OnPortItemAdded(ShelfBase port, uint itemID) { }
	public virtual void OnPortItemRemoved(ShelfBase port, uint itemID) { }
	public virtual void OnPortItemQuantityChanged(ShelfBase port, uint itemID, int quantity) { }
	public virtual void OnPortItemReserved(ShelfBase port, uint itemID, int reservedQuantity) { }

	public abstract bool BuildStoreTask(out StoringTask task);
	public abstract bool CanBuildFullTask();

	protected int AdjustQuantityToFit(float curWeight, float itemWeight, int befQuantity)
	{
		if (curWeight + itemWeight * befQuantity <= MaximumBoxWeight)
		{
			return befQuantity;
		}
		else
		{
			int newQuantity = Mathf.FloorToInt((MaximumBoxWeight - curWeight) / itemWeight);
			return newQuantity;
		}
	}
}

// store by item Id
// 가장 많은 같은 종류의 아이템을 담아서 저장하는 전략
public sealed class StoringItemFriendly : StoringPlanner
{
	private Dictionary<uint, int> itemQuantityCanPick = new();

	private bool GetBestFit(out uint bestFitID)
	{
		bestFitID = 0;
		int bestFit = 0;

		if (itemQuantityCanPick.Count <= 0)
			return false;

		foreach (var kv in itemQuantityCanPick)
		{
			int c = kv.Value;
			if (c > bestFit)
			{
				bestFit = c;
				bestFitID = kv.Key;
			}
		}

		return true;
	}

	public override void OnPortItemQuantityChanged(ShelfBase port, uint itemID, int quantity)
	{
		if (quantity > 0)
		{
			itemQuantityCanPick[itemID] = itemQuantityCanPick.GetValueOrDefault(itemID, 0) + quantity;
		}
	}

	public override void OnPortItemReserved(ShelfBase port, uint itemID, int reservedQuantity)
	{
		itemQuantityCanPick[itemID] = itemQuantityCanPick.GetValueOrDefault(itemID, 0) - reservedQuantity;

		if (itemQuantityCanPick[itemID] < 0)
		{
			// why minus??
			UnityEngine.Debug.LogError($"Reserved quantity for item {itemID} is greater than available quantity. Check the reservation logic.");
			itemQuantityCanPick[itemID] = 0;
		}

		if (itemQuantityCanPick[itemID] == 0)
		{
			itemQuantityCanPick.Remove(itemID);
		}
	}

	public override bool BuildStoreTask(out StoringTask task)
	{
		task = null;

		// 더이상 task를 만들 line이 없으면 return false
		if (CanBuildFullTask() == false)
			return false;

		// boxPercentage에 의해 job의 Line을 제한한다
		float curWeight = 0;

		List<WorkLine> line = new();

		bool boxFull = false;

		while (boxFull == false && GetBestFit(out var itemID))
		{
			var cargoPorts = IBManager.CargoPortsByItem.GetValueOrDefault(itemID, new List<CargoPort>());

			if (cargoPorts.Count <= 0)
				break;
			
			// find the most quantity port
			CargoPort mostFitCargo = cargoPorts[0];
			int max = mostFitCargo.GetPickableQuantity(itemID);

			foreach (var port in cargoPorts)
			{
				int cnt = port.GetPickableQuantity(itemID);
				if (cnt > max)
				{
					max = cnt;
					mostFitCargo = port;
				}
			}

			// pq로 하고싶은데 어케 방법이 없을까 그냥 이대로 할게
			float itemWeight = ItemDB.GetItemSize(itemID);
			while (mostFitCargo != null)
			{
				int pickable = mostFitCargo.GetPickableQuantity(itemID);
				int quantityCanPick = AdjustQuantityToFit(curWeight, itemWeight, pickable);

				curWeight += quantityCanPick * itemWeight;
				mostFitCargo.ReservePicking(itemID, quantityCanPick);

				line.Add(new(mostFitCargo, itemID, quantityCanPick));

				if (pickable != quantityCanPick)
				{
					// box is full
					boxFull = true;
					break;
				}

				mostFitCargo = GridStatic.GetClosestPlaceable(mostFitCargo.GridPosition, IBManager.CargoPortsByItem[itemID], (IGridPlaceable placeable) =>
				{
					var port = placeable as CargoPort;
					return port.GetPickableQuantity(itemID) > 0;
				}) as CargoPort;
			}
		}

		if (line.Count <= 0)
			return false;

		WorkJob job = new WorkJob(jobID++, line, WorkOp.Storing);
		task = new StoringTask(job);

		return true;
	}

	public override bool CanBuildFullTask()
	{
		foreach (var kv in itemQuantityCanPick)
		{
			float itemWeight = ItemDB.GetItemSize(kv.Key);
			float totalWeight = itemWeight * kv.Value;

			if (totalWeight >= MaximumBoxWeight)
			{
				return true;
			}
		}

		return false;
	}
}

