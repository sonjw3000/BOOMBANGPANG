using System;
using Unity.Mathematics;

public struct PlaceDecision
{
	public ShelfBase shelf;
	public uint ItemID;
	public int Quantity;
}

public interface IPlacingPolicy
{
	bool TryDecide(in int3 workerPos, BoxBase box, Predicate<ShelfBase> pred, out PlaceDecision decision);
}
