
using System;
using Unity.Mathematics;
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
	public readonly bool HandlingDamageOccurred;

	public ItemTransferResult(in ItemTransferPayload payload, int moved, bool handlingDamageOccurred = false)
	{
		ItemId = payload.ItemID;
		Requested = payload.Quantity;
		Moved = moved;
		Remaining = Math.Max(0, payload.Quantity - moved);
		HandlingDamageOccurred = handlingDamageOccurred;

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
	public readonly AIWorker HandlingWorker;

	public ItemTransferPayload(
		IItemContainer from,
		IItemContainer to,
		uint itemID,
		int quantity,
		bool consumeSourcePickReservation = false,
		Predicate<ItemStack> stackPredicate = null,
		AIWorker handlingWorker = null)
	{
		From = from;
		To = to;
		ItemID = itemID;
		Quantity = quantity;
		ConsumeSourcePickReservation = consumeSourcePickReservation;
		StackPredicate = stackPredicate;
		HandlingWorker = handlingWorker;
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
		bool handlingDamageOccurred = false;
		for (int i = payload.From.Stacks.Count - 1; i >= 0 && remaining > 0; --i)
		{
			ItemStack stack = payload.From.Stacks[i];
			if (CanMoveStack(stack, payload.ItemID, payload.StackPredicate) == false)
				continue;

			int movedFromStack = TryMoveStackQuantity(payload, stack, remaining, out bool stackDamaged);
			if (movedFromStack <= 0)
				continue;

			moved += movedFromStack;
			remaining -= movedFromStack;
			handlingDamageOccurred |= stackDamaged;
		}

		if (moved > 0 && handlingDamageOccurred)
			payload.HandlingWorker?.ReportItemDamageIncident();

		return new(payload, moved, handlingDamageOccurred);
	}

	public static ItemTransferResult MoveItemAsStack(
		IItemContainer from,
		IItemContainer to,
		ItemStack stack,
		bool consumeSourcePickReservation = false,
		Predicate<ItemStack> sourceStackPredicate = null,
		AIWorker handlingWorker = null)
	{
		if (from == null || to == null || stack == null || stack.Quantity <= 0)
			return new(new ItemTransferPayload(from, to, stack != null ? stack.ItemID : 0, 0), 0);

		ItemTransferPayload payload = new(
			from,
			to,
			stack.ItemID,
			stack.Quantity,
			consumeSourcePickReservation,
			sourceStackPredicate,
			handlingWorker);

		ItemStack damagedStack = CreateDamagedTransferStack(payload, stack, stack.Quantity, out ItemDamageChange damageChange);
		ItemStack transferStack = damagedStack ?? stack;
		if (to.CanAcceptStack(transferStack) == false)
		{
			damagedStack?.Recycle();
			return new(payload, 0);
		}

		int available = GetMatchingQuantity(from, stack.ItemID, stack.Quantity, sourceStackPredicate);
		if (available < stack.Quantity)
		{
			damagedStack?.Recycle();
			return new(payload, 0);
		}

		int removed = RemoveMatchingQuantity(from, stack.ItemID, stack.Quantity, sourceStackPredicate, consumeSourcePickReservation);
		if (removed != stack.Quantity)
		{
			damagedStack?.Recycle();
			Debug.LogError($"[ItemTransferUtility] MoveItemAsStack removed an unexpected amount. item={stack.ItemID}, requested={stack.Quantity}, removed={removed}");
			if (removed > 0)
				from.AddItem(stack.ItemID, removed);

			return new(payload, removed);
		}

		if (to.AddStack(transferStack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItemAsStack failed after CanAcceptStack returned true.");
			from.AddItem(stack.ItemID, removed);
			damagedStack?.Recycle();
			return new(payload, 0);
		}

		bool handlingDamageOccurred = damagedStack != null;
		if (handlingDamageOccurred)
		{
			CommitTransferDamage(in payload, in damageChange);
			stack.Recycle();
			handlingWorker?.ReportItemDamageIncident();
		}

		if (transferStack.Quantity <= 0)
			transferStack.Recycle();

		return new(payload, removed, handlingDamageOccurred);
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

	private static int TryMoveStackQuantity(
		in ItemTransferPayload payload,
		ItemStack stack,
		int requested,
		out bool handlingDamageOccurred)
	{
		handlingDamageOccurred = false;
		int quantity = GetStackTransferQuantity(payload.To, stack, requested);
		if (quantity <= 0)
			return 0;

		if (quantity >= stack.Quantity)
			return MoveWholeStack(payload, stack, out handlingDamageOccurred);

		return MovePartialStack(payload, stack, quantity, out handlingDamageOccurred);
	}

	private static int MoveWholeStack(
		in ItemTransferPayload payload,
		ItemStack stack,
		out bool handlingDamageOccurred)
	{
		handlingDamageOccurred = false;
		int movedQuantity = stack.Quantity;
		ItemStack damagedStack = CreateDamagedTransferStack(payload, stack, movedQuantity, out ItemDamageChange damageChange);
		if (damagedStack != null && payload.To.CanAcceptStack(damagedStack) == false)
		{
			damagedStack.Recycle();
			return 0;
		}

		if (payload.From.RemoveStack(stack) == false)
		{
			damagedStack?.Recycle();
			return 0;
		}

		ItemStack transferStack = damagedStack ?? stack;
		if (payload.To.AddStack(transferStack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItem failed after destination acceptance was calculated.");
			RestoreStack(payload.From, stack);
			if (damagedStack != null)
				damagedStack.Recycle();
			return 0;
		}

		if (damagedStack != null)
		{
			CommitTransferDamage(in payload, in damageChange);
			stack.Recycle();
			handlingDamageOccurred = true;
		}

		ConsumeSourcePickReservation(payload.From, transferStack.ItemID, movedQuantity, payload.ConsumeSourcePickReservation);

		if (transferStack.Quantity <= 0)
			transferStack.Recycle();

		return movedQuantity;
	}

	private static int MovePartialStack(
		in ItemTransferPayload payload,
		ItemStack stack,
		int quantity,
		out bool handlingDamageOccurred)
	{
		handlingDamageOccurred = false;
		if (payload.From.TryRemoveFromStack(stack, quantity, out ItemStack movedStack) == false)
			return 0;

		int movedQuantity = movedStack.Quantity;
		ItemStack damagedStack = CreateDamagedTransferStack(payload, movedStack, movedQuantity, out ItemDamageChange damageChange);
		if (damagedStack != null && payload.To.CanAcceptStack(damagedStack) == false)
		{
			damagedStack.Recycle();
			RestoreStack(payload.From, movedStack);
			return 0;
		}

		ItemStack transferStack = damagedStack ?? movedStack;
		if (payload.To.AddStack(transferStack) == false)
		{
			Debug.LogError("[ItemTransferUtility] MoveItem partial transfer failed after destination acceptance was calculated.");
			RestoreStack(payload.From, movedStack);
			if (damagedStack != null)
				damagedStack.Recycle();
			return 0;
		}

		if (damagedStack != null)
		{
			CommitTransferDamage(in payload, in damageChange);
			movedStack.Recycle();
			handlingDamageOccurred = true;
		}

		ConsumeSourcePickReservation(payload.From, transferStack.ItemID, movedQuantity, payload.ConsumeSourcePickReservation);

		if (transferStack.Quantity <= 0)
			transferStack.Recycle();

		return movedQuantity;
	}

	private static ItemStack CreateDamagedTransferStack(
		in ItemTransferPayload payload,
		ItemStack sourceStack,
		int quantity,
		out ItemDamageChange damageChange)
	{
		damageChange = default;
		if (payload.HandlingWorker == null ||
			sourceStack == null ||
			sourceStack.Damage >= 100 ||
			quantity <= 0 ||
			GameContext.HasInstance == false)
			return null;

		ItemHandlingDamageService damageService = GameContext.Instance.ItemHandlingDamage;
		if (damageService == null ||
			damageService.TryRollDamage(payload.HandlingWorker, sourceStack, payload.To, out byte damageIncrease) == false)
		{
			return null;
		}

		ItemDamageService itemDamageService = GameContext.Instance.ItemDamage;
		if (itemDamageService == null ||
			itemDamageService.TryCreateDamagedStack(
				sourceStack,
				quantity,
				damageIncrease,
				ItemDamageCause.Handling,
				out ItemStack damagedStack,
				out damageChange) == false)
		{
			return null;
		}

		return damagedStack;
	}

	private static void CommitTransferDamage(
		in ItemTransferPayload payload,
		in ItemDamageChange damageChange)
	{
		if (damageChange.WasApplied == false || GameContext.HasInstance == false)
			return;

		if (TryResolveDamageOrigin(payload.To, payload.HandlingWorker, out int3 originCell) == false)
		{
			Debug.LogWarning(
				$"[ItemTransferUtility] Handling damage was applied without a grid origin. " +
				$"item={damageChange.ItemId}, destination={payload.To?.GetType().Name ?? "null"}");
			return;
		}

		GameContext.Instance.ItemDamage.CommitDamage(in damageChange, in originCell, payload.To);
	}

	private static bool TryResolveDamageOrigin(
		IItemContainer destination,
		AIWorker handlingWorker,
		out int3 originCell)
	{
		if (destination is IGridPlaceable gridPlaceable)
		{
			originCell = gridPlaceable.GridPosition;
			return true;
		}

		if (handlingWorker != null)
		{
			originCell = handlingWorker.GridPosition;
			return true;
		}

		originCell = default;
		return false;
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
