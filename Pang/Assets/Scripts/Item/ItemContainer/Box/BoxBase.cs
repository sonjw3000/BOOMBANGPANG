using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UIElements;

[System.Serializable]
public enum BoxType
{
	Cargo = 0,
	Personal = 1,
}

public abstract class BoxBase : MonoBehaviour, IItemContainer
{
	[SerializeField] BoxType boxType;
	[SerializeField] private float capacity = 10.0f;
	protected float size = 0.0f;
	protected Dictionary<uint, ItemStack> stacks = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;
	protected BoxPoolService BoxService => GameContext.Instance.WMSys.BoxPoolMgr;

	// totebox의 stacks는 많지 않을것으로 예상
	public float Size => size;

	public IReadOnlyDictionary<uint, ItemStack> Stacks => stacks;
	public float Capacity => capacity;

	
	private void Start()
	{
		BoxService.RegisterBox(this);
	}

	private void OnDestroy()
	{
		BoxService.UnRegisterBox(this);
	}

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

	// return true when the payload fully moved
	public bool AddItem(Dictionary<uint, ItemStack> payload)
	{
		List<uint> del = new();
		bool removed = true;
		foreach (var (id, stack) in payload)
		{
			int quantity = stack.Quantity;
			int added = AddItem(id, stack.Quantity);

			stack.RemoveItem(added);

			// not fully moved
			if (quantity != added)
			{
				removed = false;
				break;
			}
			del.Add(id);
		}

		// clear empty stacks of payload

		foreach (uint id in del)
		{
			payload.Remove(id);
		}

		return removed;
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

		if (stacks[itemId] == null)
			stacks[itemId] = new ItemStack(itemId, this.capacity);

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

