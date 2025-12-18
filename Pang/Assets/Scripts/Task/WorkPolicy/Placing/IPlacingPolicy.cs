using Unity.Mathematics;

public struct PlaceDecision
{
	public ShelfBase shelf;
	public uint ItemID;
	public int Quantity;
}

public interface IPlacingPolicy
{
	bool TryDecide(in int3 workerPos, BoxBase box, out PlaceDecision decision);
}
