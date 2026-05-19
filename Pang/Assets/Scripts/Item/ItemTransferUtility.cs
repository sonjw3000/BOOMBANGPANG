
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

	public ItemTransferPayload(IItemContainer from, IItemContainer to, uint itemID, int quantity)
	{
		From = from;
		To = to;
		ItemID = itemID;
		Quantity = quantity;
	}
}

public readonly struct FullyTransferPayload
{
	public readonly IItemContainer From;
	public readonly IItemContainer To;
	public readonly Action<ItemStack> OnStackMove;

	public FullyTransferPayload(IItemContainer from, IItemContainer to, Action<ItemStack> onStackMove = null)
	{
		From = from;
		To = to;
		OnStackMove = onStackMove;
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

		int available = payload.From.GetQuantity(payload.ItemID);
		int acceptable = payload.To.GetAcceptableQuantity(payload.ItemID, payload.Quantity);
		int movable = Math.Min(payload.Quantity, Math.Min(available, acceptable));

		if (movable <= 0)
			return new(payload, 0);

		int removed = payload.From.RemoveItem(payload.ItemID, movable);
		int moved = payload.To.AddItem(payload.ItemID, removed);

		if (moved != removed)
			Debug.LogError($"[ItemTransferUtility] MoveItem committed an unexpected amount. item={payload.ItemID}, planned={movable}, removed={removed}, moved={moved}");

		return new(payload, moved);
	}

	public static ItemTransferResult MoveItemAsStack(IItemContainer from, IItemContainer to, ItemStack stack)
	{
		if (from == null || to == null || stack == null || stack.Quantity <= 0)
			return new(new ItemTransferPayload(from, to, stack != null ? stack.ItemID : 0, 0), 0);

		ItemTransferPayload payload = new(from, to, stack.ItemID, stack.Quantity);
		if (from.GetQuantity(stack.ItemID) < stack.Quantity || to.CanAcceptStack(stack) == false)
			return new(payload, 0);

		int removed = from.RemoveItem(stack.ItemID, stack.Quantity);
		if (removed != stack.Quantity)
		{
			Debug.LogError($"[ItemTransferUtility] MoveItemAsStack removed an unexpected amount. item={stack.ItemID}, requested={stack.Quantity}, removed={removed}");
			return new(payload, removed);
		}

		if (to.AddStack(stack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItemAsStack failed after CanAcceptStack returned true.");
			return new(payload, 0);
		}

		return new(payload, stack.Quantity);
	}

	public static TransferResultKind MoveAllStacks(in FullyTransferPayload payload)
	{
		if (payload.From == null || payload.To == null)
			return TransferResultKind.None;

		if (ReferenceEquals(payload.From, payload.To))
			return payload.From.Stacks.Count > 0 ? TransferResultKind.Complete : TransferResultKind.None;

		int stackCount = payload.From.Stacks.Count;
		int movedCount = 0;
		for (int i = stackCount - 1; i >= 0; --i)
		{
			var stack = payload.From.Stacks[i];
			if (payload.To.CanAcceptStack(stack) == false)
				return movedCount == 0 ? TransferResultKind.None : TransferResultKind.Partial;

			if (payload.From.RemoveStack(stack) == false)
				return movedCount == 0 ? TransferResultKind.None : TransferResultKind.Partial;

			if (payload.To.AddStack(stack) == false)
			{
				Debug.LogError("[ItemTransferUtility] MoveAllStacks failed after CanAcceptStack returned true.");
				payload.From.AddStack(stack);
				return movedCount == 0 ? TransferResultKind.None : TransferResultKind.Partial;
			}

			++movedCount;
			payload.OnStackMove?.Invoke(stack);
		}

		return TransferResultKind.Complete;
	}
}
