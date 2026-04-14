using Unity.Mathematics;

public struct CollectingPolicy
{
	public ShelfBase Shelf;
	public uint ItemId;
	public int Quantity;
}

public interface ICollectingPolicy
{
	bool TryDecide(in int3 workerPos, out CollectingPolicy decision);
}

