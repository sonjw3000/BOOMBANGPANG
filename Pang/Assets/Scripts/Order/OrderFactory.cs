using System.Collections.Generic;

public class OrderFactory
{
	static int orderIDCounter = 0;
	public static Order CreateRandomOrder()
	{
		Order order = new Order();

		// 추세에 따라서 랜덤한 주문을 생성한다.
		// 현재는 완전 랜덤값으로 설정함
		order.OrderID = orderIDCounter++;
		int numberOfLines = UnityEngine.Random.Range(1, 2);
		order.Lines = new List<OrderLine>(numberOfLines);
		for (int i = 0; i < numberOfLines; ++i)
		{
			OrderLine line = new OrderLine();
			line.ItemID = GameContext.Instance.ItemDB.GetRandomItemID(); 
			line.Quantity = UnityEngine.Random.Range(1, 4);
			order.Lines.Add(line);
		}

		return order;
	}
}
