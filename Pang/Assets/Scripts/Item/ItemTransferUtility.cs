
using System;
using UnityEngine;

public enum TransferResultKind
{
	None,
	Partial,
	Complete
}

public readonly struct ItemTransferResult
{
	public readonly uint ItemId;
	public readonly int Requested;
	public readonly int Moved;
	public readonly int Remaining;
	public readonly TransferResultKind Kind;

	public ItemTransferResult(in ItemTransferPayload payload, int moved)
	{
		ItemId = payload.ItemID;
		Requested = payload.Quantity;
		Moved = moved;
		Remaining = Math.Max(0, payload.Quantity - moved);

		Kind = moved <= 0 ?
			TransferResultKind.None :
			(moved >= payload.Quantity ? TransferResultKind.Complete : TransferResultKind.Partial);
	}
}

public readonly struct ItemTransferPayload
{
	public readonly IItemContainer From;
	public readonly IItemContainer To;
	public readonly uint ItemID;
	public readonly int Quantity;
	public readonly bool ConsumeSourcePickReservation;
	public readonly Predicate<ItemStack> StackPredicate;

	public ItemTransferPayload(
		IItemContainer from,
		IItemContainer to,
		uint itemID,
		int quantity,
		bool consumeSourcePickReservation = false,
		Predicate<ItemStack> stackPredicate = null)
	{
		From = from;
		To = to;
		ItemID = itemID;
		Quantity = quantity;
		ConsumeSourcePickReservation = consumeSourcePickReservation;
		StackPredicate = stackPredicate;
	}
}

public readonly struct FullyTransferPayload
{
	public readonly IItemContainer From;
	public readonly IItemContainer To;
	public readonly Action<ItemStack> OnStackMove;
	public readonly bool ConsumeSourcePickReservation;

	public FullyTransferPayload(IItemContainer from, IItemContainer to, Action<ItemStack> onStackMove = null, bool consumeSourcePickReservation = false)
	{
		From = from;
		To = to;
		OnStackMove = onStackMove;
		ConsumeSourcePickReservation = consumeSourcePickReservation;
	}
}


public static class ItemTransferUtility
{
	public static ItemTransferResult MoveItem(in ItemTransferPayload payload)
	{
		if (payload.From == null || payload.To == null || payload.Quantity <= 0)
		{
			if (payload.Quantity == 0)
				Debug.LogWarning("Tried to move zero quantity!");

			return new(payload, 0);
		}

		if (ReferenceEquals(payload.From, payload.To))
		{
			int sameContainerMovable = GetMatchingQuantity(payload.From, payload.ItemID, payload.Quantity, payload.StackPredicate);
			return new(payload, sameContainerMovable);
		}

		int remaining = payload.Quantity;
		int moved = 0;
		for (int i = payload.From.Stacks.Count - 1; i >= 0 && remaining > 0; --i)
		{
			ItemStack stack = payload.From.Stacks[i];
			if (CanMoveStack(stack, payload.ItemID, payload.StackPredicate) == false)
				continue;

			int movedFromStack = TryMoveStackQuantity(payload, stack, remaining);
			if (movedFromStack <= 0)
				continue;

			moved += movedFromStack;
			remaining -= movedFromStack;
		}

		return new(payload, moved);
	}

