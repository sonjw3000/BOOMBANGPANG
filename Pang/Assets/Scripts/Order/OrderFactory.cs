using System;
using System.Collections.Generic;

public class OrderFactory
{
	static ItemLedger itemLedger => GameContext.Instance.WMSys.ItemLedger;


	static int orderIDCounter = 0;
	public static Order CreateRandomOrder()
	{
		Order order = new Order();

		// 추세에 따라서 랜덤한 주문을 생성한다.
		// 현재는 완전 랜덤값으로 설정함
		order.OrderID = orderIDCounter++;
		int orderables = itemLedger.OrderableItems.Count;
		int numberOfLines = Math.Clamp(UnityEngine.Random.Range(1, 2), 0, orderables);

		if (numberOfLines == 0)
			return null;

		order.Lines = new List<OrderLine>(numberOfLines);

		for (int i = 0; i < numberOfLines; ++i)
		{
			OrderLine line = new OrderLine();
			line.ItemID = itemLedger.OrderableItems[UnityEngine.Random.Range(0, orderables)];
			line.Quantity = UnityEngine.Random.Range(1, 4);
			order.Lines.Add(line);
		}

		return order;
	}
}
