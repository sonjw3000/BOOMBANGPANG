using System;
using System.Collections.Generic;

public abstract partial class ShelfBase
{
	public virtual ShelfContainerSaveData CaptureState(Func<OrderLine, int> registerOrderLine)
	{
		ShelfContainerSaveData data = new();
		foreach (var stack in stacks)
		{
			if (stack is ItemPackage pkg)
			{
				data.Stacks.Add(new ItemStackSaveData
				{
					ItemId = pkg.ItemID,
					Quantity = pkg.Quantity,
					IsPackage = true,
					RelatedOrderLineId = registerOrderLine != null ? registerOrderLine(pkg.RelatedOrderLine) : -1,
					OutboundStage = pkg.OutboundStage,
				});
			}
			else
			{
				data.Stacks.Add(new ItemStackSaveData
				{
					ItemId = stack.ItemID,
					Quantity = stack.Quantity,
				});
			}
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
		stacks.Clear();
		itemTotals.Clear();
		itemsReservedPick.Clear();

		if (data != null)
		{
			foreach (var stackData in data.Stacks)
			{
				if (stackData.IsPackage &&
					orderLines != null &&
					orderLines.TryGetValue(stackData.RelatedOrderLineId, out var line))
				{
					AddStack(new ItemPackage(PackingType.Box, line, stackData.ItemId, stackData.Quantity, stackData.OutboundStage));
				}
				else
				{
					AddItem(stackData.ItemId, stackData.Quantity);
				}
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
