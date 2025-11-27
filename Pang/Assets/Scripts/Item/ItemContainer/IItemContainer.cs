using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface IItemContainer
{
	//[SerializeField] protected int stackCount;
	//[SerializeField] protected float stackCapacity;
	//protected int3 pickingPosition;
	//protected Dictionary<uint, ItemStack> items;

	public int StackCount { get; }
	public float StackCapacity { get; }
	public int3 PickingPosition { get; }
	public IReadOnlyDictionary<uint, ItemStack> Items { get; }

	public void RegisterItem(uint itemId);

	public void RemoveItem(uint itemId);
}

[System.Serializable]
public class ItemStack
{
	public uint ItemID;
	public int Quantity;
}

public class ItemLocation
{
	public ShelfBase Container;
	public int StackIndex;
	public int Quantity;
}
