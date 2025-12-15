using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public abstract class ShelfBase : 
	MonoBehaviour, 
	IItemContainer, 
	IGridPlaceable, 
	IGridPlacementEffect,
	IInteractionPoint
{
	[SerializeField] protected int maxStacks;
	[SerializeField] protected float sizePerStack;
	protected int currentStackCount;
	private int3 position;
	protected List<int3> interactionPoints = new();

	protected Dictionary<uint, ItemStack> stacks = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	static private Cell[,,] GridMap => GameContext.Instance.MapResources.mapRef;

	public int CurrentStackCount => currentStackCount;
	public float MaxStack => maxStacks;
	public int3 GridPosition => position;
	public IReadOnlyList<int3> InteractionPoints => interactionPoints;
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

	protected abstract void SetInteractionPoints();

	public void OnRemoved()
	{
		foreach (int3 interPos in interactionPoints)
		{
			// pickingposition위에 아무것도 없는 경우엔 삭제, 뭔가 있다 == 로봇이 올라가 있다 -> 삭제하면 안됨
			Cell cell = GridMap[interPos.x, interPos.y, interPos.z];
			if (cell.type < 0)
			{
				cell.type = 0;
			}
			cell.previousType = 0;
		}

		Cell thisPos = GridMap[position.x, position.y, position.z];
		thisPos.type = thisPos.previousType;
	}

	public void OnPositionSet(in int3 position)
	{
		enabled = true;

		// set position
		this.position = position;

		// set pickingPosition
		SetInteractionPoints();
		foreach (int3 pickingPos in interactionPoints)
		{
			Cell pickPos = GridMap[pickingPos.x, pickingPos.y, pickingPos.z];

			// set picking position's tile -1
			if (pickPos.type == 0)
			{
				pickPos.type = -1;
			}
			pickPos.previousType = -1;
		}
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
		// 부셔지면 뭐 본인이 가진 아이템을 뭐시기 해야함
		// 근데 지가 로켓이면 뭐 로켓이 로케트 부순거니까
		// 근데 로케트의 아이템은 인벤토리에서 관리를 안해
		// 제가 꽁꽁 숨겨뒀으니 찾아보세요
	}
}
