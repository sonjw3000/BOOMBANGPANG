using System;
using System.Collections.Generic;
using System.Linq;

public class OrderFactory
{
	static ItemLedger ItemLedger => GameContext.Instance.WMSys.ItemLedger;
	static int orderIDCounter = 0;

	public static int NextOrderId => orderIDCounter;
	public static void SetNextOrderId(int nextOrderId) => orderIDCounter = nextOrderId;

	public static List<Order> CreateOrdersFromContracts()
	{
		List<Order> createdOrders = new();
		var activeContracts = GameContext.Instance.ContractMgr.ActiveContracts;
		var currentTime = GameContext.Instance.GameTime;

		if (activeContracts.Count == 0) return createdOrders;

		// 1회 호출 시 최대 3종까지 주문이 들어올 수 있음
		int maxTypes = Math.Min(3, activeContracts.Count);
		int numTypes = UnityEngine.Random.Range(1, maxTypes + 1);

		// 랜덤하게 선택하기 위해 셔플
		var shuffledContracts = activeContracts.OrderBy(x => UnityEngine.Random.value).Take(numTypes).ToList();

		Order order = new()
		{
			OrderID = orderIDCounter++,
			Lines = new List<OrderLine>(),
			Destination = RollDestination(),
		};

		foreach (var contract in shuffledContracts)
		{
			uint itemID = contract.Definition.ItemToHandle.ItemID;

			if (!ItemLedger.OrderableItems.Contains(itemID))
				continue;

			int available = ItemLedger.GetAvailable(itemID);
			if (available <= 0)
				continue;

			var spec = contract.CurrentSpec;
			int quantity = Math.Clamp(UnityEngine.Random.Range(1, 4), 1, available);

			OrderLine line = new(order, itemID, quantity, contract)
			{
				StartWeek = currentTime.WeeksPassed,
				DueWeek = currentTime.WeeksPassed + spec.DeliveryTimeLimitWeeks,
				BaseReward = spec.BaseReward,
				DelayPenalty = spec.DelayPenalty,
				ReputationChange = spec.ReputationChange
			};

			order.Lines.Add(line);
			ItemLedger.OnItemReserved(itemID, quantity);
		}

		if (order.Lines.Count > 0)
		{
			createdOrders.Add(order);
		}

		return createdOrders;
	}

	private static OrderDestination RollDestination()
	{
		return UnityEngine.Random.value < 0.5f
			? OrderDestination.Mars
			: OrderDestination.Titan;
	}
}
