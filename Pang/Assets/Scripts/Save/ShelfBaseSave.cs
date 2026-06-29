using System;
using System.Collections.Generic;

public abstract partial class ShelfBase
{
	public virtual ShelfContainerSaveData CaptureState(Func<OrderLine, int> registerOrderLine)
	{
		ShelfContainerSaveData data = new();
		foreach (var stack in stacks)
		{
			data.Stacks.Add(new ItemStackSaveData
			{
				ItemId = stack.ItemID,
				Quantity = stack.Quantity,
				Freshness = stack.Freshness,
				Damage = stack.Damage,
				Status = stack.Status,
				OutboundStage = stack.OutboundStage,
			});
		}

		foreach (var entry in itemsReservedPick)
		{
			data.ReservedPick.Add(new ItemQuantitySaveData
			{
				ItemId = entry.Key,
				Quantity = entry.Value,
			});
		}

		return data;
	}

	public virtual void RestoreState(ShelfContainerSaveData data, IReadOnlyDictionary<int, OrderLine> orderLines)
	{
		for (int i = 0; i < stacks.Count; ++i)
			stacks[i]?.Recycle();

		stacks.Clear();
		itemTotals.Clear();
		itemsReservedPick.Clear();

		if (data != null)
		{
			foreach (var stackData in data.Stacks)
			{
				ItemStack stack = ItemStack.Rent(stackData.ItemId, stackData.Freshness, stackData.Damage, stackData.Status, stackData.OutboundStage);
				stack.AddItem(stackData.Quantity);
				AddStack(stack);
				if (stack.Quantity <= 0)
					stack.Recycle();
			}

			itemsReservedPick.Clear();
			foreach (var entry in data.ReservedPick)
			{
				itemsReservedPick[entry.ItemId] = entry.Quantity;
				OnItemReservedPickChanged?.Invoke(this, entry.ItemId, entry.Quantity);
			}
		}

		UpdateSize();
	}
}
