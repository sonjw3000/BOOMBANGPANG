using System;
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
	private Dictionary<OrderTotalStatus, LinkedList<Order>> orderStatus = new();

	// itemID로 주문을 빠르게 찾기 위한 맵핑
	// PickingTask를 만들 때 사용되고 난 후에 큐에서 제거됨
	private Dictionary<uint, Queue<OrderLine>> itemOrderLines = new();

	public IReadOnlyCollection<Order> Orders => orders;
	public IReadOnlyDictionary<uint, Queue<OrderLine>> ItemOrderLines => itemOrderLines;
	public IReadOnlyDictionary<OrderTotalStatus, LinkedList<Order>> OrderStatusMap => orderStatus;
private void Start()
	{
		foreach (OrderTotalStatus status in Enum.GetValues(typeof(OrderTotalStatus)))
		{
			orderStatus[status] = new();
		}
	}

	public void CreateRandomOrder()
	{
		var newOrders = OrderFactory.CreateOrdersFromContracts();

		foreach (var order in newOrders)
		{
			orders.Add(order);
			orderStatus[OrderTotalStatus.Pending].AddLast(order);
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

	public void ChangeOrderStatus(OrderLine targetOrder, OrderStatus status)
	{
		var befStatus = targetOrder.ParentOrder.Status;
		var afterStatus = targetOrder.ChangeOrderStatus(status);

		// ContractRuntime에 결과 기록
		if (status == OrderStatus.Completed)
		{
			int currentWeek = GameContext.Instance.GameTime.WeeksPassed;
			var contractStatus = currentWeek > targetOrder.DueWeek ? Assets.Scripts.Contract.Status.Delayed : Assets.Scripts.Contract.Status.Success;
			targetOrder.SourceContract.AddResult(contractStatus, 1);
		}
		else if (status == OrderStatus.Cancelled)
		{
			targetOrder.SourceContract.AddResult(Assets.Scripts.Contract.Status.Failed, 1);
		}

		if (befStatus != afterStatus)
		{
			var parent = targetOrder.ParentOrder;
			orderStatus[befStatus].Remove(parent);
			orderStatus[afterStatus].AddLast(parent);

			if (afterStatus == OrderTotalStatus.Completed)
			{
				SettleOrder(parent);
			}
		}
	}

	public void CheckExpiredOrders()
	{
		int currentWeek = GameContext.Instance.GameTime.WeeksPassed;
		List<OrderLine> linesToCancel = new();

		// Pending이나 InProgress인 주문의 라인들 확인
		foreach (var status in new[] { OrderTotalStatus.Pending, OrderTotalStatus.InProgress })
		{
			foreach (var order in orderStatus[status])
			{
				foreach (var line in order.Lines)
				{
					if (line.Status == OrderStatus.Completed || line.Status == OrderStatus.Cancelled)
						continue;

					// 마감 기한으로부터 2주 이상 지났을 경우
					if (currentWeek > line.DueWeek + 2)
					{
						// 30% 확률로 주문 취소
						if (UnityEngine.Random.value < 0.3f)
						{
							// Cancel은 일시적으로 막아둠
							//linesToCancel.Add(line);
						}
					}
				}
			}
		}

		foreach (var line in linesToCancel)
		{
			Debug.Log($"OrderLine in {line.ParentOrder.OrderID} is cancelled due to extreme delay.");
			ChangeOrderStatus(line, OrderStatus.Cancelled);
		}
	}

	private void SettleOrder(Order order)
	{
		int totalItemRevenue = 0;
		int totalBonusReward = 0;
		float totalReputationDelta = 0;

		var itemDB = GameContext.Instance.ItemDB;
		int currentWeek = GameContext.Instance.GameTime.WeeksPassed;

		foreach (var line in order.Lines)
		{
			if (line.Status == OrderStatus.Cancelled) continue;

			// 아이템 기본 수익
			if (itemDB.GetItemData(line.ItemID, out var data))
			{
				totalItemRevenue += data.Price * line.Quantity;
			}

			// 계약 보상 및 패널티
			bool isDelayed = currentWeek > line.DueWeek;
			int bonus = line.BaseReward;
			float rep = line.ReputationChange;

			if (isDelayed)
			{
				bonus -= line.DelayPenalty;
				rep *= 0.2f;
			}

			totalBonusReward += bonus;
			totalReputationDelta += rep;
		}

		var transaction = new EconomyTransaction
		{
			moneyDelta = totalItemRevenue + totalBonusReward,
			reputationDelta = totalReputationDelta,
			reason = EconomyTransaction.Reason.OrderSettlement
		};

		GameContext.Instance.EconomyService.ApplyTransaction(transaction);
		Debug.Log($"Order {order.OrderID} settled. Revenue: {totalItemRevenue + totalBonusReward}, Rep: {totalReputationDelta}");
	}

	public OrderManagerSaveData CaptureState(Func<OrderLine, int> registerOrderLine)
	{
		OrderManagerSaveData data = new()
		{
			NextOrderId = OrderFactory.NextOrderId,
		};

		foreach (Order order in orders)
		{
			OrderSaveData orderData = new()
			{
				OrderId = order.OrderID,
				Status = order.Status,
			};

			foreach (OrderLine line in order.Lines)
			{
				int lineId = registerOrderLine != null ? registerOrderLine(line) : line.SaveId;
				orderData.Lines.Add(new OrderLineSaveData
				{
					LineId = lineId,
					ItemId = line.ItemID,
					Quantity = line.Quantity,
					Status = line.Status,
					SourceContractId = line.SourceContract.Definition.ContractId,
					StartWeek = line.StartWeek,
					DueWeek = line.DueWeek,
					BaseReward = line.BaseReward,
					DelayPenalty = line.DelayPenalty,
					ReputationChange = line.ReputationChange,
				});
			}

			data.Orders.Add(orderData);
		}

		return data;
	}

	public void RestoreState(OrderManagerSaveData data, ContractService contractService, Dictionary<int, OrderLine> restoredLines)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (OrderSaveData orderData in data.Orders)
		{
			Order order = new Order
			{
				OrderID = orderData.OrderId,
				Lines = new List<OrderLine>(),
			};

			foreach (OrderLineSaveData lineData in orderData.Lines)
			{
				if (contractService.TryGetActiveContract(lineData.SourceContractId, out var sourceContract) == false)
					continue;

				OrderLine line = new(order, lineData.ItemId, lineData.Quantity, sourceContract);
				line.RestoreState(lineData.LineId, lineData.Status, lineData.StartWeek, lineData.DueWeek, lineData.BaseReward, lineData.DelayPenalty, lineData.ReputationChange);
				order.Lines.Add(line);
				restoredLines[lineData.LineId] = line;
			}

			order.RestoreStatus(orderData.Status);
			orders.Add(order);
			orderStatus[order.Status].AddLast(order);
		}

		foreach (Order order in orders)
		{
			foreach (OrderLine line in order.Lines)
			{
				if (line.Status == OrderStatus.Pending || line.Status == OrderStatus.Allocated)
				{
					if (itemOrderLines.ContainsKey(line.ItemID) == false)
						itemOrderLines[line.ItemID] = new Queue<OrderLine>();

					itemOrderLines[line.ItemID].Enqueue(line);
				}
			}
		}

		OrderFactory.SetNextOrderId(data.NextOrderId);
	}

	public void ResetRuntimeState()
	{
		orders.Clear();
		itemOrderLines.Clear();
		orderStatus.Clear();
		foreach (OrderTotalStatus status in Enum.GetValues(typeof(OrderTotalStatus)))
			orderStatus[status] = new();
	}

}
