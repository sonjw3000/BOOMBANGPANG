using System.Collections.Generic;

public class DeliveryRequest
{
	private uint requestedContractID;
	private ItemDefinition targetItem;
	private int quantity;

	public uint RequestedContractID => requestedContractID;
	public ItemDefinition TargetItem => targetItem;
	public int Quantity => quantity;

	public void Set(uint contract, ItemDefinition item, int quantity)
	{
		requestedContractID = contract;
		targetItem = item;
		this.quantity = quantity;
	}

	public void ReduceAmount(int amount)
	{
		quantity -= amount;
	}
}

public class DeliveryQueue
{
	// todo
	// 순회가 가능하고, 우선순위가 있게 만들어야함
	// 또한 유저가 순서를 바꾸는 기능도 있을수도 있기 때문에 일단 연결리스트
	private readonly LinkedList<DeliveryRequest> queue = new();
	public int Count => queue.Count;

	public void Enqueue(DeliveryRequest contract)
	{
		queue.AddLast(contract);
	}

	public DeliveryRequest Dequeue()
	{
		DeliveryRequest request = queue.First.Value;
		queue.RemoveFirst();
		return request;
	}

	public bool Peek(out DeliveryRequest request)
	{
		request = null;
		if (queue.Count == 0)
			return false;

		request = queue.First.Value;
		return true;
	}

	public IEnumerable<DeliveryRequest> Enumerate()
	{
		foreach (var request in queue)
			yield return request;
	}

	public void Clear() => queue.Clear();
}

public partial class DeliveryService
{
	private readonly DeliveryQueue deliveryQueue = new();
	private readonly ItemPool<DeliveryRequest> requestPool = new(5, ()=> { return new DeliveryRequest(); });

	public void RequestDelivery(uint contractID, ItemDefinition item, int quantity)
	{
		DeliveryRequest request = requestPool.Get();
		request.Set(contractID, item, quantity);
		deliveryQueue.Enqueue(request);
	}

	public bool TryPeek(out DeliveryRequest req)
	{
		return deliveryQueue.Peek(out req);
	}

	public void AcceptDelivery()
	{
		DeliveryRequest request = deliveryQueue.Dequeue();
		requestPool.Release(request);
	}

}
