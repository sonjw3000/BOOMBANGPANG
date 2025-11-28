using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class OrderAllocator
{
	static private int jobID = 1;

	protected PickingTask.PickJob CreateNewPickJob()
	{
		PickingTask.PickJob pickJob = new PickingTask.PickJob();
		pickJob.JobID = jobID++;
		pickJob.CurrentLine = 0;
		pickJob.Lines = new List<PickingTask.PickingLine>();
		return pickJob;
	}

	public abstract PickingTask BuildPickingTask(OrderManager manager);
}

// 테스트용 picking 태스크 생성기
public class TestingOrderAllocator : OrderAllocator
{
	public override PickingTask BuildPickingTask(OrderManager manager)
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
			PickingTask.PickingLine pickLine = new PickingTask.PickingLine();
			if (GameContext.Instance.ItemInventoryData.GetClosestItemLocation(itemId, new int3(1, 1, 1), out int3 location) == false)
			{
				Debug.Log("Cannot find item location for item ID: " + itemId);
				break;
			}
			pickLine.GoalPosition = location;
			pickLine.ItemID = itemId;

			foreach (var orderLine in manager.GetOrderLine(itemId))
			{
				// todo
				// 추후 weight 계산 로직 필요
				// weight가 capacity를 초과하면 루프 탈출 후 새로운 피킹 태스크를 생성해야 함

				// 현재는 단순히 피킹라인으로 변환한다
				pickLine.Quantity += orderLine.Quantity;
			}

			pickJob.Lines.Add(pickLine);
		}

		// 비어있는 큐 클리어
		manager.ClearEmptyQueues();
		PickingTask task = new PickingTask(pickJob);

		return task;
	}
}

// batch picking 태스크 생성기
public class BatchOrderAllocator : OrderAllocator
{
	public override PickingTask BuildPickingTask(OrderManager manager)
	{

		return null;
	}
}

// zone picking 태스크 생성기
public class ZoneOrderAllocator : OrderAllocator
{
	public override PickingTask BuildPickingTask(OrderManager manager)
	{

		return null;
	}
}

// wave picking 태스크 생성기
public class WaveOrderAllocator : OrderAllocator
{
	public override PickingTask BuildPickingTask(OrderManager manager)
	{

		return null;
	}
}

