using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

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
	Cancelled
}

public class Order
{
	public int OrderID;
	public List<OrderLine> Lines;
	public DateTime DeadLine;
	public int Priority;
}

// 지구가 제일 힘들었던 시기는?
// 고생대

// 수요를 정리함
// 주문이 만족되었는지를 판단하기 위한 데이터
public class OrderLine
{
	public uint ItemID;
	public int Quantity;
	public OrderStatus Status = OrderStatus.Pending;
}
