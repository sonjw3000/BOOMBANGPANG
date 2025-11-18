using System.Collections.Generic;
using Unity.Mathematics;

public interface IItemContainer
{
	int StackCount { get; }
	float StackCapacity { get; }
	int3 PickingPosition { get; }
	IReadOnlyList<ItemStack> Items { get; }
}

[System.Serializable]
public struct ItemStack
{
	public int ItemID;
	public int Quantity;
}

public struct ItemLocation
{
	public IItemContainer Container;
	public int StackIndex;
	public int Quantity;
}
