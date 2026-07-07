using System;
using System.Collections.Generic;

public partial class OrderManager
{
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
				Destination = order.Destination,
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
					PickingAllocatedQuantity = line.PickingAllocatedQuantity,
					PickingCompletedQuantity = line.PickingCompletedQuantity,
					PackagingCompletedQuantity = line.PackagingCompletedQuantity,
					WaitingForShippingQuantity = line.WaitingForShippingQuantity,
					ShippingQuantity = line.ShippingQuantity,
					InDeliveryQuantity = line.InDeliveryQuantity,
					CompletedQuantity = line.CompletedQuantity,
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
				Destination = orderData.Destination,
			};

			foreach (OrderLineSaveData lineData in orderData.Lines)
			{
				if (contractService.TryGetActiveContract(lineData.SourceContractId, out var sourceContract) == false)
					continue;

				OrderLine line = new(order, lineData.ItemId, lineData.Quantity, sourceContract);
				line.RestoreState(
					lineData.LineId,
					lineData.Status,
					lineData.StartWeek,
					lineData.DueWeek,
					lineData.BaseReward,
					lineData.DelayPenalty,
					lineData.ReputationChange,
					lineData.PickingAllocatedQuantity,
					lineData.PickingCompletedQuantity,
					lineData.PackagingCompletedQuantity,
					lineData.WaitingForShippingQuantity,
					lineData.ShippingQuantity,
					lineData.InDeliveryQuantity,
					lineData.CompletedQuantity);
				order.Lines.Add(line);
				restoredLines[lineData.LineId] = line;
				RegisterOrderLineForPicking(line);
			}

			order.RecalculateStatus();
			orders.Add(order);
			orderStatus[order.Status].AddLast(order);
		}

		OrderFactory.SetNextOrderId(data.NextOrderId);
	}

	public void ResetRuntimeState()
	{
		orders.Clear();
		itemOrderLines.Clear();
		orderStatus.Clear();
		foreach (OrderTotalStatus status in Enum.GetValues(typeof(OrderTotalStatus)))
		{
			orderStatus[status] = new();
		}
	}
}
