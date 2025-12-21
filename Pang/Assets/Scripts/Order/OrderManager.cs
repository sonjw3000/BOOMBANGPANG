using System.Collections.Generic;
using UnityEngine;

// OrderManager
// OrderManager는 주문을 생성하고 관리한다
// 유저의 입력에 따라피킹 알고리즘의 변화가 있을 수 있기 때문에 Allocator는 별도의 클래스로 분리한다
// OrderManager 주문을 생성하고(랜덤으로) 이를 피킹태스크로 변환해야한다
// PickingTask.PickingLine을 생성하고 이를 지역별로 묶는다
// 묶인 PickingLine을 PickingTask.PickJob으로 변환한다
// PickJob을 PickingTask로 변환한다
// PickingTask를 TaskManager에 등록한다

public class OrderManager : MonoBehaviour
{
	// 실제 주문 목록
	private List<Order> orders = new();

	// itemID로 주문을 빠르게 찾기 위한 맵핑
	// PickingTask를 만들 때 사용되고 난 후에 큐에서 제거됨
	private Dictionary<uint, Queue<OrderLine>> itemOrderLines = new();

	public IReadOnlyCollection<Order> Orders => orders;
	public IReadOnlyDictionary<uint, Queue<OrderLine>> ItemOrderLines => itemOrderLines;


	public void CreateRandomOrder()
	{
		var order = OrderFactory.CreateRandomOrder();

		orders.Add(order);

		// convert order to OrderLines
		foreach (var line in order.Lines)
		{
			if (!itemOrderLines.ContainsKey(line.ItemID))
			{
				itemOrderLines[line.ItemID] = new Queue<OrderLine>();
			}

			itemOrderLines[line.ItemID].Enqueue(line);
		}
	}

	public IEnumerable<uint> GetAllOrderedItemIDs()
	{
		foreach (var kvp in itemOrderLines)
		{
			if (kvp.Value.Count > 0)
			{
				yield return kvp.Key;
			}
		}
	}

	public IEnumerable<OrderLine> GetOrderLine(uint itemID)
	{
		if (!itemOrderLines.ContainsKey(itemID))
		{
			yield break;
		}

		while (itemOrderLines[itemID].Count > 0)
		{
			yield return itemOrderLines[itemID].Dequeue();
		}
	}

	public void ClearEmptyQueues()
	{
		var keysToRemove = new List<uint>();
		foreach (var kvp in itemOrderLines)
		{
			if (kvp.Value.Count == 0)
			{
				keysToRemove.Add(kvp.Key);
			}
		}

		foreach (var key in keysToRemove)
		{
			itemOrderLines.Remove(key);
		}
	}

}

