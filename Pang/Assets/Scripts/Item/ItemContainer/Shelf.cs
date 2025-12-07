using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ShelfBase : MonoBehaviour, IItemContainer
{
	[SerializeField] protected int stackCount;
	[SerializeField] protected float stackCapacity;
	protected int3 pickingPosition;
	protected Dictionary<uint, ItemStack> items = new();

	public int StackCount => stackCount;
	public float StackCapacity => stackCapacity;
	public int3 PickingPosition => pickingPosition;
	public IReadOnlyDictionary<uint, ItemStack> Items => items;

	public bool HasSpace() => stackCapacity > stackCount;

	public void RegisterItem(uint itemId)
	{
		items[itemId] = new ItemStack(itemId);
	}

	public void RemoveItem(uint itemId)
	{
		if (items.ContainsKey(itemId))
		{
			items.Remove(itemId);
		}
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
