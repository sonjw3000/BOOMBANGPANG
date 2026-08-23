using System;
using System.Collections.Generic;
using UnityEngine;

public partial class OrderManager : MonoBehaviour, ICollectRequestSource<OrderLine>
{
	private readonly List<Order> orders = new();
	private readonly Dictionary<OrderTotalStatus, LinkedList<Order>> orderStatus = new();
	private readonly Dictionary<uint, List<OrderLine>> itemOrderLines = new();

	public IReadOnlyCollection<Order> Orders => orders;
	public IReadOnlyDictionary<uint, List<OrderLine>> ItemOrderLines => itemOrderLines;
	public IReadOnlyDictionary<OrderTotalStatus, LinkedList<Order>> OrderStatusMap => orderStatus;
	public event Action OnOrdersChanged;
	public event Action<Order> OnOrderSettled;

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
			order.RecalculateStatus();
			orders.Add(order);
			orderStatus[order.Status].AddLast(order);

			foreach (var line in order.Lines)
			{
				RegisterOrderLineForPicking(line);
			}
		}

		if (newOrders.Count > 0)
			OnOrdersChanged?.Invoke();
	}

	public IEnumerable<uint> GetAllOrderedItemIDs()
	{
		foreach (var kvp in itemOrderLines)
		{
			if (HasAllocatableLine(kvp.Value))
				yield return kvp.Key;
		}
	}

	public IEnumerable<OrderLine> GetOrderLine(uint itemID)
	{
		if (itemOrderLines.TryGetValue(itemID, out var lines) == false)
			yield break;

		for (int i = 0; i < lines.Count; ++i)
		{
			OrderLine line = lines[i];
			if (line != null && line.CanAllocatePicking)
				yield return line;
		}
	}

	public IEnumerable<uint> GetRequestedItemIds() => GetAllOrderedItemIDs();

	public IEnumerable<OrderLine> GetRequestLines(uint itemId) => GetOrderLine(itemId);

	public int GetAllocatableQuantity(OrderLine requestLine) => requestLine != null ? requestLine.GetPickingAllocatableQuantity() : 0;

	public void GetPendingPickingDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;

		foreach (var entry in itemOrderLines)
		{
			List<OrderLine> lines = entry.Value;
			if (lines == null)
				continue;

			for (int i = 0; i < lines.Count; ++i)
			{
				OrderLine line = lines[i];
				int quantity = line != null ? line.GetPickingAllocatableQuantity() : 0;
				if (quantity <= 0)
					continue;

				++sourceCount;
				itemQuantity += quantity;
			}
		}
	}

	public int Allocate(OrderLine requestLine, int quantity) => AllocatePicking(requestLine, quantity);

	public WorkLine CreateWorkLine(ShelfBase source, uint itemId, int quantity, OrderLine requestLine)
	{
		return source == null || requestLine == null ? null : new WorkLine(source, itemId, quantity, requestLine);
	}

	public float GetOutstandingPickingTotalSize(ItemDatabase itemDatabase)
	{
		if (itemDatabase == null)
			return 0.0f;

		float totalSize = 0.0f;
		foreach (var kvp in itemOrderLines)
		{
			float itemSize = itemDatabase.GetItemSize(kvp.Key);
			if (itemSize <= 0.0f)
				continue;

			List<OrderLine> lines = kvp.Value;
			if (lines == null)
				continue;

			for (int i = 0; i < lines.Count; ++i)
			{
				OrderLine line = lines[i];
				if (line == null)
					continue;

				int allocatable = line.GetPickingAllocatableQuantity();
				if (allocatable > 0)
					totalSize += itemSize * allocatable;
			}
		}

		return totalSize;
	}

	public void ClearEmptyQueues()
	{
		List<uint> keysToRemove = new();
		foreach (var kvp in itemOrderLines)
		{
			if (kvp.Value == null || kvp.Value.Count == 0)
				keysToRemove.Add(kvp.Key);
		}

		foreach (uint key in keysToRemove)
		{
			itemOrderLines.Remove(key);
		}
	}

	public int AllocatePicking(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.TryAllocatePicking(quantity));
	}

	public int ReleasePickingAllocation(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReleasePickingAllocation(quantity));
	}

	public int ReportPickingCompleted(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReportPickingCompleted(quantity));
	}

	public int ReportPackagingCompleted(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReportPackagingCompleted(quantity));
	}

	public int ReportWaitingForShipping(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReportWaitingForShipping(quantity));
	}

	public int ReportShipping(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReportShipping(quantity));
	}

	public int ReportInDelivery(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReportInDelivery(quantity));
	}

	public int ReportCompleted(OrderLine targetOrder, int quantity)
	{
		return ApplyLineProgress(targetOrder, () => targetOrder.ReportCompleted(quantity));
	}

	public int RollbackDestroyedCargo(
		OrderLine targetOrder,
		int pickedQuantity,
		int packedQuantity,
		PackageOutboundStage outboundStage)
	{
		return ApplyLineProgress(
			targetOrder,
			() => targetOrder.RollbackDestroyedCargo(pickedQuantity, packedQuantity, outboundStage));
	}

	public void ChangeOrderStatus(OrderLine targetOrder, OrderStatus status)
	{
		if (targetOrder == null)
			return;

		if (status != OrderStatus.Cancelled)
		{
			Debug.LogWarning($"[OrderManager] Direct status change is only supported for cancellation. Requested: {status}");
			return;
		}

		OrderStatus beforeLineStatus = targetOrder.Status;
		OrderTotalStatus beforeOrderStatus = targetOrder.ParentOrder.Status;
		targetOrder.Cancel();
		HandleLineStateChange(targetOrder, beforeLineStatus, beforeOrderStatus);
		OnOrdersChanged?.Invoke();
	}

	public void CheckExpiredOrders()
	{
		int currentWeek = GameContext.Instance.GameTime.WeeksPassed;
		List<OrderLine> linesToCancel = new();

		foreach (var status in new[] { OrderTotalStatus.Pending, OrderTotalStatus.InProgress })
		{
			foreach (var order in orderStatus[status])
			{
				foreach (var line in order.Lines)
				{
					if (line.Status == OrderStatus.Completed || line.Status == OrderStatus.Cancelled)
						continue;

					if (currentWeek > line.DueWeek + 2)
					{
						if (UnityEngine.Random.value < 0.3f)
						{
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
			if (line.Status == OrderStatus.Cancelled)
				continue;

			if (itemDB.GetItemData(line.ItemID, out var data))
			{
				totalItemRevenue += data.Price * line.Quantity;
			}

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
		OnOrderSettled?.Invoke(order);
	}

	private int ApplyLineProgress(OrderLine targetOrder, Func<int> mutator)
	{
		if (targetOrder == null || mutator == null)
			return 0;

		OrderStatus beforeLineStatus = targetOrder.Status;
		OrderTotalStatus beforeOrderStatus = targetOrder.ParentOrder.Status;
		int actual = mutator();

		if (actual > 0)
		{
			HandleLineStateChange(targetOrder, beforeLineStatus, beforeOrderStatus);
			OnOrdersChanged?.Invoke();
		}

		return actual;
	}

	private void HandleLineStateChange(OrderLine targetOrder, OrderStatus beforeLineStatus, OrderTotalStatus beforeOrderStatus)
	{
		OrderStatus afterLineStatus = targetOrder.Status;
		OrderTotalStatus afterOrderStatus = targetOrder.ParentOrder.RecalculateStatus();

		if (beforeLineStatus != afterLineStatus)
		{
			if (afterLineStatus == OrderStatus.Completed)
			{
				int currentWeek = GameContext.Instance.GameTime.WeeksPassed;
				var contractStatus = currentWeek > targetOrder.DueWeek ? Assets.Scripts.Contract.Status.Delayed : Assets.Scripts.Contract.Status.Success;
				targetOrder.SourceContract.AddResult(contractStatus, 1);
			}
			else if (afterLineStatus == OrderStatus.Cancelled)
			{
				targetOrder.SourceContract.AddResult(Assets.Scripts.Contract.Status.Failed, 1);
			}
		}

		if (beforeOrderStatus != afterOrderStatus)
		{
			LinkedList<Order> beforeList = orderStatus[beforeOrderStatus];
			beforeList.Remove(targetOrder.ParentOrder);
			orderStatus[afterOrderStatus].AddLast(targetOrder.ParentOrder);

			if (afterOrderStatus == OrderTotalStatus.Completed)
			{
				SettleOrder(targetOrder.ParentOrder);
			}
		}
	}

	private void RegisterOrderLineForPicking(OrderLine line)
	{
		if (line == null)
			return;

		if (itemOrderLines.TryGetValue(line.ItemID, out var lines) == false)
		{
			lines = new List<OrderLine>();
			itemOrderLines[line.ItemID] = lines;
		}

		if (lines.Contains(line) == false)
		{
			lines.Add(line);
		}
	}

	private static bool HasAllocatableLine(List<OrderLine> lines)
	{
		if (lines == null)
			return false;

		for (int i = 0; i < lines.Count; ++i)
		{
			if (lines[i] != null && lines[i].CanAllocatePicking)
				return true;
		}

		return false;
	}
}
