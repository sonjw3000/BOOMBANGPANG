
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

	public ItemTransferPayload(IItemContainer from, IItemContainer to, uint itemID, int quantity, bool consumeSourcePickReservation = false)
	{
		From = from;
		To = to;
		ItemID = itemID;
		Quantity = quantity;
		ConsumeSourcePickReservation = consumeSourcePickReservation;
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

		int available = GetDefaultQuantity(payload.From, payload.ItemID);
		int acceptable = payload.To.GetAcceptableQuantity(payload.ItemID, payload.Quantity);
		int movable = Math.Min(payload.Quantity, Math.Min(available, acceptable));

		if (movable <= 0)
			return new(payload, 0);

		int removed = payload.From.RemoveItem(payload.ItemID, movable);
		ConsumeSourcePickReservation(payload.From, payload.ItemID, removed, payload.ConsumeSourcePickReservation);
		int moved = payload.To.AddItem(payload.ItemID, removed);

		if (moved != removed)
			Debug.LogError($"[ItemTransferUtility] MoveItem committed an unexpected amount. item={payload.ItemID}, planned={movable}, removed={removed}, moved={moved}");

		return new(payload, moved);
	}

	public static ItemTransferResult MoveItemAsStack(IItemContainer from, IItemContainer to, ItemStack stack, bool consumeSourcePickReservation = false)
	{
		if (from == null || to == null || stack == null || stack.Quantity <= 0)
			return new(new ItemTransferPayload(from, to, stack != null ? stack.ItemID : 0, 0), 0);

		ItemTransferPayload payload = new(from, to, stack.ItemID, stack.Quantity);
		if (GetDefaultQuantity(from, stack.ItemID) < stack.Quantity || to.CanAcceptStack(stack) == false)
			return new(payload, 0);

		int requestedQuantity = stack.Quantity;
		int removed = from.RemoveItem(stack.ItemID, stack.Quantity);
		ConsumeSourcePickReservation(from, stack.ItemID, removed, consumeSourcePickReservation);
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

		return new(payload, requestedQuantity);
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

		movedStack = stack.Split(acceptable);
		if (movedStack == null)
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

		if (stack.Quantity <= 0)
		{
			if (payload.From.RemoveStack(stack) == false)
				Debug.LogError("[ItemTransferUtility] Failed to detach emptied source stack after split move.");
			else
				stack.Recycle();
		}

		ConsumeSourcePickReservation(payload.From, movedStack.ItemID, movedStack.Quantity, payload.ConsumeSourcePickReservation);
		return true;
	}

	private static int GetDefaultQuantity(IItemContainer container, uint itemId)
	{
		if (container?.Stacks == null)
			return 0;

		int quantity = 0;
		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack != null && stack.HasItemID(itemId) && stack.IsDefaultIdentity)
				quantity += stack.Quantity;
		}

		return quantity;
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

		if (source is ShelfBase shelf)
		{
			shelf.ConsumeReservedPick(itemId, quantity);
			return;
		}

		Debug.LogWarning($"[ItemTransferUtility] Requested reserved pick consumption from unsupported source. source={source?.GetType().Name}, item={itemId}, quantity={quantity}");
	}
}
