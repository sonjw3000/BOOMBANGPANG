using System.Collections.Generic;
using Unity.Mathematics;

public interface IItemContainer
{
	int StackSize { get; }
	int3 Position { get; }
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
