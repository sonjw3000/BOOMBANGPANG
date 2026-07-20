using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;



public abstract partial class ShelfBase :
	ItemInteraction,
	IItemContainer,
	IItemPickReservable,
	IFacilityUserRemovalGuard
{
	[SerializeField] protected int maxStacks = 16;
	[SerializeField] protected float sizePerStack;

	protected List<ItemStack> stacks;
	protected Dictionary<uint, int> itemTotals = new();
	protected Dictionary<uint, int> itemsReservedPick = new();

	private float totalSize = 0.0f;
	private ItemTag itemTags = ItemTag.None;

	public float TotalSize => totalSize;
	public float MaxSize => sizePerStack * maxStacks;

	public float FilledPercent => MaxSize <= 0 ? 0 : (TotalSize / MaxSize) * 100.0f;


	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	// item의 종류가 등록/해제 되었을 경우
	public event Action<ShelfBase, uint, bool> OnItemPresentChanged;

	// item quantity의 변경이 일어났을 경우
	public event Action<ShelfBase, uint, int> OnItemQuantityChanged;

	// item의 picking이 예약되었을 때 (quantity는 예약된 수량)
	public event Action<IItemContainer, uint, int> OnItemReservedPickChanged;

	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public IReadOnlyDictionary<uint, int> ItemToBePicked => itemsReservedPick;
	public ItemTag ItemTags => itemTags;
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

		if (FindDefaultStack(itemId) == null && CanCreateNewStack() == false)
			return 0;

		float availableSize = Mathf.Max(0.0f, MaxSize - totalSize);
		return Mathf.Clamp(Mathf.FloorToInt(availableSize / itemSize), 0, requested);
	}

	public bool CanAcceptStack(ItemStack stack)
	{
		if (stack == null || stack.Quantity <= 0)
			return false;

		if (stack.Size > Mathf.Max(0.0f, MaxSize - totalSize))
			return false;

		return FindMergeTarget(stack) != null || CanCreateNewStack();
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
		RebuildItemTags();
	}

	private void RebuildItemTags()
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


	public int AddItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int acceptable = GetAcceptableQuantity(itemId, quantity);
		if (acceptable <= 0)
			return 0;

		int befItemCounts = itemTotals.GetValueOrDefault(itemId);
		int befItemStacks = stacks.Count;
		ItemStack stack = FindDefaultStack(itemId);
		if (stack == null)
		{
			if (CanCreateNewStack() == false)
				return 0;

			stack = ItemStack.RentDefault(itemId);
			stacks.Add(stack);
		}

		int itemAdded = stack.AddItem(acceptable);
		itemTotals[itemId] = befItemCounts + itemAdded;

		int curItemStacks = stacks.Count;

		// 새로운 종류의 stack이 register 되었다면?
		if (befItemCounts == 0 && befItemStacks != curItemStacks)
		{
			OnItemPresentChanged?.Invoke(this, itemId, true);
		}

		OnItemQuantityChanged?.Invoke(this, itemId, itemAdded);

		UpdateSize();

		return itemAdded;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int remain = quantity;

		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack stack = stacks[i];

			if (stack.HasItemID(itemId) == false || stack.IsDefaultIdentity == false)
				continue;
			
			int itemRemoved = stack.RemoveItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) - itemRemoved;
			remain -= itemRemoved;
			if (stack.Quantity <= 0)
			{
				stacks.RemoveAt(i);
				stack.Recycle();
			}

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

		uint itemId = stack.ItemID;
		int quantity = stack.Quantity;
		int befItemCounts = itemTotals.GetValueOrDefault(itemId);
		ItemStack mergeTarget = FindMergeTarget(stack);
		if (mergeTarget != null)
		{
			if (mergeTarget.TryMergeFrom(stack) == false)
				return false;

			itemTotals[itemId] = befItemCounts + quantity;
		}
		else
		{
			stacks.Add(stack);
			itemTotals[itemId] = befItemCounts + quantity;
		}

		if (befItemCounts == 0)
			OnItemPresentChanged?.Invoke(this, itemId, true);

		OnItemQuantityChanged?.Invoke(this, itemId, quantity);

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
		{
			if (itemTotals[itemId] < 0)
				Debug.LogWarning($"Item total for {itemId} went below zero after removing stack quantity. Adjusting to zero.");

			itemTotals.Remove(itemId);
			OnItemPresentChanged?.Invoke(this, itemId, false);
		}

		if (stack.Quantity <= 0)
		{
			stacks.Remove(stack);
			stack.Recycle();
		}

		OnItemQuantityChanged?.Invoke(this, itemId, -removedQuantity);
		UpdateSize();
		return true;
	}

	public bool CanAccept(uint itemId, int quantity)
	{
		return GetAcceptableQuantity(itemId, quantity) >= quantity;
	}

	private bool CanCreateNewStack() => stacks.Count < maxStacks;

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
		ClearAllPickReservations();
		if (ctx.IsOverride && stacks != null)
		{
			ItemStack[] contents = stacks.ToArray();
			for (int i = 0; i < contents.Length; ++i)
			{
				ItemStack stack = contents[i];
				if (stack != null && RemoveStack(stack))
					stack.Recycle();
			}
		}

		OnDestroyedByCore(in ctx);
	}

	protected virtual void OnDestroyedByCore(in DestroyContext ctx) { }

	public bool CanUserRemove(out FacilityRemovalFailure failure)
	{
		if (stacks != null && stacks.Count > 0)
		{
			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.ContainsItems,
				"Move all items before removing this facility.");
			return false;
		}

		foreach (int reservedQuantity in itemsReservedPick.Values)
		{
			if (reservedQuantity <= 0)
				continue;

			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.HasReservation,
				"Release all item reservations before removing this facility.");
			return false;
		}

		failure = FacilityRemovalFailure.None;
		return true;
	}

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

	private void ClearAllPickReservations()
	{
		if (itemsReservedPick.Count <= 0)
			return;

		uint[] itemIds = itemsReservedPick.Keys.ToArray();
		for (int i = 0; i < itemIds.Length; ++i)
		{
			uint itemId = itemIds[i];
			int reserved = itemsReservedPick.GetValueOrDefault(itemId);
			if (reserved > 0)
				OnItemReservedPickChanged?.Invoke(this, itemId, -reserved);
		}

		itemsReservedPick.Clear();
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

	public int ReleaseReservedPick(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int reserved = itemsReservedPick.GetValueOrDefault(itemId);
		if (reserved <= 0)
			return 0;

		int released = math.min(quantity, reserved);
		int remaining = reserved - released;
		if (remaining > 0)
			itemsReservedPick[itemId] = remaining;
		else
			itemsReservedPick.Remove(itemId);

		OnItemReservedPickChanged?.Invoke(this, itemId, -released);
		return released;
	}

}