	public static ItemTransferResult MoveItemAsStack(
		IItemContainer from,
		IItemContainer to,
		ItemStack stack,
		bool consumeSourcePickReservation = false,
		Predicate<ItemStack> sourceStackPredicate = null)
	{
		if (from == null || to == null || stack == null || stack.Quantity <= 0)
			return new(new ItemTransferPayload(from, to, stack != null ? stack.ItemID : 0, 0), 0);

		ItemTransferPayload payload = new(
			from,
			to,
			stack.ItemID,
			stack.Quantity,
			consumeSourcePickReservation,
			sourceStackPredicate);

		if (to.CanAcceptStack(stack) == false)
			return new(payload, 0);

		int available = GetMatchingQuantity(from, stack.ItemID, stack.Quantity, sourceStackPredicate);
		if (available < stack.Quantity)
			return new(payload, 0);

		int removed = RemoveMatchingQuantity(from, stack.ItemID, stack.Quantity, sourceStackPredicate, consumeSourcePickReservation);
		if (removed != stack.Quantity)
		{
			Debug.LogError($"[ItemTransferUtility] MoveItemAsStack removed an unexpected amount. item={stack.ItemID}, requested={stack.Quantity}, removed={removed}");
			if (removed > 0)
				from.AddItem(stack.ItemID, removed);

			return new(payload, removed);
		}

		if (to.AddStack(stack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItemAsStack failed after CanAcceptStack returned true.");
			from.AddItem(stack.ItemID, removed);
			return new(payload, 0);
		}

		if (stack.Quantity <= 0)
			stack.Recycle();

		return new(payload, removed);
	}

	public static int GetMovableQuantity(
		IItemContainer from,
		IItemContainer to,
		uint itemId,
		int requested,
		Predicate<ItemStack> stackPredicate = null)
	{
		if (from == null || to == null || requested <= 0)
			return 0;

		if (ReferenceEquals(from, to))
			return GetMatchingQuantity(from, itemId, requested, stackPredicate);

		int remaining = requested;
		int movable = 0;
		for (int i = from.Stacks.Count - 1; i >= 0 && remaining > 0; --i)
		{
			ItemStack stack = from.Stacks[i];
			if (CanMoveStack(stack, itemId, stackPredicate) == false)
				continue;

			int stackMovable = GetStackTransferQuantity(to, stack, remaining);
			if (stackMovable <= 0)
				continue;

			movable += stackMovable;
			remaining -= stackMovable;
		}

		return movable;
	}

	public static TransferResultKind MoveAllStacks(in FullyTransferPayload payload)
	{
		if (payload.From == null || payload.To == null)
			return TransferResultKind.None;

		if (ReferenceEquals(payload.From, payload.To))
			return payload.From.Stacks.Count > 0 ? TransferResultKind.Complete : TransferResultKind.None;

		int stackCount = payload.From.Stacks.Count;
		int movedCount = 0;
		bool movedPartially = false;
		for (int i = stackCount - 1; i >= 0; --i)
		{
			var stack = payload.From.Stacks[i];
			if (payload.To.CanAcceptStack(stack) == false)
			{
				if (TryMovePartialStack(payload, stack, out ItemStack movedStack))
				{
					movedPartially = true;
					++movedCount;
					payload.OnStackMove?.Invoke(movedStack);
					movedStack?.Recycle();
					continue;
				}

				return movedCount == 0 ? TransferResultKind.None : TransferResultKind.Partial;
			}

			if (payload.From.RemoveStack(stack) == false)
				return movedCount == 0 ? TransferResultKind.None : TransferResultKind.Partial;

			int movedQuantity = stack.Quantity;
			ItemStack movedReportStack = stack.CloneWithQuantity(movedQuantity);
			if (payload.To.AddStack(stack) == false)
			{
				Debug.LogError("[ItemTransferUtility] MoveAllStacks failed after CanAcceptStack returned true.");
				payload.From.AddStack(stack);
				movedReportStack?.Recycle();
				return movedCount == 0 ? TransferResultKind.None : TransferResultKind.Partial;
			}

			ConsumeSourcePickReservation(payload.From, stack.ItemID, movedQuantity, payload.ConsumeSourcePickReservation);

			++movedCount;
			payload.OnStackMove?.Invoke(movedReportStack);
			movedReportStack?.Recycle();

			if (stack.Quantity <= 0)
				stack.Recycle();
		}

		return movedPartially ? TransferResultKind.Partial : TransferResultKind.Complete;
	}

	private static bool TryMovePartialStack(in FullyTransferPayload payload, ItemStack stack, out ItemStack movedStack)
	{
		movedStack = null;

		if (stack == null)
			return false;

		int acceptable = GetAcceptableQuantityForStack(payload.To, stack);
		if (acceptable <= 0)
			return false;

		if (payload.From.TryRemoveFromStack(stack, acceptable, out movedStack) == false)
			return false;

		int movedQuantity = movedStack.Quantity;
		if (payload.To.AddStack(movedStack) == false)
		{
			RestoreStack(payload.From, movedStack);
			movedStack = null;
			return false;
		}

		ItemStack movedReportStack = movedStack.CloneWithQuantity(movedQuantity);
		if (movedStack.Quantity <= 0)
			movedStack.Recycle();
		movedStack = movedReportStack;

		ConsumeSourcePickReservation(payload.From, movedStack.ItemID, movedStack.Quantity, payload.ConsumeSourcePickReservation);
		return true;
	}

	private static int GetMatchingQuantity(
		IItemContainer container,
		uint itemId,
		int requested,
		Predicate<ItemStack> stackPredicate)
	{
		if (container?.Stacks == null || requested <= 0)
			return 0;

		int quantity = 0;
		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (CanMoveStack(stack, itemId, stackPredicate) == false)
				continue;

			quantity += Math.Min(stack.Quantity, requested - quantity);
			if (quantity >= requested)
				break;
		}

		return quantity;
	}

	private static bool CanMoveStack(ItemStack stack, uint itemId, Predicate<ItemStack> stackPredicate)
	{
		return stack != null &&
			stack.Quantity > 0 &&
			stack.HasItemID(itemId) &&
			(stackPredicate == null || stackPredicate(stack));
	}

