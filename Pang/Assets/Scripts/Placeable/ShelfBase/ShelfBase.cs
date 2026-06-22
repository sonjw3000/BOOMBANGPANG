using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;



public abstract partial class ShelfBase :
	ItemInteraction,
	IItemContainer
{
	[SerializeField] protected int maxStacks = 16;
	[SerializeField] protected float sizePerStack;

	protected List<ItemStack> stacks;
	protected Dictionary<uint, int> itemTotals = new();
	protected Dictionary<uint, int> itemsReservedPick = new();

	private float totalSize = 0.0f;

	public float TotalSize => totalSize;
	public float MaxSize => sizePerStack * maxStacks;

	public float FilledPercent => MaxSize <= 0 ? 0 : (TotalSize / MaxSize) * 100.0f;


	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	// item의 종류가 등록/해제 되었을 경우
	public event Action<ShelfBase, uint, bool> OnItemPresentChanged;

	// item quantity의 변경이 일어났을 경우
	public event Action<ShelfBase, uint, int> OnItemQuantityChanged;

	// item의 picking이 예약되었을 때 (quantity는 예약된 수량)
	public event Action<ShelfBase, uint, int> OnItemReservedPickChanged;

	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public IReadOnlyDictionary<uint, int> ItemToBePicked => itemsReservedPick;
	public int GetPickableQuantity(uint itemID) => ItemTotals.GetValueOrDefault(itemID) - ItemToBePicked.GetValueOrDefault(itemID);
	public bool CanRegister() => MaxStack > Stacks.Count;
	public float MaxStack => maxStacks;

	public int GetQuantity(uint itemId)
	{
		return itemTotals.GetValueOrDefault(itemId);
	}

	public int GetAcceptableQuantity(uint itemId, int requested)
	{
		if (requested <= 0)
			return 0;

		float itemSize = itemDB.GetItemSize(itemId);
		if (itemSize <= 0.0f)
			return 0;

		int capacity = 0;
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack.ItemID == itemId)
				capacity += stack.AvailableAmount;
		}

		int freeSlots = maxStacks - stacks.Count;
		capacity += freeSlots * Mathf.FloorToInt(sizePerStack / itemSize);

		return Mathf.Clamp(capacity, 0, requested);
	}

	public bool CanAcceptStack(ItemStack stack)
	{
		return stack != null && stack.StackSize <= sizePerStack && stacks.Count < maxStacks;
	}

	protected virtual void Awake()
	{
		stacks = new List<ItemStack>(capacity: maxStacks);
	}

	// 가장 쉬운 숫자는?
	// 190,000

	// 식인종이 우사인볼트를 보면?
	// 패스트푸드

	private void UpdateSize()
	{
		totalSize = stacks.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
	}


	public int AddItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int befItemCounts = itemTotals.GetValueOrDefault(itemId);
		int befItemStacks = stacks.Count;

		// 기존 인덱스에 넣기
		int remain = quantity;
		for (int i = 0; i < stacks.Count; ++i) 
		{
			ItemStack stack = stacks[i];

			if (stack.ItemID != itemId)
				continue;

			int itemAdded = stack.AddItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + itemAdded;
			remain -= itemAdded;

			if (remain <= 0) break;
		}

		// 기존 인덱스가 없다면 새로 만들어 채우기
		while (remain > 0 && stacks.Count < maxStacks)
		{
			ItemStack stack = new ItemStack(itemId, sizePerStack);
			stacks.Add(stack);

			int itemAdded = stack.AddItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + itemAdded;
			remain -= itemAdded;
		}

		int curItemStacks = stacks.Count;

		// 새로운 종류의 stack이 register 되었다면?
		if (befItemCounts == 0 && befItemStacks != curItemStacks)
		{
			OnItemPresentChanged?.Invoke(this, itemId, true);
		}

		int addedItem = quantity - remain;
		OnItemQuantityChanged?.Invoke(this, itemId, addedItem);

		UpdateSize();

		return addedItem;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int remain = quantity;

		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack stack = stacks[i];

			if (stack.ItemID != itemId)
				continue;
			
			int itemRemoved = stack.RemoveItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) - itemRemoved;
			remain -= itemRemoved;
			if (stack.Quantity <= 0)
				stacks.RemoveAt(i);

			if (remain == 0)
				break;
		}

		// 아이템이 사라졌다면
		if (itemTotals.TryGetValue(itemId, out int value) && value == 0)
		{
			itemTotals.Remove(itemId);
			OnItemPresentChanged?.Invoke(this, itemId, false);
		}

		int removed = quantity - remain;

		OnItemQuantityChanged?.Invoke(this, itemId, -removed);

		UpdateSize();

		return removed;
	}

	public bool AddStack(ItemStack stack)
	{
		if (CanAcceptStack(stack) == false)
			return false;

		int befItemCounts = itemTotals.GetValueOrDefault(stack.ItemID);
		stacks.Add(stack);
		itemTotals[stack.ItemID] = befItemCounts + stack.Quantity;

		if (befItemCounts == 0)
			OnItemPresentChanged?.Invoke(this, stack.ItemID, true);

		OnItemQuantityChanged?.Invoke(this, stack.ItemID, stack.Quantity);

		UpdateSize();

		return true;
	}

	public bool RemoveStack(ItemStack stack)
	{
		if (stack == null || stacks.Remove(stack) == false)
			return false;

		itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID) - stack.Quantity;
		if (itemTotals[stack.ItemID] <= 0)
		{
			if (itemTotals[stack.ItemID] < 0)
				Debug.LogWarning($"Item total for {stack.ItemID} went below zero after removing stack. Adjusting to zero.");

			itemTotals.Remove(stack.ItemID);
			OnItemPresentChanged?.Invoke(this, stack.ItemID, false);
		}

		OnItemQuantityChanged?.Invoke(this, stack.ItemID, -stack.Quantity);

		UpdateSize();

		return true;
	}
	public bool CanAccept(uint itemId, int quantity)
	{
		int capacity = 0;
		float itemSize = itemDB.GetItemSize(itemId);

		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack.ItemID == itemId)
				capacity += stack.AvailableAmount;
		}

		int freeslots = maxStacks - stacks.Count;
		capacity += freeslots * (int)(sizePerStack / itemSize);

		return capacity >= quantity;
	}

	public override void OnRemoved()
	{
		//foreach (int3 interPos in interactionPoints)
		//{
		//	// pickingposition위에 아무것도 없는 경우엔 삭제, 뭔가 있다 == 로봇이 올라가 있다 -> 삭제하면 안됨
		//	//Cell cell = GridMap[interPos.x, interPos.y, interPos.z];
		//	//if (cell.type < 0)
		//	//{
		//	//	cell.type = 0;
		//	//}
		//	//cell.previousType = 0;
		//}

		//Cell thisPos = GridMap[position.x, position.y, position.z];
		//thisPos.type = thisPos.previousType;
	}

	public override void OnPositionSet(in int3 position, FacingDirection direction)
	{
		enabled = true;

		// set position
		this.position = position;
		facingDirection = direction;
		// set pickingPosition
		//SetInteractionPoints();
		//foreach (int3 pickingPos in interactionPoints)
		//{
		//	//Cell pickPos = GridMap[pickingPos.x, pickingPos.y, pickingPos.z];

		//	//// set picking position's tile -1
		//	//if (pickPos.type == 0)
		//	//{
		//	//	pickPos.type = -1;
		//	//}
		//	//pickPos.previousType = -1;
		//}
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{
		OnDestroyedByCore(in ctx);
	}

	protected virtual void OnDestroyedByCore(in DestroyContext ctx) { }

	public int ReservePicking(uint itemId, int quantity)
	{
		if (itemTotals.TryGetValue(itemId, out int val) == false)
		{
			Debug.LogError("NO ITEMS HERE");
			return 0;
		}

		int befReserved = itemsReservedPick.GetValueOrDefault(itemId);

		int canReserve = math.clamp(quantity, 0, val - befReserved);
		itemsReservedPick[itemId] = befReserved + canReserve;

		OnItemReservedPickChanged?.Invoke(this, itemId, canReserve);

		return canReserve;
	}

	public int ConsumeReservedPick(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int reserved = itemsReservedPick.GetValueOrDefault(itemId);
		if (reserved <= 0)
		{
			Debug.LogWarning($"[ShelfBase] Tried to consume unreserved pick. shelf={name}, item={itemId}, quantity={quantity}");
			return 0;
		}

		int consumed = math.min(quantity, reserved);
		int remaining = reserved - consumed;
		if (remaining > 0)
			itemsReservedPick[itemId] = remaining;
		else
			itemsReservedPick.Remove(itemId);

		OnItemReservedPickChanged?.Invoke(this, itemId, -consumed);

		if (consumed != quantity)
		{
			Debug.LogWarning($"[ShelfBase] Reserved pick was smaller than removed quantity. shelf={name}, item={itemId}, requested={quantity}, consumed={consumed}");
		}

		return consumed;
	}

}
