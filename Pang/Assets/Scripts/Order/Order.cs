using System;
using System.Collections.Generic;

public enum OrderStatus
{
	// 주문 처리 대기중
	Pending,
	// 주문 할당됨
	Allocated,
	// 피킹 작업중
	Picking,
	// 포장 작업중
	Packaging,
	// 배송 작업중
	Shipping,
	// 주문 완료
	Completed,
	// 주문 취소됨
	Cancelled,
	// 주문 딜레이
	Delayed,
}

public enum OrderTotalStatus
{
	Pending,
	InProgress,
	Completed,
	Cancelled,
}

public class Order
{
	public int OrderID;
	public List<OrderLine> Lines;
	public Tuple<int, int> DeadLine;
	public int Priority;
	
	private OrderTotalStatus status = OrderTotalStatus.Pending;

	public OrderTotalStatus Status => status;

	public void ChangeOrderStatus(OrderStatus status)
	{
		// check all lines are completed
		if (status == OrderStatus.Completed)
		{
			bool isAllCompleted = true;
			foreach (var line in Lines)
			{
				if (line.Status != OrderStatus.Completed)
				{
					isAllCompleted = false;
					break;
				}
			}

			if (isAllCompleted)
			{
				this.status = OrderTotalStatus.Completed;
			}

			return;
		}

		// todo
		// 유저의 액션이나 주문 지연으로 인한 고객의 취소 등을 대응해야한다
		// 여기에

		this.status = OrderTotalStatus.InProgress;
	}
}

// 지구가 제일 힘들었던 시기는?
// 고생대

// 수요를 정리함
// 주문이 만족되었는지를 판단하기 위한 데이터
public class OrderLine
{
	public readonly Order ParentOrder;
	public readonly uint ItemID;
	public readonly int Quantity;

	private OrderStatus status = OrderStatus.Pending;
	public OrderStatus Status => status;

	public OrderLine(Order parentOrder, uint itemID, int quantity)
	{
		ParentOrder = parentOrder;
		ItemID = itemID;
		Quantity = quantity;
	}

	public void ChangeOrderStatus(OrderStatus status)
	{
		this.status = status;

		ParentOrder.ChangeOrderStatus(status);
	}
}
