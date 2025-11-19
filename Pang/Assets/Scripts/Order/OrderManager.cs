using System.Collections.Generic;

// OrderManager
// OrderManager는 주문을 생성하고 관리한다
// 유저의 입력에 따라피킹 알고리즘의 변화가 있을 수 있기 때문에 Allocator는 별도의 클래스로 분리한다
// OrderManager 주문을 생성하고(랜덤으로) 이를 피킹태스크로 변환해야한다
// PickingTask.PickingLine을 생성하고 이를 지역별로 묶는다
// 묶인 PickingLine을 PickingTask.PickJob으로 변환한다
// PickJob을 PickingTask로 변환한다
// PickingTask를 TaskManager에 등록한다

public class OrderManager
{
	private List<Order> orders = new();
	public IReadOnlyList<Order> Orders => orders;

	public void CreateRandomOrder()
	{
		var order = OrderFactory.CreateRandomOrder();

		orders.Add(order);
	}
}

