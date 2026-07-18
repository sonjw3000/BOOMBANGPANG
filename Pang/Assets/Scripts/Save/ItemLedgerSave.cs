public partial class ItemLedger
{
	public ItemLedgerSaveData CaptureState()
	{
		ItemLedgerSaveData data = new();
		foreach (var kv in itemTotals)
			data.Totals.Add(new ItemQuantitySaveData { ItemId = kv.Key, Quantity = kv.Value });

		foreach (var kv in reservedItems)
			data.Reserved.Add(new ItemQuantitySaveData { ItemId = kv.Key, Quantity = kv.Value });

		data.OrderableItems.AddRange(orderableItems);
		return data;
	}

	public void RestoreState(ItemLedgerSaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (var entry in data.Totals)
			itemTotals[entry.ItemId] = entry.Quantity;

		foreach (var entry in data.Reserved)
			reservedItems[entry.ItemId] = entry.Quantity;

		orderableItems.AddRange(data.OrderableItems);
		OnInventoryChanged?.Invoke();
	}

	public void ResetRuntimeState()
	{
		itemTotals.Clear();
		reservedItems.Clear();
		orderableItems.Clear();
		OnInventoryChanged?.Invoke();
	}
}
