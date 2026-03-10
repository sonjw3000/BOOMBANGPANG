using System;
using System.Collections.Generic;

public class OrderFactory
{
	static ItemLedger ItemLedger => GameContext.Instance.WMSys.ItemLedger;


	static int orderIDCounter = 0;
	public static Order CreateRandomOrder()
	{
		Order order = new();

		// 추세에 따라서 랜덤한 주문을 생성한다.
		// 현재는 완전 랜덤값으로 설정함
		order.OrderID = orderIDCounter++;
		int orderables = ItemLedger.OrderableItems.Count;
		int numberOfLines = Math.Clamp(UnityEngine.Random.Range(1, 2), 0, orderables);

		if (numberOfLines == 0)
			return null;

		order.Lines = new List<OrderLine>(numberOfLines);

		for (int i = 0; i < numberOfLines; ++i)
		{
			OrderLine line = new();
			line.ItemID = ItemLedger.OrderableItems[UnityEngine.Random.Range(0, orderables)];
			int maxOrderable = ItemLedger.GetAvailable(line.ItemID);
			if (maxOrderable <= 0)
				continue;

			line.Quantity = Math.Clamp(UnityEngine.Random.Range(1, 4), 1, maxOrderable);
			order.Lines.Add(line);

			ItemLedger.OnItemReserved(line.ItemID, line.Quantity);
		}

		return order;
	}
}
