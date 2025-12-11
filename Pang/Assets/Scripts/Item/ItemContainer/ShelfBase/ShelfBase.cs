using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public abstract class ShelfBase : MonoBehaviour, IItemContainer, IGridPlaceable
{
	[SerializeField] protected int maxStacks;
	[SerializeField] protected float sizePerStack;
	protected int currentStackCount;
	private int3 position;
	protected int3 pickingPosition;

	protected Dictionary<uint, ItemStack> stacks = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	public int CurrentStackCount => currentStackCount;
	public float MaxStack => maxStacks;
	public int3 GridPosition => position;
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

	protected abstract void SetPickingPosition();

	public void OnReset(Cell[,,] map)
	{
		// pickingposition위에 아무것도 없는 경우엔 삭제, 뭔가 있다 == 로봇이 올라가 있다 -> 삭제하면 안됨
		Cell pickPos = map[PickingPosition.x, PickingPosition.y, PickingPosition.z];
		if (pickPos.type < 0)
		{
			pickPos.type = 0;
		}
		pickPos.previousType = 0;

		Cell thisPos = map[position.x, position.y, position.z];
		thisPos.type = thisPos.previousType;
	}

	public void OnPositionSet(Cell[,,] map, int3 position)
	{
		// set position
		this.position = position;

		// set pickingPosition
		SetPickingPosition();
		Cell pickPos = map[PickingPosition.x, PickingPosition.y, PickingPosition.z];

		// set picking position's tile -1
		if (pickPos.type == 0)
		{
			pickPos.type = -1;
		}
		pickPos.previousType = -1;
	}

}
