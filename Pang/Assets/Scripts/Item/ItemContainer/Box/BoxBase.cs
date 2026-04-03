using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum BoxType
{
	None = -1,
	Cargo = 0,
	Personal = 1,
	Any = 2,
}

public abstract class BoxBase : MonoBehaviour, IItemContainer
{
	[SerializeField] BoxType boxType;
	[SerializeField] private float capacity = 10.0f;
	protected float size = 0.0f;

	protected List<ItemStack> stacks = new();
	protected Dictionary<uint, int> itemTotals = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;
	protected BoxPoolService BoxService => GameContext.Instance.WMSys.BoxPoolMgr;

	// totebox의 stacks는 많지 않을것으로 예상
	public float TotalSize => size;
	public float MaxSize => capacity;

	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;

	public float Capacity => capacity;

	
	public void UpdateToteCapacity(float capacity) => this.capacity = capacity;

	private void Start()
	{
		BoxService.RegisterBox(this);
	}

	private void OnDestroy()
	{
		BoxService.UnRegisterBox(this);
	}

	public bool CanRegister() => true;

	// return true when the payload fully moved
	public bool AddItem(List<ItemStack> payload)
	{
		for (int i = payload.Count - 1; i >= 0; --i) 
		{
			ItemStack stack = payload[i];

			int result = AddItem(stack.ItemID, stack.Quantity);
			stack.RemoveItem(result);

			if (stack.Quantity <= 0)
				payload.RemoveAt(i);

			itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID, 0) + result;
		}

		return payload.Count <= 0;
	}

	public int AddItem(uint itemId, int quantity)
	{
		float availableSize = capacity - size;
		float itemSize = itemDB.GetItemSize(itemId);

		// quantity를 줄여야한다
		if (availableSize < itemSize * quantity)
			quantity = Mathf.FloorToInt(availableSize / itemSize);

		// 0이면 불필요한 로직을 타지 않게
		if (quantity == 0)
		{
			return 0;
		}

		ItemStack stack = stacks.Find(id => id.ItemID == itemId);

		if (stack == null)
		{
			stack = new ItemStack(itemId, this.capacity);
			stacks.Add(stack);
		}

		int res = stack.AddItem(quantity);

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId, 0) + res;

		UpdateSize();

		return res;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		ItemStack stack = stacks.Find(id => id.ItemID == itemId);

		if (stack == null)
			return 0;

		int res = stack.RemoveItem(quantity);

		if (stack.Quantity <= 0)
		{
			stacks.Remove(stack);
		}

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId, 0) - res;

		UpdateSize();

		return res;
	}

	// pallet같은 경우에는 소유한 pallet들의 capacity들을 합쳐야하기 때문에
	protected abstract void UpdateSize();

}

