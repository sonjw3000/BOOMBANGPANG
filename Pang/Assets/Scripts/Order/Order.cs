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
	// 배송 대기중
	WaitingForShipping,
	// 배송 작업중
	Shipping,
	// 배송중
	IndDelivery,
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

	public void RestoreStatus(OrderTotalStatus status)
	{
		this.status = status;
	}

	public OrderTotalStatus ChangeOrderStatus(OrderStatus status)
	{
		// check all lines are in a final state (Completed or Cancelled)
		bool isAllFinal = true;
		foreach (var line in Lines)
		{
			if (line.Status != OrderStatus.Completed && line.Status != OrderStatus.Cancelled)
			{
				isAllFinal = false;
				break;
			}
		}

		if (isAllFinal)
		{
			this.status = OrderTotalStatus.Completed; // Or create a Settle state
		}
		else
		{
			this.status = OrderTotalStatus.InProgress;
		}

		return this.status;
	}
	}

	// 지구가 제일 힘들었던 시기는?
	// 고생대

	// 수요를 정리함
	// 주문이 만족되었는지를 판단하기 위한 데이터
	public class OrderLine
	{
	public int SaveId { get; set; }
	public readonly Order ParentOrder;
	public readonly uint ItemID;
	public readonly int Quantity;
	public readonly Assets.Scripts.Contract.ContractRuntime SourceContract;

	public int StartWeek;
	public int DueWeek;
	public int BaseReward;
	public int DelayPenalty;
	public float ReputationChange;

	private OrderStatus status = OrderStatus.Pending;
	public OrderStatus Status => status;

	public OrderLine(Order parentOrder, uint itemID, int quantity, Assets.Scripts.Contract.ContractRuntime sourceContract)
	{
		ParentOrder = parentOrder;
		ItemID = itemID;
		Quantity = quantity;
		SourceContract = sourceContract;
	}

	public OrderTotalStatus ChangeOrderStatus(OrderStatus status)
	{
		this.status = status;

		return ParentOrder.ChangeOrderStatus(status);
	}

	public void RestoreState(int saveId, OrderStatus status, int startWeek, int dueWeek, int baseReward, int delayPenalty, float reputationChange)
	{
		SaveId = saveId;
		this.status = status;
		StartWeek = startWeek;
		DueWeek = dueWeek;
		BaseReward = baseReward;
		DelayPenalty = delayPenalty;
		ReputationChange = reputationChange;
	}
}
