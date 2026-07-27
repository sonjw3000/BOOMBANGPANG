using System;
using System.Collections.Generic;

public abstract partial class ShelfBase
{
	public virtual ShelfContainerSaveData CaptureState(Func<OrderLine, int> registerOrderLine)
	{
		ShelfContainerSaveData data = new()
		{
			HasTemperatureState = true,
			CurrentTemperatureCelsius = CurrentTemperatureCelsius,
		};
		foreach (var stack in stacks)
		{
			data.Stacks.Add(new ItemStackSaveData
			{
				ItemId = stack.ItemID,
				Quantity = stack.Quantity,
				CurrentFreshness = stack.CurrentFreshness,
				CurrentIntegrity = stack.CurrentIntegrity,
				HasTemperatureState = true,
				CurrentTemperatureCelsius = stack.CurrentTemperatureCelsius,
				Status = stack.Status,
				OutboundStage = stack.OutboundStage,
				Quality = stack.Quality,
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
		SetCurrentTemperatureCelsius(
			data != null && data.HasTemperatureState
				? data.CurrentTemperatureCelsius
				: GridCell.DefaultTemperatureCelsius);

		if (data != null)
		{
			foreach (var stackData in data.Stacks)
			{
				ItemStack stack = ItemStack.Rent(
					stackData.ItemId,
					stackData.CurrentFreshness,
					stackData.CurrentIntegrity,
					stackData.Status,
					stackData.OutboundStage,
					stackData.Quality,
					stackData.HasTemperatureState
						? stackData.CurrentTemperatureCelsius
						: GridCell.DefaultTemperatureCelsius);
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
