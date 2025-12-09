using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public abstract class BoxBase : MonoBehaviour, IItemContainer
{
	[SerializeField] private float capacity = 10.0f;
	protected float size = 0.0f;
	protected Dictionary<uint, ItemStack> stacks = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	// totebox의 stacks는 많지 않을것으로 예상
	public float Size => size;

	public IReadOnlyDictionary<uint, ItemStack> Stacks => stacks;
	public float Capacity => capacity;


	//public BoxBase(float boxCapacity) => capacity = boxCapacity;

	public bool CanRegister() => true;

	// 장소를 단순 등록
	public void RegisterItem(uint itemId)
	{
		stacks[itemId] = new ItemStack(itemId, capacity);
	}

	public void UnregistereItem(uint itemId)
	{
		stacks.Remove(itemId);
	}

	public int AddItem(uint itemId, int quantity)
	{
		float availableSize = capacity - size;
		float itemSize = itemDB.GetItemSize(itemId);

		// quantity를 줄여야한다
		if (availableSize < itemSize * quantity)
			quantity = Mathf.FloorToInt(availableSize / itemSize);

		int res = stacks[itemId].AddItem(quantity);

		UpdateSize();

		return res;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		int res = stacks[itemId].RemoveItem(quantity);

		UpdateSize();

		return res;
	}

	// pallet같은 경우에는 소유한 pallet들의 capacity들을 합쳐야하기 때문에
	protected abstract void UpdateSize();

}

