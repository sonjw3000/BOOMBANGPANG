using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum BoxType
{
	None = -1,
	Cargo = 0,
	Personal = 1,
	Capsule = 2,
	Any = 3,
}

public abstract partial class BoxBase : MonoBehaviour, IItemContainer
{
	[SerializeField] BoxType boxType;
	[SerializeField] private float capacity = 10.0f;
	[SerializeField] private uint boxId = 0;
	protected float size = 0.0f;
	private ItemTag itemTags = ItemTag.None;

	protected List<ItemStack> stacks = new();
	protected Dictionary<uint, int> itemTotals = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;
	protected BoxManager BoxMgr => GameContext.Instance.BoxMgr;

	// totebox의 stacks는 많지 않을것으로 예상
	public float TotalSize => size;
	public float MaxSize => capacity;

	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public ItemTag ItemTags => itemTags;

	public float Capacity => capacity;
	public BoxType Type => boxType;
	public uint BoxId => boxId;

	public void SetBoxId(uint id) => boxId = id;

	public virtual void ResetContainer()
	{
		for (int i = 0; i < stacks.Count; ++i)
			stacks[i]?.Recycle();

		stacks.Clear();
		itemTotals.Clear();
		size = 0;
		itemTags = ItemTag.None;
	}

	public void UpdateToteCapacity(float capacity) => this.capacity = capacity;

	public bool CanRegister() => true;

	public int GetQuantity(uint itemId)
	{
		return itemTotals.GetValueOrDefault(itemId);
	}

	public int GetAcceptableQuantity(uint itemId, int requested)
	{
		if (requested <= 0)
			return 0;

		if (FindDefaultStack(itemId) == null && CanCreateNewStack() == false)
			return 0;

		float availableSize = capacity - size;
		float itemSize = itemDB.GetItemSize(itemId);
		if (itemSize <= 0.0f)
			return 0;

		return Mathf.Clamp(Mathf.FloorToInt(availableSize / itemSize), 0, requested);
	}

	public bool CanAcceptStack(ItemStack stack)
	{
		if (stack == null || stack.Quantity <= 0)
			return false;

		if (stack.Size + size > MaxSize)
			return false;

		return FindMergeTarget(stack) != null || CanCreateNewStack();
	}

	// return true when the payload fully moved
	public bool AddItem(List<ItemStack> payload)
	{
		for (int i = payload.Count - 1; i >= 0; --i) 
		{
			ItemStack stack = payload[i];
			if (stack == null)
			{
				payload.RemoveAt(i);
				continue;
			}

			if (AddStack(stack))
			{
				payload.RemoveAt(i);
				if (stack.Quantity <= 0)
					stack.Recycle();
			}
		}

		return payload.Count <= 0;
	}

	public int AddItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int acceptable = GetAcceptableQuantity(itemId, quantity);
		if (acceptable <= 0)
			return 0;

		ItemStack stack = FindDefaultStack(itemId);
		if (stack == null)
		{
			if (CanCreateNewStack() == false)
				return 0;

			stack = ItemStack.RentDefault(itemId);
			stacks.Add(stack);
		}

		int res = stack.AddItem(acceptable);

		if (stack.Quantity < 0)
		{
			Debug.LogError(
				$"[BoxBase] Stack quantity is negative after AddItem. " +
				$"BoxId={boxId}, BoxType={boxType}, ItemId={itemId}, " +
				$"Requested={quantity}, Adjusted={acceptable}, Added={res}, " +
				$"Capacity={capacity}, CurrentSize={size}, StackQuantity={stack.Quantity}, " +
				$"Stacks={BuildStackDebugText()}");
		}

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId, 0) + res;

		UpdateSize();

		return res;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int remain = quantity;
		int removed = 0;
		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack stack = stacks[i];
			if (stack.HasItemID(itemId) == false || stack.IsDefaultIdentity == false)
				continue;

			int res = stack.RemoveItem(remain);
			removed += res;
			remain -= res;
			if (stack.Quantity <= 0)
			{
				stacks.RemoveAt(i);
				stack.Recycle();
			}

			if (remain <= 0)
				break;
		}

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId, 0) - removed;
		if (itemTotals[itemId] <= 0)
			itemTotals.Remove(itemId);

		UpdateSize();

		return removed;
	}

	private string BuildStackDebugText()
	{
		if (stacks == null || stacks.Count == 0)
			return "empty";

		List<string> stackTexts = new(stacks.Count);
		foreach (ItemStack stack in stacks)
		{
			if (stack == null)
				continue;

			stackTexts.Add($"{stack.ItemID}x{stack.Quantity}");
		}

		return string.Join(", ", stackTexts);
	}

	public bool AddStack(ItemStack stack)
	{
		if (CanAcceptStack(stack) == false)
			return false;

		uint itemId = stack.ItemID;
		int quantity = stack.Quantity;
		ItemStack mergeTarget = FindMergeTarget(stack);
		if (mergeTarget != null)
		{
			if (mergeTarget.TryMergeFrom(stack) == false)
				return false;

			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + quantity;
		}
		else
		{
			stacks.Add(stack);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + quantity;
		}

		UpdateSize();

		return true;
	}

	public bool RemoveStack(ItemStack stack)
	{
		if (stack == null || stacks.Remove(stack) == false)
			return false;

		itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID) - stack.Quantity;
		if (itemTotals[stack.ItemID] <= 0)
			itemTotals.Remove(stack.ItemID);

		UpdateSize();

		return true;
	}

	public bool TryRemoveFromStack(ItemStack stack, int quantity, out ItemStack removedStack)
	{
		removedStack = null;
		if (stack == null || quantity <= 0 || stacks.Contains(stack) == false)
			return false;

		uint itemId = stack.ItemID;
		removedStack = stack.Split(quantity);
		if (removedStack == null)
			return false;

		int removedQuantity = removedStack.Quantity;
		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) - removedQuantity;
		if (itemTotals[itemId] <= 0)
			itemTotals.Remove(itemId);

		if (stack.Quantity <= 0)
		{
			stacks.Remove(stack);
			stack.Recycle();
		}

		UpdateSize();
		return true;
	}

	// pallet같은 경우에는 소유한 pallet들의 capacity들을 합쳐야하기 때문에
	protected abstract void UpdateSize();

	protected void RebuildItemTags()
	{
		itemTags = ItemTag.None;

		if (itemDB == null || stacks == null)
			return;

		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (itemDB.GetItemData(stack.ItemID, out ItemDefinition itemData) == false || itemData == null)
				continue;

			itemTags |= itemData.Tag;
		}
	}

	private bool CanCreateNewStack() => true;

	private ItemStack FindDefaultStack(uint itemId)
	{
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack.HasItemID(itemId) && stack.IsDefaultIdentity)
				return stack;
		}

		return null;
	}

	private ItemStack FindMergeTarget(ItemStack incoming)
	{
		if (incoming == null)
			return null;

		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (ReferenceEquals(stack, incoming))
				continue;

			if (stack.CanMergeWith(incoming))
				return stack;
		}

		return null;
	}

}
