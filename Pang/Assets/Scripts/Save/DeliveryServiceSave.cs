public partial class DeliveryService
{
	public DeliveryQueueSaveData CaptureState()
	{
		DeliveryQueueSaveData data = new();
		foreach (var request in deliveryQueue.Enumerate())
		{
			data.Requests.Add(new DeliveryRequestSaveData
			{
				ContractId = request.RequestedContractID,
				ItemId = request.TargetItem.ItemID,
				Quantity = request.Quantity,
			});
		}

		return data;
	}

	public void RestoreState(DeliveryQueueSaveData data, ItemDatabase itemDatabase)
	{
		ResetRuntimeState();
		if (data == null || itemDatabase == null)
			return;

		foreach (var request in data.Requests)
		{
			if (itemDatabase.GetItemData(request.ItemId, out var item) == false)
				continue;

			RequestDelivery(request.ContractId, item, request.Quantity);
		}
	}

	public void ResetRuntimeState()
	{
		deliveryQueue.Clear();
	}
}