	private static int RemoveMatchingQuantity(
		IItemContainer container,
		uint itemId,
		int quantity,
		Predicate<ItemStack> stackPredicate,
		bool consumeSourcePickReservation)
	{
		if (container?.Stacks == null || quantity <= 0)
			return 0;

		int remaining = quantity;
		int removed = 0;
		for (int i = container.Stacks.Count - 1; i >= 0 && remaining > 0; --i)
		{
			ItemStack stack = container.Stacks[i];
			if (CanMoveStack(stack, itemId, stackPredicate) == false)
				continue;

			int removeFromStack = Math.Min(stack.Quantity, remaining);
			if (removeFromStack >= stack.Quantity)
			{
				if (container.RemoveStack(stack) == false)
					continue;

				removed += removeFromStack;
				remaining -= removeFromStack;
				ConsumeSourcePickReservation(container, itemId, removeFromStack, consumeSourcePickReservation);
				stack.Recycle();
				continue;
			}

			if (container.TryRemoveFromStack(stack, removeFromStack, out ItemStack removedStack) == false)
				continue;

			removed += removedStack.Quantity;
			remaining -= removedStack.Quantity;
			ConsumeSourcePickReservation(container, itemId, removedStack.Quantity, consumeSourcePickReservation);
			removedStack.Recycle();
		}

		return removed;
	}

	private static int TryMoveStackQuantity(in ItemTransferPayload payload, ItemStack stack, int requested)
	{
		int quantity = GetStackTransferQuantity(payload.To, stack, requested);
		if (quantity <= 0)
			return 0;

		if (quantity >= stack.Quantity)
			return MoveWholeStack(payload, stack);

		return MovePartialStack(payload, stack, quantity);
	}

	private static int MoveWholeStack(in ItemTransferPayload payload, ItemStack stack)
	{
		int movedQuantity = stack.Quantity;
		if (payload.From.RemoveStack(stack) == false)
			return 0;

		if (payload.To.AddStack(stack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItem failed after destination acceptance was calculated.");
			RestoreStack(payload.From, stack);
			return 0;
		}

		ConsumeSourcePickReservation(payload.From, stack.ItemID, movedQuantity, payload.ConsumeSourcePickReservation);

		if (stack.Quantity <= 0)
			stack.Recycle();

		return movedQuantity;
	}

	private static int MovePartialStack(in ItemTransferPayload payload, ItemStack stack, int quantity)
	{
		if (payload.From.TryRemoveFromStack(stack, quantity, out ItemStack movedStack) == false)
			return 0;

		int movedQuantity = movedStack.Quantity;
		if (payload.To.AddStack(movedStack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItem partial transfer failed after destination acceptance was calculated.");
			RestoreStack(payload.From, movedStack);
			return 0;
		}

		ConsumeSourcePickReservation(payload.From, movedStack.ItemID, movedQuantity, payload.ConsumeSourcePickReservation);

		if (movedStack.Quantity <= 0)
			movedStack.Recycle();

		return movedQuantity;
	}

	private static int GetStackTransferQuantity(IItemContainer to, ItemStack stack, int requested)
	{
		if (to == null || stack == null || requested <= 0)
			return 0;

		int quantity = Math.Min(stack.Quantity, requested);
		if (quantity <= 0)
			return 0;

		if (quantity == stack.Quantity && to.CanAcceptStack(stack))
			return quantity;

		int acceptable = Math.Min(quantity, GetAcceptableQuantityForStack(to, stack));
		if (acceptable <= 0)
			return 0;

		ItemStack probe = stack.CloneWithQuantity(acceptable);
		bool canAccept = to.CanAcceptStack(probe);
		probe?.Recycle();
		return canAccept ? acceptable : 0;
	}

	private static int GetAcceptableQuantityForStack(IItemContainer container, ItemStack stack)
	{
		if (container == null || stack == null || stack.Quantity <= 0)
			return 0;

		float itemSize = GameContext.Instance.ItemDB.GetItemSize(stack.ItemID);
		if (itemSize <= 0.0f)
			return 0;

		float availableSize = Math.Max(0.0f, container.MaxSize - container.TotalSize);
		int acceptable = Math.Min(stack.Quantity, Mathf.FloorToInt(availableSize / itemSize));
		return acceptable;
	}

	private static void RestoreStack(IItemContainer container, ItemStack stack)
	{
		if (container == null || stack == null)
			return;

		if (container.AddStack(stack))
		{
			if (stack.Quantity <= 0)
				stack.Recycle();

			return;
		}

		int restored = container.AddItem(stack.ItemID, stack.Quantity);
		if (restored != stack.Quantity)
		{
			Debug.LogError($"[ItemTransferUtility] Failed to restore moved stack. item={stack.ItemID}, requested={stack.Quantity}, restored={restored}");
		}

		stack.Recycle();
	}

	private static void ConsumeSourcePickReservation(IItemContainer source, uint itemId, int quantity, bool consume)
	{
		if (consume == false || quantity <= 0)
			return;

		if (source is IItemPickReservable reservable)
		{
			reservable.ConsumeReservedPick(itemId, quantity);
			return;
		}

		Debug.LogWarning($"[ItemTransferUtility] Requested reserved pick consumption from unsupported source. source={source?.GetType().Name}, item={itemId}, quantity={quantity}");
	}
}
