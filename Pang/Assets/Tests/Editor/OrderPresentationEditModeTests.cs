using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class OrderPresentationEditModeTests
{
	private readonly List<ItemDefinition> createdItems = new();

	[TearDown]
	public void TearDown()
	{
		for (int i = createdItems.Count - 1; i >= 0; --i)
		{
			if (createdItems[i] != null)
				Object.DestroyImmediate(createdItems[i]);
		}

		createdItems.Clear();
	}

	[Test]
	public void UrgencyAndDeadlineSorting_UseNearestUnfinishedLine()
	{
		const int currentWeek = 10;
		Order delayed = CreateOrder(40, CreateLine(4, 1, dueWeek: 9));
		Order dueThisWeek = CreateOrder(30, CreateLine(3, 1, dueWeek: 10));
		Order dueSoon = CreateOrder(
			20,
			CreateCompletedLine(1, 1, dueWeek: 4),
			CreateLine(2, 1, dueWeek: 12));
		Order normal = CreateOrder(10, CreateLine(5, 1, dueWeek: 13));

		Assert.That(OrderPresentation.GetWeeksLeft(dueSoon, currentWeek), Is.EqualTo(2));
		Assert.That(OrderPresentation.GetUrgency(delayed, currentWeek), Is.EqualTo(OrderUrgency.Delayed));
		Assert.That(OrderPresentation.GetUrgency(dueThisWeek, currentWeek), Is.EqualTo(OrderUrgency.DueThisWeek));
		Assert.That(OrderPresentation.GetUrgency(dueSoon, currentWeek), Is.EqualTo(OrderUrgency.DueSoon));
		Assert.That(OrderPresentation.GetUrgency(normal, currentWeek), Is.EqualTo(OrderUrgency.Normal));

		List<Order> sorted = new() { normal, dueSoon, dueThisWeek, delayed };
		sorted.Sort((left, right) => OrderPresentation.CompareByUrgency(left, right, currentWeek));

		Assert.That(sorted, Is.EqualTo(new[] { delayed, dueThisWeek, dueSoon, normal }));
	}

	[Test]
	public void CancelledLines_AreExcludedFromDeadlineAndQuantityCalculations()
	{
		OrderLine activeLine = CreateLine(1, 7, dueWeek: 15);
		OrderLine cancelledLine = CreateLine(2, 99, dueWeek: 1);
		cancelledLine.Cancel();
		Order order = CreateOrder(1, activeLine, cancelledLine);

		Assert.That(OrderPresentation.GetWeeksLeft(order, currentWeek: 10), Is.EqualTo(5));
		Assert.That(OrderPresentation.GetRequestedQuantity(order), Is.EqualTo(7));
		Assert.That(OrderPresentation.GetCompletedQuantity(order), Is.Zero);
	}

	[Test]
	public void ExclusiveStageQuantities_ConvertCumulativeProgressAndPreserveTotal()
	{
		OrderLine line = CreateLine(1, 36, dueWeek: 20);
		Assert.That(line.TryAllocatePicking(28), Is.EqualTo(28));
		Assert.That(line.ReportPickingCompleted(24), Is.EqualTo(24));
		Assert.That(line.ReportPackagingCompleted(19), Is.EqualTo(19));
		Assert.That(line.ReportWaitingForShipping(15), Is.EqualTo(15));
		Assert.That(line.ReportShipping(12), Is.EqualTo(12));
		Assert.That(line.ReportInDelivery(8), Is.EqualTo(8));
		Assert.That(line.ReportCompleted(5), Is.EqualTo(5));
		Order order = CreateOrder(1, line);

		OrderStageQuantities quantities = OrderPresentation.GetExclusiveStageQuantities(order);

		Assert.That(quantities.Pending, Is.EqualTo(8));
		Assert.That(quantities.Allocated, Is.EqualTo(4));
		Assert.That(quantities.Picked, Is.EqualTo(5));
		Assert.That(quantities.Packed, Is.EqualTo(4));
		Assert.That(quantities.AtPort, Is.EqualTo(3));
		Assert.That(quantities.Shipping, Is.EqualTo(4));
		Assert.That(quantities.InDelivery, Is.EqualTo(3));
		Assert.That(quantities.Completed, Is.EqualTo(5));
		Assert.That(quantities.Total, Is.EqualTo(OrderPresentation.GetRequestedQuantity(order)));
	}

	[Test]
	public void SettlementProjection_UsesPerLineDeadlineAndExcludesCancelledLines()
	{
		Dictionary<uint, ItemDefinition> items = new()
		{
			[1] = CreateItem(1, "Produce", price: 10),
			[2] = CreateItem(2, "Equipment", price: 20),
			[3] = CreateItem(3, "Cancelled Cargo", price: 1000),
		};
		OrderLine delayedLine = CreateLine(
			1,
			3,
			dueWeek: 8,
			baseReward: 50,
			delayPenalty: 7,
			reputationChange: 5f);
		OrderLine onTimeLine = CreateLine(
			2,
			2,
			dueWeek: 12,
			baseReward: 100,
			delayPenalty: 11,
			reputationChange: 3f);
		OrderLine cancelledLine = CreateLine(
			3,
			100,
			dueWeek: 1,
			baseReward: 500,
			delayPenalty: 300,
			reputationChange: 100f);
		cancelledLine.Cancel();
		Order order = CreateOrder(1, delayedLine, onTimeLine, cancelledLine);
		ItemDefinition ResolveItem(uint itemId) => items.TryGetValue(itemId, out ItemDefinition item) ? item : null;

		Assert.That(OrderPresentation.CalculateExpectedMoney(order, ResolveItem, settlementWeek: 10), Is.EqualTo(213));
		Assert.That(OrderPresentation.CalculateOnTimeMoney(order, ResolveItem), Is.EqualTo(220));
		Assert.That(OrderPresentation.CalculateDelayMoneyLossAtWeek(order, settlementWeek: 10), Is.EqualTo(7));
		Assert.That(
			OrderPresentation.CalculateExpectedReputation(order, settlementWeek: 10),
			Is.EqualTo(4f).Within(0.0001f));
		Assert.That(OrderPresentation.CalculateOnTimeReputation(order), Is.EqualTo(8f).Within(0.0001f));
		Assert.That(
			OrderPresentation.CalculateDelayReputationLossAtWeek(order, settlementWeek: 10),
			Is.EqualTo(4f).Within(0.0001f));
	}

	[Test]
	public void RepresentativeItem_PrefersNearestUnfinishedLineAndAggregatesItsQuantity()
	{
		Dictionary<uint, ItemDefinition> items = new()
		{
			[1] = CreateItem(1, "Produce", price: 10),
			[2] = CreateItem(2, "Completed Equipment", price: 20),
			[3] = CreateItem(3, "Cancelled Cargo", price: 30),
		};
		OrderLine representativeLine = CreateLine(1, 3, dueWeek: 5);
		OrderLine sameItemLaterLine = CreateLine(1, 4, dueWeek: 15);
		OrderLine completedEarlierLine = CreateCompletedLine(2, 2, dueWeek: 1);
		OrderLine cancelledLine = CreateLine(3, 100, dueWeek: 0);
		cancelledLine.Cancel();
		Order order = CreateOrder(
			1,
			representativeLine,
			sameItemLaterLine,
			completedEarlierLine,
			cancelledLine);
		ItemDefinition ResolveItem(uint itemId) => items.TryGetValue(itemId, out ItemDefinition item) ? item : null;

		OrderRepresentativeItem representative = OrderPresentation.GetRepresentativeItem(order, ResolveItem);

		Assert.That(representative.HasValue, Is.True);
		Assert.That(representative.ItemId, Is.EqualTo(1));
		Assert.That(representative.ItemName, Is.EqualTo("Produce"));
		Assert.That(representative.Quantity, Is.EqualTo(7));
		Assert.That(representative.AdditionalItemTypeCount, Is.EqualTo(1));
	}

	private static Order CreateOrder(int orderId, params OrderLine[] lines)
	{
		Order order = new()
		{
			OrderID = orderId,
			Lines = new List<OrderLine>(),
		};

		foreach (OrderLine sourceLine in lines)
		{
			OrderLine line = object.ReferenceEquals(sourceLine.ParentOrder, order)
				? sourceLine
				: CloneForOrder(sourceLine, order);
			order.Lines.Add(line);
		}

		order.RecalculateStatus();
		return order;
	}

	private static OrderLine CreateLine(
		uint itemId,
		int quantity,
		int dueWeek,
		int baseReward = 0,
		int delayPenalty = 0,
		float reputationChange = 0f)
	{
		Order placeholder = new();
		return new OrderLine(placeholder, itemId, quantity, sourceContract: null)
		{
			DueWeek = dueWeek,
			BaseReward = baseReward,
			DelayPenalty = delayPenalty,
			ReputationChange = reputationChange,
		};
	}

	private static OrderLine CreateCompletedLine(uint itemId, int quantity, int dueWeek)
	{
		OrderLine line = CreateLine(itemId, quantity, dueWeek);
		line.RestoreState(
			saveId: 0,
			status: OrderStatus.Completed,
			startWeek: 0,
			dueWeek: dueWeek,
			baseReward: line.BaseReward,
			delayPenalty: line.DelayPenalty,
			reputationChange: line.ReputationChange,
			pickingAllocatedQuantity: 0,
			pickingCompletedQuantity: 0,
			packagingCompletedQuantity: 0,
			waitingForShippingQuantity: 0,
			shippingQuantity: 0,
			inDeliveryQuantity: 0,
			completedQuantity: 0);
		return line;
	}

	private static OrderLine CloneForOrder(OrderLine source, Order parent)
	{
		OrderLine clone = new(parent, source.ItemID, source.Quantity, source.SourceContract);
		clone.RestoreState(
			source.SaveId,
			source.Status,
			source.StartWeek,
			source.DueWeek,
			source.BaseReward,
			source.DelayPenalty,
			source.ReputationChange,
			source.PickingAllocatedQuantity,
			source.PickingCompletedQuantity,
			source.PackagingCompletedQuantity,
			source.WaitingForShippingQuantity,
			source.ShippingQuantity,
			source.InDeliveryQuantity,
			source.CompletedQuantity);
		return clone;
	}

	private ItemDefinition CreateItem(uint itemId, string itemName, int price)
	{
		ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
		item.name = itemName;
		SetPrivateField(item, "itemID", itemId);
		SetPrivateField(item, "price", price);
		createdItems.Add(item);
		return item;
	}

	private static void SetPrivateField(object target, string fieldName, object value)
	{
		FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {target.GetType().Name}.{fieldName}");
		field.SetValue(target, value);
	}
}
