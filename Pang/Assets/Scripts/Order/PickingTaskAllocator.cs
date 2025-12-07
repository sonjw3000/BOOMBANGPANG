using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class PickingTaskAllocator
{
	static private int jobID = 1;

	protected OrderManager manager => GameContext.Instance.OrderMgr;
	protected ItemInventory itemInv => GameContext.Instance.ItemInventoryData;
	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	protected PickingTask.PickJob CreateNewPickJob()
	{
		PickingTask.PickJob pickJob = new PickingTask.PickJob(jobID++);
		return pickJob;
	}

	public abstract PickingTask BuildPickingTask();
}

// 테스트용 picking 태스크 생성기
public class TestingPickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{
		if (manager.ItemOrderLines.Count <= 0)
		{
			Debug.Log("No orders to allocate.");
			return null;
		}
		
		// 테스트용으로 단순히 모든 오더를 하나의 피킹 태스크로 만든다
		// 실제 구현에서는 다양한 로직이 들어갈 수 있다
		PickingTask.PickJob pickJob = CreateNewPickJob();

		float curWeight = 0f;

		// 오더라인을 순회하며 피킹라인 생성
		foreach (uint itemId in manager.GetAllOrderedItemIDs())
		{
			//PickingTask.PickingLine pickLine = new PickingTask.PickingLine();
			if (itemInv.GetClosestItemLocation(itemId, new int3(1, 1, 1), out ItemLocation location) == false)
			{
				Debug.Log("Cannot find item location for item ID: " + itemId);
				break;
			}
			int quantity = 0;
			foreach (var orderLine in manager.GetOrderLine(itemId))
			{
				// todo
				// 추후 weight 계산 로직 필요
				// weight가 capacity를 초과하면 루프 탈출 후 새로운 피킹 태스크를 생성해야 함

				// todo
				// tobeQuantity를 고려하여 모두 피킹했다면 쳐내라

				// 현재는 단순히 피킹라인으로 변환한다
				//pickLine.Quantity += orderLine.Quantity;
				curWeight += orderLine.Quantity * itemDB.GetItemWeight(itemId);
				quantity += orderLine.Quantity;
				location.ReservePicking(orderLine.Quantity);
			}

			pickJob.AddLine(location, quantity);
		}

		// 비어있는 큐 클리어
		manager.ClearEmptyQueues();
		PickingTask task = new PickingTask(pickJob);

		return task;
	}
}

// batch picking 태스크 생성기
public class BatchPickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{

		return null;
	}
}

// zone picking 태스크 생성기
public class ZonePickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{

		return null;
	}
}

// wave picking 태스크 생성기
public class WavePickingTaskAllocator : PickingTaskAllocator
{
	public override PickingTask BuildPickingTask()
	{

		return null;
	}
}

