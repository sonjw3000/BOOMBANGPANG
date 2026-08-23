using System;
using System.Collections.Generic;

namespace UniverseLogistics.UI.Toolkit
{
	public enum OrderUrgency
	{
		None,
		Normal,
		DueSoon,
		DueThisWeek,
		Delayed,
	}

	public readonly struct OrderStageQuantities
	{
		public int Pending { get; }
		public int Allocated { get; }
		public int Picked { get; }
		public int Packed { get; }
		public int AtPort { get; }
		public int Shipping { get; }
		public int InDelivery { get; }
		public int Completed { get; }

		public int Total => Pending + Allocated + Picked + Packed + AtPort + Shipping + InDelivery + Completed;

		public OrderStageQuantities(
			int pending,
			int allocated,
			int picked,
			int packed,
			int atPort,
			int shipping,
			int inDelivery,
			int completed)
		{
			Pending = pending;
			Allocated = allocated;
			Picked = picked;
			Packed = packed;
			AtPort = atPort;
			Shipping = shipping;
			InDelivery = inDelivery;
			Completed = completed;
		}
	}

	public readonly struct OrderRepresentativeItem
	{
		public bool HasValue { get; }
		public uint ItemId { get; }
		public string ItemName { get; }
		public int Quantity { get; }
		public int AdditionalItemTypeCount { get; }

		public OrderRepresentativeItem(
			bool hasValue,
			uint itemId,
			string itemName,
			int quantity,
			int additionalItemTypeCount)
		{
			HasValue = hasValue;
			ItemId = itemId;
			ItemName = itemName;
			Quantity = quantity;
			AdditionalItemTypeCount = additionalItemTypeCount;
		}
	}

	public static class OrderPresentation
	{
		public const int DueSoonWeeks = 2;

		public static bool IsActive(Order order)
		{
			return order != null &&
				(order.Status == OrderTotalStatus.Pending || order.Status == OrderTotalStatus.InProgress);
		}

		public static OrderUrgency GetUrgency(Order order, int currentWeek)
		{
			if (IsActive(order) == false)
				return OrderUrgency.None;

			int weeksLeft = GetWeeksLeft(order, currentWeek);
			if (weeksLeft < 0)
				return OrderUrgency.Delayed;
			if (weeksLeft == 0)
				return OrderUrgency.DueThisWeek;
			if (weeksLeft <= DueSoonWeeks)
				return OrderUrgency.DueSoon;
			return OrderUrgency.Normal;
		}

		public static int CompareByUrgency(Order left, Order right, int currentWeek)
		{
			if (ReferenceEquals(left, right))
				return 0;
			if (left == null)
				return 1;
			if (right == null)
				return -1;

			int dueCompare = GetWeeksLeft(left, currentWeek).CompareTo(GetWeeksLeft(right, currentWeek));
			return dueCompare != 0 ? dueCompare : left.OrderID.CompareTo(right.OrderID);
		}

		public static int GetWeeksLeft(Order order, int currentWeek)
		{
			int dueWeek = int.MaxValue;
			if (order?.Lines != null)
			{
				foreach (OrderLine line in order.Lines)
				{
					if (line == null || line.Status == OrderStatus.Cancelled || (IsActive(order) && line.IsFinal))
						continue;
					dueWeek = Math.Min(dueWeek, line.DueWeek);
				}
			}

			return dueWeek == int.MaxValue ? 0 : dueWeek - currentWeek;
		}

