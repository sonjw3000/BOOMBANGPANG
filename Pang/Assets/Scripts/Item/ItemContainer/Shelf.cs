using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.Progress;

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

	public int AddItem(uint  itemId, int quantity)
	{
		return stacks[itemId].AddItem(quantity);
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		return stacks[itemId].RemoveItem(quantity);
	}

}

public class Shelf : ShelfBase
{
	void OnEnable()
	{
		Debug.Log("Shelf 등장이요");
		pickingPosition = new int3(
			Mathf.RoundToInt(transform.position.x + transform.forward.x),
			Mathf.RoundToInt(transform.position.y),
			Mathf.RoundToInt(transform.position.z + transform.forward.z)
		);
		GameContext.Instance.ItemInventoryData.OnContainerAdded(this);
	}
	void OnDisable()
	{
		GameContext.Instance.ItemInventoryData.OnContainerRemoved(this);
	}
}
