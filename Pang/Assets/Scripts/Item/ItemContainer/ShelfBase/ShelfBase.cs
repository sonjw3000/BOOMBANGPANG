using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ShelfBase : MonoBehaviour, IItemContainer
{
	[SerializeField] protected int maxStacks;
	[SerializeField] protected float sizePerStack;
	protected int currentStackCount;
	protected int3 pickingPosition;
	protected Dictionary<uint, ItemStack> stacks = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	public int CurrentStackCount => currentStackCount;
	public float MaxStack => maxStacks;
	public int3 PickingPosition => pickingPosition;
	public IReadOnlyDictionary<uint, ItemStack> Stacks => stacks;

	public bool CanRegister() => maxStacks > currentStackCount;

	public void RegisterItem(uint itemId)
	{
		++currentStackCount;
		stacks[itemId] = new ItemStack(itemId, sizePerStack);
	}

	public void UnregistereItem(uint itemId)
	{
		if (stacks.ContainsKey(itemId))
		{
			--currentStackCount;
			stacks.Remove(itemId);
		}
	}
	// 가장 쉬운 숫자는?
	// 190,000

	// 식인종이 우사인볼트를 보면?
	// 패스트푸드

	public int AddItem(uint itemId, int quantity)
	{
		return stacks[itemId].AddItem(quantity);
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		return stacks[itemId].RemoveItem(quantity);
	}

}