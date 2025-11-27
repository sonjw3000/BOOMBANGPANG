using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Shelf : MonoBehaviour, IItemContainer
{
	[SerializeField] private int stackCount;
	[SerializeField] private float stackCapacity;
	private int3 pickingPosition;
	private List<ItemStack> items;

	public int StackCount => stackCount;
	public float StackCapacity => stackCapacity;
	public int3 PickingPosition => pickingPosition;
	public IReadOnlyList<ItemStack> Items => items;


	void OnEnable()
	{
		pickingPosition = new int3(
			Mathf.RoundToInt(transform.position.x + transform.forward.x),
			Mathf.RoundToInt(transform.position.y),
			Mathf.RoundToInt(transform.position.z + transform.forward.z)
		);
		items = new List<ItemStack>(new ItemStack[stackCount]);
		GameContext.Instance.ItemInventoryData.OnContainerAdded(this);
	}

	void OnDisable()
	{
		GameContext.Instance.ItemInventoryData.OnContainerRemoved(this);
	}
}