		public static int GetRequestedQuantity(Order order)
		{
			int total = 0;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.Status != OrderStatus.Cancelled)
					total += line.Quantity;
			}
			return total;
		}

		public static int GetCompletedQuantity(Order order)
		{
			int total = 0;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.Status != OrderStatus.Cancelled)
					total += line.CompletedQuantity;
			}
			return total;
		}

		public static OrderStageQuantities GetExclusiveStageQuantities(Order order)
		{
			int pending = 0;
			int allocated = 0;
			int picked = 0;
			int packed = 0;
			int atPort = 0;
			int shipping = 0;
			int inDelivery = 0;
			int completed = 0;

			if (order?.Lines != null)
			{
				foreach (OrderLine line in order.Lines)
				{
					if (line == null || line.Status == OrderStatus.Cancelled)
						continue;

					pending += Math.Max(0, line.Quantity - line.PickingCompletedQuantity - line.PickingAllocatedQuantity);
					allocated += Math.Max(0, line.PickingAllocatedQuantity);
					picked += Math.Max(0, line.PickingCompletedQuantity - line.PackagingCompletedQuantity);
					packed += Math.Max(0, line.PackagingCompletedQuantity - line.WaitingForShippingQuantity);
					atPort += Math.Max(0, line.WaitingForShippingQuantity - line.ShippingQuantity);
					shipping += Math.Max(0, line.ShippingQuantity - line.InDeliveryQuantity);
					inDelivery += Math.Max(0, line.InDeliveryQuantity - line.CompletedQuantity);
					completed += Math.Max(0, line.CompletedQuantity);
				}
			}

			return new OrderStageQuantities(
				pending,
				allocated,
				picked,
				packed,
				atPort,
				shipping,
				inDelivery,
				completed);
		}

		public static string BuildStageSummary(Order order)
		{
			int allocated = 0;
			int picked = 0;
			int packed = 0;
			int port = 0;
			int shipping = 0;
			int delivery = 0;
			int done = 0;

			if (order?.Lines != null)
			{
				foreach (OrderLine line in order.Lines)
				{
					if (line == null || line.Status == OrderStatus.Cancelled)
						continue;
					allocated += line.PickingAllocatedQuantity;
					picked += line.PickingCompletedQuantity;
					packed += line.PackagingCompletedQuantity;
					port += line.WaitingForShippingQuantity;
					shipping += line.ShippingQuantity;
					delivery += line.InDeliveryQuantity;
					done += line.CompletedQuantity;
				}
			}

			List<string> parts = new();
			AddStage(parts, "Alloc", allocated);
			AddStage(parts, "Pick", picked);
			AddStage(parts, "Pack", packed);
			AddStage(parts, "Port", port);
			AddStage(parts, "Ship", shipping);
			AddStage(parts, "Flight", delivery);
			AddStage(parts, "Done", done);
			return parts.Count > 0 ? string.Join(" · ", parts) : "Pending";
		}

		public static string BuildLineStageSummary(OrderLine line)
		{
			if (line == null)
				return "Pending 0/0";

			List<string> parts = new();
			AddStage(parts, "Alloc", line.PickingAllocatedQuantity);
			AddStage(parts, "Pick", line.PickingCompletedQuantity);
			AddStage(parts, "Pack", line.PackagingCompletedQuantity);
			AddStage(parts, "Port", line.WaitingForShippingQuantity);
			AddStage(parts, "Ship", line.ShippingQuantity);
			AddStage(parts, "Flight", line.InDeliveryQuantity);
			AddStage(parts, "Done", line.CompletedQuantity);
			return parts.Count > 0 ? string.Join(" · ", parts) : $"Pending 0/{line.Quantity}";
		}

		public static int CalculateExpectedMoney(
			Order order,
			Func<uint, ItemDefinition> resolveItem,
			int settlementWeek)
		{
			int total = 0;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line == null || line.Status == OrderStatus.Cancelled)
					continue;

				ItemDefinition item = resolveItem?.Invoke(line.ItemID);
				if (item != null)
					total += item.Price * line.Quantity;
				total += line.BaseReward - (settlementWeek > line.DueWeek ? line.DelayPenalty : 0);
			}
			return total;
		}

		public static int CalculateOnTimeMoney(Order order, Func<uint, ItemDefinition> resolveItem)
		{
			int total = 0;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line == null || line.Status == OrderStatus.Cancelled)
					continue;
				ItemDefinition item = resolveItem?.Invoke(line.ItemID);
				if (item != null)
					total += item.Price * line.Quantity;
				total += line.BaseReward;
			}
			return total;
		}

		public static int CalculateDelayMoneyLossAtWeek(Order order, int settlementWeek)
		{
			int total = 0;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.Status != OrderStatus.Cancelled && settlementWeek > line.DueWeek)
					total += line.DelayPenalty;
			}
			return total;
		}

		public static float CalculateExpectedReputation(Order order, int settlementWeek)
		{
			float total = 0f;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line == null || line.Status == OrderStatus.Cancelled)
					continue;
				total += settlementWeek > line.DueWeek ? line.ReputationChange * 0.2f : line.ReputationChange;
			}
			return total;
		}

		public static float CalculateOnTimeReputation(Order order)
		{
			float total = 0f;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.Status != OrderStatus.Cancelled)
					total += line.ReputationChange;
			}
			return total;
		}

		public static float CalculateDelayReputationLossAtWeek(Order order, int settlementWeek)
		{
			float total = 0f;
			if (order?.Lines == null)
				return total;

			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.Status != OrderStatus.Cancelled && settlementWeek > line.DueWeek)
					total += line.ReputationChange * 0.8f;
			}
			return total;
		}

		public static OrderRepresentativeItem GetRepresentativeItem(
			Order order,
			Func<uint, ItemDefinition> resolveItem)
		{
			if (order?.Lines == null)
				return default;

			OrderLine representative = null;
			HashSet<uint> itemIds = new();
			foreach (OrderLine line in order.Lines)
			{
				if (line == null || line.Status == OrderStatus.Cancelled)
					continue;

				itemIds.Add(line.ItemID);
				if (representative == null ||
					(line.IsFinal == false && representative.IsFinal) ||
					(line.IsFinal == representative.IsFinal && line.DueWeek < representative.DueWeek))
				{
					representative = line;
				}
			}

			if (representative == null)
				return default;

			int quantity = 0;
			foreach (OrderLine line in order.Lines)
			{
				if (line != null && line.Status != OrderStatus.Cancelled && line.ItemID == representative.ItemID)
					quantity += line.Quantity;
			}

			return new OrderRepresentativeItem(
				true,
				representative.ItemID,
				GetItemName(representative.ItemID, resolveItem),
				quantity,
				Math.Max(0, itemIds.Count - 1));
		}

		public static string GetItemName(uint itemId, Func<uint, ItemDefinition> resolveItem)
		{
			ItemDefinition item = resolveItem?.Invoke(itemId);
			return item != null
				? item.name
				: $"Unknown Item {itemId}";
		}

		public static string GetContractName(OrderLine line)
		{
			if (line?.SourceContract?.Definition == null)
				return "Unknown contract";

			return string.IsNullOrWhiteSpace(line.SourceContract.Definition.ContractName)
				? line.SourceContract.Definition.ItemToHandle != null
					? line.SourceContract.Definition.ItemToHandle.name
					: "Unnamed contract"
				: line.SourceContract.Definition.ContractName;
		}

		public static string BuildContractNames(Order order)
		{
			HashSet<string> names = new();
			if (order?.Lines != null)
			{
				foreach (OrderLine line in order.Lines)
				{
					if (line != null)
						names.Add(GetContractName(line));
				}
			}
			return names.Count > 0 ? string.Join("  ·  ", names) : "No source contract";
		}

		public static string FormatWeeksLeft(Order order, int weeksLeft)
		{
			if (order == null)
				return "—";
			if (order.Status == OrderTotalStatus.Completed)
				return "Completed";
			if (order.Status == OrderTotalStatus.Cancelled)
				return "Cancelled";
			if (weeksLeft < 0)
				return $"Delayed {-weeksLeft}w";
			if (weeksLeft == 0)
				return "Due this week";
			return $"{weeksLeft} weeks left";
		}

		public static string FormatLineDue(OrderLine line, int currentWeek)
		{
			if (line == null)
				return "—";
			if (line.Status == OrderStatus.Completed)
				return "Completed";
			if (line.Status == OrderStatus.Cancelled)
				return "Cancelled";

			int weeksLeft = line.DueWeek - currentWeek;
			return weeksLeft < 0 ? $"Delayed {-weeksLeft}w" : weeksLeft == 0 ? "Due this week" : $"{weeksLeft}w left";
		}

		public static string FormatStatus(OrderTotalStatus status)
		{
			return status == OrderTotalStatus.InProgress ? "In Progress" : status.ToString();
		}

		public static string FormatLineStatus(OrderStatus status)
		{
			return status switch
			{
				OrderStatus.WaitingForShipping => "At Port",
				OrderStatus.IndDelivery => "In Delivery",
				_ => status.ToString(),
			};
		}

		private static void AddStage(List<string> parts, string label, int quantity)
		{
			if (quantity > 0)
				parts.Add($"{label} {quantity:N0}");
		}
	}
}
