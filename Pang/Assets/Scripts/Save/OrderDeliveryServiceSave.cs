public partial class OrderDeliveryService
{
	public void ResetRuntimeState()
	{
		deliveryProgresses.Clear();
	}

	public OrderDeliverySaveData CaptureState()
	{
		OrderDeliverySaveData data = new();
		foreach (var progress in deliveryProgresses)
		{
			data.Progresses.Add(new DeliveryProgressSaveData
			{
				Box = progress.Cargo == null
					? null
					: new BoxReferenceSaveData
					{
						BoxType = progress.Cargo.Type,
						BoxId = progress.Cargo.BoxId,
					},
				TimeRemain = progress.TimeRemain,
			});
		}

		return data;
	}

	public void RestoreState(OrderDeliverySaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (var progress in data.Progresses)
		{
			if (progress.Box == null || BoxMgr.TryGetBox(progress.Box.BoxType, progress.Box.BoxId, out var cargo) == false)
				continue;

			deliveryProgresses.Add(new DeliveryProgress(cargo, progress.TimeRemain));
		}
	}
}
